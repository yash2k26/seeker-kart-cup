// Seeker Kart Cup backend — one Worker, one D1. See research/14 for the reasoning behind every route.
import { challenge, verify, readSession } from './auth';
import { raceStart, raceFinish, raceClaim, leaderboard, me, deleteMe, event, demoUnlock } from './race';
import { rpcProxy, txConfirm, purchaseVerify } from './chain';

export interface Env {
  DB: D1Database;
  ASSETS: Fetcher;
  APP_DOMAIN: string;
  PACKAGE_NAME: string;
  CERT_SHA256: string;
  SKR_MINT: string;
  TREASURY_OWNER: string;
  TREASURY_ATA: string;
  PRICES_JSON: string;
  HELIUS_URL: string;
  SESSION_HMAC_KEY: string;
  DEMO_CODE?: string;   // secret; reviewer/demo unlock of the SGT cosmetics (no Seeker on hand)
}

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
  'GET /v1/me': me,
  'DELETE /v1/me': deleteMe,
  'POST /v1/event': event,
  'POST /v1/demo/unlock': demoUnlock,
};

const CORS = {
  'access-control-allow-origin': '*',
  'access-control-allow-methods': 'GET,POST,DELETE,OPTIONS',
  'access-control-allow-headers': 'content-type,authorization',
};

export default {
  async fetch(req: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(req.url);
    if (req.method === 'OPTIONS') return new Response(null, { headers: CORS });

    // Digital Asset Links: the wallet verifies our package + signing cert against this file (MWA spec).
    if (url.pathname === '/.well-known/assetlinks.json') {
      const fps = env.CERT_SHA256.split(',').map(s => s.trim()).filter(Boolean);
      return Response.json([{
        relation: ['delegate_permission/common.handle_all_urls'],
        target: { namespace: 'android_app', package_name: env.PACKAGE_NAME, sha256_cert_fingerprints: fps },
      }], { headers: { 'cache-control': 'public, max-age=3600' } });
    }
    if (url.pathname === '/privacy' || url.pathname === '/terms') {
      return env.ASSETS.fetch(new Request(new URL(url.pathname + '.html', url), req));
    }

    const key = `${req.method} ${url.pathname}`;
    const handler = routes[key];
    if (!handler) return env.ASSETS.fetch(req); // icon.png, index, 404s

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
export const now = () => Math.floor(Date.now() / 1000);
