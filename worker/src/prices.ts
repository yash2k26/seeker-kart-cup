// Everything is priced in SOL (catalog.ts) and paid in SOL or SKR; no dollar figure is shown or charged anywhere.
// The SKR price is the SOL price at today's SOL/SKR rate, less the premium below: SKR is the discount rail. The two
// market rates are fetched here, cached for five minutes, with a hard fallback so a dead price API never takes the
// shop down.
import type { Currency } from './catalog';

// Jupiter prices the SKR mint itself, and answers a Worker. CoinGecko is the second try: its edge returns 403 to a
// request with no User-Agent, which is what a Worker's fetch sends unless told otherwise (the live shop sat on the
// fallback for two weeks that way), so it gets one.
const SKR_MINT = 'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3', SOL_MINT = 'So11111111111111111111111111111111111111112';
const JUPITER = `https://lite-api.jup.ag/price/v3?ids=${SKR_MINT},${SOL_MINT}`;
const COINGECKO = 'https://api.coingecko.com/api/v3/simple/price?ids=seeker,solana&vs_currencies=usd';
const FALLBACK = { SKR: 0.0196, SOL: 121 };          // 27 Sep 2026; only used while both APIs are unreachable
const SOL_PREMIUM = 1.2;
const TTL_MS = 5 * 60_000;

export type Rates = { SKR: number; SOL: number; at: number; source: 'jupiter' | 'coingecko' | 'fallback' | 'env' };
let cache: Rates | null = null;

const FEEDS: [Rates['source'], string, (j: any) => [number, number]][] = [
  ['jupiter', JUPITER, j => [Number(j?.[SKR_MINT]?.usdPrice), Number(j?.[SOL_MINT]?.usdPrice)]],
  ['coingecko', COINGECKO, j => [Number(j?.seeker?.usd), Number(j?.solana?.usd)]],
];

export async function rates(env: { RATES_JSON?: string }): Promise<Rates> {
  if (env.RATES_JSON) { try { const r = JSON.parse(env.RATES_JSON); return { SKR: r.SKR, SOL: r.SOL, at: Date.now(), source: 'env' }; } catch { /* fall through */ } }
  if (cache && Date.now() - cache.at < TTL_MS) return cache;
  for (const [source, url, pick] of FEEDS) {
    try {
      const r = await fetch(url, { headers: { accept: 'application/json', 'user-agent': 'kartel-worker/1' }, signal: AbortSignal.timeout(4000) });
      const [skr, sol] = pick(await r.json());
      if (skr > 0 && sol > 0) { cache = { SKR: skr, SOL: sol, at: Date.now(), source }; return cache; }
      console.warn(`price feed ${source} answered ${r.status} without both prices`);
    } catch (e: any) { console.warn(`price feed ${source} failed`, e?.message ?? e); }
  }
  return cache ?? { ...FALLBACK, at: Date.now(), source: 'fallback' };
}

/** Base units for a price in SOL: the SOL amount itself, or its SKR equivalent less the premium, rounded to a clean
 *  amount so the sheet reads "555 SKR", never "553.27 SKR". */
export function baseUnits(sol: number, currency: Currency, r: Rates): bigint {
  if (currency === 'SOL') return BigInt(Math.max(1, Math.round(sol * 10_000))) * 100_000n;   // 0.0001 SOL steps
  const skr = sol * r.SOL / r.SKR / SOL_PREMIUM;
  const clean = skr >= 50 ? Math.round(skr / 5) * 5 : Math.max(1, Math.round(skr));
  return BigInt(clean) * 1_000_000n;
}
