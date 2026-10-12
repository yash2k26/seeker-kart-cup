// Race sessions, results, leaderboard, profile, events, deletion. Guest mode: a race never needs a wallet.
import type { Env } from './index';
import { json, bad, now, body } from './index';
import { signHmac, readHmac } from './auth';
import { getProfile } from './chain';
import { geoOf, referee, finishT, lapTimes, crossingsToFinish, type Sample } from './referee';
import { weekOf } from './weekly';

/** Is this a circuit the generator builds (TrackSetup.cs writes tracks.json)? Own keys only, never 'constructor'. */
export const knownTrack = (id: unknown): id is string => geoOf(id) !== undefined;

/** A device id as the phone sends it (SystemInfo.deviceUniqueIdentifier): short text, never a payload. */
const deviceOk = (d: unknown): d is string => typeof d === 'string' && d.length > 0 && d.length <= 64;

type RaceToken = { r: string; d: string; t: string; s: number };

/** POST /v1/race/start { deviceId, trackId, mode? } → { raceId, raceToken } — no session needed (guest mode).
 *  mode "weekly": a Kartel Weekly time trial, only on this week's circuit (anything else is an ordinary race). */
export async function raceStart(req: Request, env: Env) {
  const { deviceId, trackId, mode } = await body(req);
  if (!deviceOk(deviceId) || !knownTrack(trackId)) return bad('deviceId and known trackId required');
  const raceId = crypto.randomUUID();
  const startedMs = Date.now();
  const weekly = mode === 'weekly' && trackId === weekOf(now()).track ? 'weekly' : null;
  await env.DB.prepare('INSERT INTO race_sessions (race_id, device_id, track_id, started_at, mode, country) VALUES (?1, ?2, ?3, ?4, ?5, ?6)')
    .bind(raceId, deviceId, trackId, Math.floor(startedMs / 1000), weekly, req.headers.get('cf-ipcountry')).run();
  return json({ raceId, raceToken: await signHmac(env, { r: raceId, d: deviceId, t: trackId, s: startedMs } satisfies RaceToken) });
}

/** A result counts only if the path the phone drove (5 Hz samples [t s, x, y, z] from GO, the kart still on the grid)
 *  is a drive the referee believes over the line. The times stored are the referee's own, from that path; the times
 *  the phone reports are not used (typed-in numbers are no result, and where phone and referee differ at a trigger's
 *  edge the phone's own count would refuse an honest run). */
function validate(trackId: string, b: any, issuedAtMs: number): { why: string } | { totalMs: number; lapMs: number[] } {
  const g = geoOf(trackId);
  if (!g) return { why: 'unknown track' };
  const wall = Date.now() - issuedAtMs;
  if (!Array.isArray(b.lapMs) || typeof b.totalMs !== 'number' || !Number.isFinite(b.totalMs) || !Array.isArray(b.path)) return { why: 'shape' };
  if (!b.lapMs.map(Number).every(Number.isFinite)) return { why: 'shape' };
  if (b.path.length < 20 || b.path.length > 4000) return { why: 'path length' };
  const path: Sample[] = [];
  for (const q of b.path) {
    if (!Array.isArray(q) || q.length < 4) return { why: 'path shape' };
    const t = Number(q[0]);
    if (!(t >= 0)) return { why: 'path shape' };               // RaceSession records from GO
    path.push({ t, x: Number(q[1]), y: Number(q[2]), z: Number(q[3]) });
  }
  const s = referee(g, path, true);
  if (s.bad) return { why: s.bad };
  const ft = finishT(s, g);
  if (ft === null) return { why: `crossed ${s.n} of ${crossingsToFinish(g)} gates` };
  // Against the drive, not the claim: the phone asks for the token at GO and may wait up to its 15 s request timeout;
  // a pause only adds wall time. (A path computed in advance is the accepted limit of a client-timed race.)
  if (wall < ft * 1000 - 15_000) return { why: 'finished faster than wall clock' };
  if (wall > ft * 1000 + 60 * 60_000) return { why: 'session too old' };
  return { totalMs: Math.round(ft * 1000), lapMs: lapTimes(s, g, true).map(l => Math.round(l * 1000)) };   // RaceSession: lap 1 from GO
}

/** POST /v1/race/finish { raceToken, totalMs, lapMs[], checkpointMs[], position } → { accepted, isPersonalBest, raceId } */
export async function raceFinish(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  const b = await body(req);
  const tok = await readHmac<RaceToken>(env, b.raceToken);
  if (!tok) return bad('bad race token', 401);
  const v = validate(tok.t, b, tok.s);
  if ('why' in v) return bad(`rejected: ${v.why}`, 422);

  const session = await env.DB.prepare('SELECT wallet, finished_at, mode, country FROM race_sessions WHERE race_id = ?1').bind(tok.r).first<any>();
  if (!session) return bad('unknown race', 404);
  if (session.finished_at) return bad('already finished', 409);
  const owner: string | null = session.wallet ?? wallet;
  const bestLap = Math.min(...v.lapMs);

  let isPb = false;
  if (owner) {
    const prev = await env.DB.prepare('SELECT MIN(total_ms) AS best FROM results WHERE wallet = ?1 AND track_id = ?2').bind(owner, tok.t).first<any>();
    isPb = !prev?.best || v.totalMs < prev.best;
  }
  // The finish is claimed first, atomically: two copies of one finish arriving together (a retry over a slow link) both
  // passed the finished_at read above, and the second hit results.race_id UNIQUE as a 500 (scripts/stress.mjs, 12 Oct 2026).
  const claimed = await env.DB.prepare('UPDATE race_sessions SET finished_at = ?2, wallet = COALESCE(wallet, ?3) WHERE race_id = ?1 AND finished_at IS NULL')
    .bind(tok.r, now(), owner).run();
  if (!claimed.meta.changes) return bad('already finished', 409);
  await env.DB.prepare('INSERT OR IGNORE INTO results (race_id, device_id, wallet, track_id, total_ms, best_lap_ms, laps_json, position, is_pb, created_at, mode, country) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12)')
    .bind(tok.r, tok.d, owner, tok.t, v.totalMs, bestLap, JSON.stringify(v.lapMs), Number.isInteger(b.position) ? b.position : null, isPb ? 1 : 0, now(), session.mode, session.country).run();
  return json({ accepted: true, isPersonalBest: isPb, raceId: tok.r, totalMs: v.totalMs, lapMs: v.lapMs, memo: isPb ? `kc:pb:${tok.r}` : null });
}

/** POST /v1/race/claim { raceToken } (session) — binds a guest result to the wallet after Sign-In. */
export async function raceClaim(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  const { raceToken } = await body(req);
  const tok = await readHmac<RaceToken>(env, raceToken);
  if (!tok) return bad('bad race token', 401);
  await env.DB.batch([
    env.DB.prepare('UPDATE race_sessions SET wallet = ?2 WHERE race_id = ?1 AND wallet IS NULL').bind(tok.r, wallet),
    env.DB.prepare('UPDATE results SET wallet = ?2 WHERE race_id = ?1 AND wallet IS NULL').bind(tok.r, wallet),
  ]);
  // Only the wallet the result now belongs to has claimed it: another holder of the token learns nothing and marks nothing.
  const prev = await env.DB.prepare('SELECT total_ms, wallet FROM results WHERE race_id = ?1').bind(tok.r).first<any>();
  if (!prev || prev.wallet !== wallet) return json({ claimed: false, isPersonalBest: false, memo: null });
  const best = await env.DB.prepare('SELECT MIN(total_ms) AS best FROM results WHERE wallet = ?1 AND track_id = ?2 AND race_id != ?3').bind(wallet, tok.t, tok.r).first<any>();
  const isPb = !best?.best || prev.total_ms < best.best;
  if (isPb) await env.DB.prepare('UPDATE results SET is_pb = 1 WHERE race_id = ?1 AND wallet = ?2').bind(tok.r, wallet).run();
  return json({ claimed: true, isPersonalBest: isPb, memo: isPb ? `kc:pb:${tok.r}` : null });
}

/** POST /v1/demo/unlock { code } (session) — reviewer/demo code grants the SGT-gated cosmetics on a non-Seeker phone.
 *  Rows are `source = 'demo'`; refreshProfile never deletes unlocks, so they survive later logins. */
export async function demoUnlock(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  // Per wallet and per address: a fresh wallet is free to make, so the wallet alone is no limit.
  const ip = req.headers.get('cf-connecting-ip') ?? 'local';
  for (const key of ['demo:' + wallet, 'demo-ip:' + ip]) {
    if (env.RL_STRICT && !(await env.RL_STRICT.limit({ key })).success) return bad('too many attempts, try again in a minute', 429);
  }
  const { code } = await body(req);
  if (!env.DEMO_CODE || typeof code !== 'string' || code.trim().toUpperCase() !== env.DEMO_CODE.toUpperCase()) return bad('bad code', 403);
  await env.DB.batch(['kart.seeker_edition', 'badge.seeker'].map(sku =>
    env.DB.prepare("INSERT OR IGNORE INTO unlocks (wallet, sku, source, created_at) VALUES (?1, ?2, 'demo', ?3)").bind(wallet, sku, now())));
  return json(await getProfile(env, wallet));
}

/** GET /v1/leaderboard?track=beach&around=<wallet> */
export async function leaderboard(req: Request, env: Env) {
  const url = new URL(req.url);
  const track = url.searchParams.get('track') ?? 'beach';
  if (!knownTrack(track)) return bad('unknown track');
  const rows = await env.DB.prepare(`
    SELECT r.wallet, COALESCE(p.skr_name, substr(r.wallet,1,4) || '…' || substr(r.wallet,-4)) AS name,
           (p.is_seeker OR EXISTS (SELECT 1 FROM unlocks u WHERE u.wallet = r.wallet AND u.sku = 'badge.seeker')) AS isSeeker,
           MIN(r.total_ms) AS bestMs, r.memo_sig AS memoSig
    FROM results r JOIN players p USING (wallet)
    WHERE r.track_id = ?1 AND r.wallet IS NOT NULL
    GROUP BY r.wallet ORDER BY bestMs LIMIT 50`).bind(track).all();
  return json({ track, entries: rows.results.map((e: any, i) => ({ rank: i + 1, ...e, isSeeker: !!e.isSeeker })) },
  );
}

/** GET /v1/me (session) */
export async function me(_req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  return json(await getProfile(env, wallet));
}

/** DELETE /v1/me (session) — Publisher Policy: users must be able to delete their account and data.
 *  Payment records stay, with the wallet replaced: tx_log is what stops one on-chain payment being redeemed twice, and
 *  deleting it let a player pay a room once, leave for a refund, delete the account and redeem the same payment again.
 *  (The payments themselves are public on-chain; room_entries and payouts are the room's accounts and stay as well.) */
export async function deleteMe(_req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  await env.DB.batch([
    env.DB.prepare('DELETE FROM results WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM race_sessions WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM unlocks WHERE wallet = ?1').bind(wallet),
    env.DB.prepare("UPDATE tx_log SET wallet = 'deleted' WHERE wallet = ?1").bind(wallet),
    env.DB.prepare('DELETE FROM pending_tx WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM events WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM players WHERE wallet = ?1').bind(wallet),
  ]);
  return json({ deleted: true });
}

/** POST /v1/event { deviceId, name, props } — races, connects, returning devices, last exception. */
export async function event(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  const { deviceId, name, props } = await body(req);
  if (!deviceOk(deviceId) || !name || String(name).length > 64) return bad('bad event');
  await env.DB.prepare('INSERT INTO events (device_id, wallet, name, props, created_at) VALUES (?1, ?2, ?3, ?4, ?5)')
    .bind(deviceId, wallet, String(name), props ? JSON.stringify(props).slice(0, 4000) : null, now()).run();
  return json({ ok: true });
}
