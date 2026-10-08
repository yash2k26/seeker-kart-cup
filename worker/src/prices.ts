// Everything is priced in SOL (catalog.ts) and paid in SOL, SKR or ORE; no dollar figure is shown or charged anywhere.
// The SKR price is the SOL price at today's SOL/SKR rate, less the premium below: SKR is the discount rail. The three
// market rates are fetched here, cached for five minutes, with a hard fallback so a dead price API never takes the
// shop down.
import type { Currency } from './catalog';

// Jupiter prices the SKR mint itself, and answers a Worker. CoinGecko is the second try: its edge returns 403 to a
// request with no User-Agent, which is what a Worker's fetch sends unless told otherwise (the live shop sat on the
// fallback for two weeks that way), so it gets one.
const SKR_MINT = 'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3', SOL_MINT = 'So11111111111111111111111111111111111111112';
// ORE (Regolith Labs), since 8 Oct 2026: a third rail at the market rate, no premium either way. 11 decimals.
const ORE_MINT = 'oreoU2P8bN6jkk3jbaiVxYnG1dCXcYxwhwyK9jSybcp';
const JUPITER = `https://lite-api.jup.ag/price/v3?ids=${SKR_MINT},${SOL_MINT},${ORE_MINT}`;
const COINGECKO = 'https://api.coingecko.com/api/v3/simple/price?ids=seeker,solana,ore&vs_currencies=usd';
const FALLBACK = { SKR: 0.0196, SOL: 121, ORE: 110 };   // SKR/SOL 27 Sep, ORE 8 Oct 2026; only used while both APIs are unreachable
const SOL_PREMIUM = 1.2;
const TTL_MS = 5 * 60_000;

export type Rates = { SKR: number; SOL: number; ORE: number; at: number; source: 'jupiter' | 'coingecko' | 'fallback' | 'env' };
let cache: Rates | null = null;

const FEEDS: [Rates['source'], string, (j: any) => [number, number, number]][] = [
  ['jupiter', JUPITER, j => [Number(j?.[SKR_MINT]?.usdPrice), Number(j?.[SOL_MINT]?.usdPrice), Number(j?.[ORE_MINT]?.usdPrice)]],
  ['coingecko', COINGECKO, j => [Number(j?.seeker?.usd), Number(j?.solana?.usd), Number(j?.ore?.usd)]],
];

export async function rates(env: { RATES_JSON?: string }): Promise<Rates> {
  if (env.RATES_JSON) { try { const r = JSON.parse(env.RATES_JSON); return { SKR: r.SKR, SOL: r.SOL, ORE: r.ORE ?? FALLBACK.ORE, at: Date.now(), source: 'env' }; } catch { /* fall through */ } }
  if (cache && Date.now() - cache.at < TTL_MS) return cache;
  for (const [source, url, pick] of FEEDS) {
    try {
      const r = await fetch(url, { headers: { accept: 'application/json', 'user-agent': 'kartel-worker/1' }, signal: AbortSignal.timeout(4000) });
      const [skr, sol, ore] = pick(await r.json());
      // a feed without ORE must not take the shop down: keep the last ORE rate, else the fallback
      if (skr > 0 && sol > 0) { cache = { SKR: skr, SOL: sol, ORE: ore > 0 ? ore : cache?.ORE ?? FALLBACK.ORE, at: Date.now(), source }; return cache; }
      console.warn(`price feed ${source} answered ${r.status} without both prices`);
    } catch (e: any) { console.warn(`price feed ${source} failed`, e?.message ?? e); }
  }
  return cache ?? { ...FALLBACK, at: Date.now(), source: 'fallback' };
}

/** Base units for a price in SOL: the SOL amount itself, its SKR equivalent less the premium, or its ORE equivalent at
 *  par, rounded to a clean amount so the sheet reads "555 SKR" or "0.031 ORE", never "553.27 SKR". */
export function baseUnits(sol: number, currency: Currency, r: Rates): bigint {
  if (currency === 'SOL') return BigInt(Math.max(1, Math.round(sol * 10_000))) * 100_000n;   // 0.0001 SOL steps
  if (currency === 'ORE') return BigInt(Math.max(1, Math.round(sol * r.SOL / r.ORE * 1000))) * 100_000_000n;   // 0.001 ORE steps (11 decimals)
  const skr = sol * r.SOL / r.SKR / SOL_PREMIUM;
  const clean = skr >= 50 ? Math.round(skr / 5) * 5 : Math.max(1, Math.round(skr));
  return BigInt(clean) * 1_000_000n;
}
