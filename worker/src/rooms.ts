// HTTP side of rooms: create (→ a code), inspect, the WebSocket hand-off to the Durable Object, the invite page,
// and the client config (treasury, escrow program, stake tiers, catalog) so the APK carries no addresses of its own.
import type { Env } from './index';
import { json, bad, now, body } from './index';
import { knownTrack } from './race';
import { CATALOG, STAKE_TIERS, MIN_PAID_PLAYERS, catalogQuoted, formatAmount, type Currency } from './catalog';
import { rates } from './prices';
import { validStake, geoBlocked, guestId, MAX_PLAYERS } from './room';
import { treasuryAddrs } from './payout';
import { escrowConfigured, Referee } from './escrow';
import { rpc } from './rpc';

// No 0/O/1/I: a code is read aloud across a room and typed on a phone in landscape.
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
function code(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(5));
  return Array.from(bytes, b => ALPHABET[b % ALPHABET.length]).join('');
}

const stub = (env: Env, c: string) => env.ROOMS.get(env.ROOMS.idFromName(c));

// The referee pays each escrow's rent (back when the room closes) and every fee. New paid rooms stop while it holds
// less than this, so the rooms already running can always lock, settle and pay out.
// ponytail: the escrow opens with the room, so free sign-ins can tie up rent until their lobbies expire (30 min) and
// starve new paid rooms; open it on the first stake attempt instead if that is ever seen.
const REFEREE_RESERVE_LAMPORTS = 30_000_000;

/** POST /v1/rooms { tracks: string[], minStake: string, currency: 'SOL'|'SKR', device, isPublic? } → { code, url }.
 *  minStake '0' is a free room. A paid room takes each player's stake from minStake up to the currency's top tier,
 *  held by the escrow program until the race is settled. */
export async function roomsCreate(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  const b = await body(req);
  const currency: Currency = b.currency === 'SKR' ? 'SKR' : 'SOL';
  // An app from before the escrow sends `fee`, and would pay its stake to the treasury where no room can see it.
  if (b.minStake === undefined && b.fee !== undefined && String(b.fee) !== '0') return bad('update the app to host paid races', 426);
  const minStake = String(b.minStake ?? '0');
  const paid = minStake !== '0';
  const tracks: string[] = Array.isArray(b.tracks) ? b.tracks.map(String) : [];
  if (!tracks.length || tracks.length > 4 || tracks.some(t => !knownTrack(t))) return bad('tracks must be 1–4 known track ids');
  if (paid && !validStake(minStake, currency, STAKE_TIERS)) return bad('stake not offered');
  if (paid && !wallet) return bad('sign in to host a paid room', 401);
  if (paid && (env.PAID_ROOMS !== '1' || !escrowConfigured(env))) return bad('paid rooms are switched off', 403);
  if (paid && geoBlocked(env, geoOf(req, env))) return bad('paid races are not available in your region', 451);
  if (paid) {
    const referee = new Referee(env).key.publicKey.toBase58();
    const balance: number = await rpc(env, 'getBalance', [referee, { commitment: 'confirmed' }]).then(r => r.value).catch(() => 0);
    if (balance < REFEREE_RESERVE_LAMPORTS) { console.error('referee below its reserve', referee, balance); return bad('paid rooms are busy right now', 503); }
  }
  const tiers = STAKE_TIERS[currency];
  const maxStake = paid ? tiers[tiers.length - 1] : '0';
  const device = String(b.device ?? '').slice(0, 64);
  if (!wallet && !device) return bad('deviceId required');
  const host = wallet ?? await guestId(device);
  const isPublic = b.isPublic === true;

  for (let attempt = 0; attempt < 5; attempt++) {
    const c = code();
    const taken = await env.DB.prepare('SELECT 1 FROM rooms WHERE code = ?1').bind(c).first();
    if (taken) continue;
    const r = await stub(env, c).fetch('https://room/init', {
      method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ code: c, host, tracks, minStake, maxStake, currency, isPublic, cutBps: Number(env.HOUSE_CUT_BPS ?? '1000') }),
    });
    if (r.status === 409) continue;
    await env.DB.prepare("INSERT INTO rooms (code, host, tracks, fee, currency, state, created_at, is_public, updated_at) VALUES (?1,?2,?3,?4,?5,'lobby',?6,?7,?6)")
      .bind(c, host, JSON.stringify(tracks), minStake, currency, now(), isPublic ? 1 : 0).run();
    return json({ code: c, url: `https://${env.APP_DOMAIN}/r/${c}` });
  }
  return bad('could not allocate a room code', 503);
}

/** GET /v1/rooms/public → open public lobbies with at least one racer waiting, newest activity first. */
export async function roomsPublic(_req: Request, env: Env) {
  const rows = await env.DB.prepare(`
    SELECT code, host_name, players, tracks, fee, currency, created_at FROM rooms
    WHERE is_public = 1 AND state = 'lobby' AND players > 0 AND updated_at > ?1
    ORDER BY updated_at DESC LIMIT 12`).bind(now() - 30 * 60).all<any>();
  return json({ rooms: rows.results.map(r => ({ code: r.code, host: r.host_name ?? 'Racer', players: r.players, maxPlayers: MAX_PLAYERS,
    tracks: JSON.parse(r.tracks), minStake: r.fee, currency: r.currency, createdAt: r.created_at })) });
}

/** GET /v1/rooms/:code → live snapshot (404 if unknown). */
export async function roomsInfo(_req: Request, env: Env, c: string) {
  const r = await stub(env, c.toUpperCase()).fetch('https://room/state');
  return new Response(r.body, { status: r.status, headers: { 'content-type': 'application/json' } });
}

/** GET /v1/rooms/:code/ws — the upgrade is answered by the Durable Object itself. */
export async function roomsSocket(req: Request, env: Env, c: string) {
  if (req.headers.get('Upgrade') !== 'websocket') return bad('expected a websocket', 426);
  const fwd = new Request(req);
  fwd.headers.set('x-kc-geo', geoOf(req, env));
  return stub(env, c.toUpperCase()).fetch(fwd);
}

/** "CC" or "CC-RR" from Cloudflare's request metadata; empty when unknown (local dev). */
export function geoOf(req: Request, env?: Env): string {
  if (env?.MOCK_CHAIN === '1') return req.headers.get('x-kc-geo') ?? '';   // local smoke test drives the gate by header
  const cf: any = (req as any).cf ?? {};
  return cf.country ? (cf.regionCode ? `${cf.country}-${cf.regionCode}` : String(cf.country)) : '';
}

/** GET /v1/config — everything the client must not hardcode. */
export async function config(_req: Request, env: Env) {
  const { owner: treasuryOwner, ata: treasuryAta, oreAta: treasuryOreAta } = treasuryAddrs(env);   // the same addresses payments are checked against
  const r = await rates(env);
  return json({
    treasuryOwner, treasuryAta, skrMint: env.SKR_MINT, oreMint: env.ORE_MINT ?? '', treasuryOreAta,   // no oreMint = the ORE button never shows
    rates: { SKR: r.SKR, SOL: r.SOL, ORE: r.ORE, source: r.source },   // market rates behind the SKR quote; not shown to players
    paidRooms: env.PAID_ROOMS === '1' && escrowConfigured(env) && !geoBlocked(env, geoOf(_req, env)),
    escrowProgram: env.ESCROW_PROGRAM ?? null,   // stakes go to this program's room accounts, never to the treasury
    cutBps: Number(env.HOUSE_CUT_BPS ?? '1000'), minPaidPlayers: MIN_PAID_PLAYERS, maxPlayers: MAX_PLAYERS,
    stakes: STAKE_TIERS, catalog: catalogQuoted(r, env.PRICES_JSON),
  });
}

/** GET /r/:code — the link a friend receives. Android with the app installed opens the app; everyone else sees this. */
export async function invitePage(_req: Request, env: Env, c: string) {
  const upper = c.toUpperCase();
  const r = await stub(env, upper).fetch('https://room/state');
  const room: any = r.ok ? await r.json() : null;
  const esc = (s: string) => s.replace(/[&<>"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[ch]!));
  const trackNames = room ? room.tracks.map((t: string) => esc(t)).join(' · ') : '';
  const fee = room && room.minStake !== '0' ? `Stakes from ${formatAmount(room.minStake, room.currency)} · Pot ${formatAmount(room.pool, room.currency)}` : 'Free race';
  const status = !room ? 'This room does not exist or has closed.'
    : room.state === 'lobby' ? `${room.players.length}/${MAX_PLAYERS} racers waiting · ${trackNames}<br>${fee}`
    : room.state === 'cancelled' ? 'This room has closed.' : 'The race has already started.';
  // intent:// carries host+path as the data URI: with scheme=kartcup this becomes kartcup://r/CODE, the manifest's fallback filter.
  const intent = `intent://r/${upper}#Intent;scheme=kartcup;package=${env.PACKAGE_NAME};S.browser_fallback_url=${encodeURIComponent(env.APK_URL ?? `https://${env.APP_DOMAIN}/`)};end`;
  const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Kartel · Room ${upper}</title>
<style>body{margin:0;padding:48px 20px;font:16px/1.5 system-ui,sans-serif;background:#11242E;color:#F4F1EA;text-align:center}
.code{font:700 64px/1 system-ui,sans-serif;letter-spacing:.12em;margin:24px 0 8px}.muted{color:#93AEBC}
a.btn{display:inline-block;margin-top:28px;padding:18px 36px;border-radius:999px;background:#FF5A36;color:#fff;font-weight:700;text-decoration:none}
a{color:#FFB03A}</style></head><body>
<img src="/icon.png" alt="" width="96" height="96" style="border-radius:22px">
<p class="muted">You're invited to a race</p><div class="code">${upper}</div>
<p class="muted">${status}</p>
${room && room.state === 'lobby' ? `<a class="btn" href="${intent}">Open in Kartel</a>` : ''}
<p class="muted" style="margin-top:32px">No app yet? <a href="${esc(env.APK_URL ?? '/')}">Get Kartel</a>, then enter the code under RACE ONLINE.</p>
<p><a href="/privacy">Privacy</a> · <a href="/terms">Terms</a></p></body></html>`;
  return new Response(html, { status: room ? 200 : 404, headers: { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' } });
}

export { CATALOG };
