// Kartel backend — one Worker, one D1. See research/14 for the reasoning behind every route.
import { challenge, verify, readSession } from './auth';
import { raceStart, raceFinish, raceClaim, leaderboard, me, deleteMe, event, demoUnlock, saveGet, savePut } from './race';
import { weekly } from './weekly';
import { rpcProxy, txConfirm, purchaseVerify } from './chain';
import { roomsCreate, roomsInfo, roomsSocket, roomsPublic, invitePage, config } from './rooms';
export { RoomDO } from './room';

export interface Env {
  DB: D1Database;
  ASSETS: Fetcher;
  APP_DOMAIN: string;
  PACKAGE_NAME: string;
  CERT_SHA256: string;
  SKR_MINT: string;
  ORE_MINT?: string;                 // ORE (Regolith Labs): a third shop rail at the market rate. Unset = ORE off
  TREASURY_OWNER?: string;           // secret; see wrangler.toml
  TREASURY_ATA?: string;
  PRICES_JSON: string;
  HELIUS_URL: string;
  RPC_FALLBACKS?: string;            // "none": no public nodes after HELIUS_URL (the stress test's fake node)
  SESSION_HMAC_KEY: string;
  DEMO_CODE?: string;   // secret; reviewer/demo unlock of the SGT cosmetics (no Seeker on hand)
  // ---- rooms & tournaments ----
  ROOMS: DurableObjectNamespace<import('./room').RoomDO>;
  TREASURY_SECRET?: string;          // optional secret; only to derive TREASURY_OWNER when that is unset. Nothing pays out from it any more
  ESCROW_PROGRAM?: string;           // the prize escrow program (escrow/): paid rooms need it and REFEREE_SECRET
  REFEREE_SECRET?: string;           // secret; the escrow referee key (base58 or JSON byte array). It opens, locks and settles rooms,
                                     // and holds only fees and rent: stakes live in the program, never with this key
  HOUSE_CUT_BPS?: string;            // "1000" = 10% of every prize pool
  PAID_ROOMS?: string;               // "1" enables entry fees; anything else = free rooms only (kill switch)
  PAID_BLOCK?: string;               // jurisdictions where paid rooms are refused: "IN,BE,JP,KR,US-AR,US-CT,US-DE,US-LA,US-SD"
  PRIORITY_FEE_MICROLAMPORTS?: string;
  APK_URL?: string;                  // where the invite page sends people without the app
  MOCK_CHAIN?: string;               // "1" in .dev.vars only: fake shop payments and the geo header for the local smoke test
  RATES_JSON?: string;               // optional pin: {"SKR":0.0196,"SOL":121} instead of the live price feed
  RL?: RateLimit;                    // per-IP limit on the routes anyone can call without signing in
  RL_STRICT?: RateLimit;             // per-wallet limit on guessing the demo code
  RL_WIDE?: RateLimit;               // per-address limit across room codes (the per-room one is RL)
}

// Routes that cost something per call (a D1 write or read, an upstream RPC, waking a Durable Object): a per-IP budget
// per minute (RL). A session is no limit by itself: any keypair can sign in.
const LIMITED = new Set([
  'POST /rpc', 'POST /v1/auth/challenge', 'POST /v1/auth/verify', 'POST /v1/race/start', 'POST /v1/race/finish',
  'POST /v1/race/claim', 'POST /v1/tx/confirm', 'POST /v1/purchase/verify', 'POST /v1/demo/unlock', 'POST /v1/event',
  'POST /v1/rooms', 'GET /v1/rooms/public', 'GET /v1/config', 'GET /v1/leaderboard', 'GET /v1/weekly',
  'GET /v1/save', 'POST /v1/save',
]);

async function limited(req: Request, env: Env, key: string, rl: RateLimit | undefined = env.RL): Promise<boolean> {
  if (!rl) return false;
  const ip = req.headers.get('cf-connecting-ip') ?? 'local';
  return !(await rl.limit({ key: `${key}|${ip}` })).success;
}
const slowDown = () => Response.json({ error: 'slow down' }, { status: 429, headers: CORS });

type Handler = (req: Request, env: Env, ctx: ExecutionContext, wallet: string | null) => Promise<Response>;

// ponytail: a route table, not a framework. Method + exact path (or a prefix for the query routes).
const routes: Record<string, Handler> = {
  'POST /rpc': rpcProxy,
  'POST /v1/auth/challenge': challenge,
  'POST /v1/auth/verify': verify,
  'POST /v1/race/start': raceStart,
  'POST /v1/race/finish': raceFinish,
  'POST /v1/race/claim': raceClaim,
  'POST /v1/tx/confirm': txConfirm,
  'POST /v1/purchase/verify': purchaseVerify,
  'GET /v1/leaderboard': leaderboard,
  'GET /v1/weekly': weekly,
  'GET /v1/me': me,
  'DELETE /v1/me': deleteMe,
  'GET /v1/save': saveGet,
  'POST /v1/save': savePut,
  'POST /v1/event': event,
  'POST /v1/demo/unlock': demoUnlock,
  'POST /v1/rooms': roomsCreate,
  'GET /v1/rooms/public': roomsPublic,
  'GET /v1/config': config,
};

const CORS = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'GET,POST,DELETE,OPTIONS',
  'access-control-allow-headers': 'content-type,authorization',
};

export default {
  async fetch(req: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(req.url);
    // MOCK_CHAIN fakes payments and payouts for the local smoke test. It must never be live: refuse to serve anything
    // on a real host while it is set, rather than trust that nobody ever pastes it into the dashboard.
    if (env.MOCK_CHAIN === '1' && !/^(localhost|127\.0\.0\.1|\[::1\])$/.test(url.hostname)) return bad('misconfigured', 503);
    if (req.method === 'OPTIONS') return new Response(null, { headers: CORS });

    // Digital Asset Links: the wallet verifies our package + signing cert against this file (MWA spec).
    if (url.pathname === '/.well-known/assetlinks.json') {
      const fps = env.CERT_SHA256.split(',').map(s => s.trim()).filter(Boolean);
      return Response.json([{
        relation: ['delegate_permission/common.handle_all_urls'],
        target: { namespace: 'android_app', package_name: env.PACKAGE_NAME, sha256_cert_fingerprints: fps },
      }], { headers: { 'cache-control': 'public, max-age=3600' } });
    }
    // Rooms: /v1/rooms/<CODE> (state), /v1/rooms/<CODE>/ws (socket → Durable Object), /r/<CODE> (invite page)
    const room = url.pathname.match(/^\/v1\/rooms\/([A-Za-z0-9]{5})(\/ws)?$/);
    if (room && req.method === 'GET') {
      // Each one wakes a Durable Object. Keyed by room as well as address: phones sharing one Wi-Fi reconnecting to
      // their own room must not use up each other's budget, or a stranger's calls to other codes use up theirs.
      if (await limited(req, env, `${room[2] ? 'GET /v1/rooms/ws' : 'GET /v1/rooms/code'}|${room[1].toUpperCase()}`)) return slowDown();
      if (await limited(req, env, 'GET /v1/rooms/*', env.RL_WIDE)) return slowDown();   // and across codes, per address
      try { return room[2] ? await roomsSocket(req, env, room[1]) : await roomsInfo(req, env, room[1]); }
      catch (e: any) { console.error('rooms', e?.stack ?? e); return Response.json({ error: 'internal' }, { status: 500, headers: CORS }); }
    }
    const invite = url.pathname.match(/^\/r\/([A-Za-z0-9]{5})\/?$/);
    if (invite && req.method === 'GET') {
      if (await limited(req, env, 'GET /r') || await limited(req, env, 'GET /v1/rooms/*', env.RL_WIDE)) return slowDown();
      return invitePage(req, env, invite[1]);
    }
    if (url.pathname === '/privacy' || url.pathname === '/terms') {
      return env.ASSETS.fetch(new Request(new URL(url.pathname + '.html', url), req));
    }

    const key = `${req.method} ${url.pathname}`;
    const handler = routes[key];
    if (LIMITED.has(key) && await limited(req, env, key)) return slowDown();
    if (!handler) {   // icon.png, index; the assets binding throws on a miss (observed as a 1101), so a miss is a plain 404
      if (req.method !== 'GET') return bad('not found', 404);
      try { return await env.ASSETS.fetch(req); } catch { return bad('not found', 404); }
    }

    try {
      const wallet = await readSession(env, req.headers.get('authorization'));
      const res = await handler(req, env, ctx, wallet);
      Object.entries(CORS).forEach(([k, v]) => res.headers.set(k, v));
      return res;
    } catch (e: any) {
      console.error(key, e?.stack ?? e);
      return Response.json({ error: 'internal' }, { status: 500, headers: CORS });
    }
  },
} satisfies ExportedHandler<Env>;

export const json = (data: unknown, status = 200) => Response.json(data, { status });
export const bad = (msg: string, status = 400) => Response.json({ error: msg }, { status });
/** A request's JSON body as an object to take fields from: {} for no body, broken JSON, or a body that is valid JSON but
 *  not an object ("null", "[]", "12"). Destructuring the raw parse threw on "null" and four routes answered 500 (12 Oct
 *  2026, scripts/stress.mjs). */
export const body = async (req: Request): Promise<any> => {
  const v = await req.json<any>().catch(() => null);
  return v !== null && typeof v === 'object' && !Array.isArray(v) ? v : {};
};
export const now = () => Math.floor(Date.now() / 1000);
