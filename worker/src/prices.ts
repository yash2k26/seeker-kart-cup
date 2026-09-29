// USD pegs → live token amounts. Every live crypto shop does this (research/23 §D.44): a kart costs $2.99, and the
// number of SKR or SOL that is today is computed here, cached for five minutes, with a hard fallback so a dead price
// API never takes the shop down. SOL is accepted at +20%: SKR is the discount rail (the $10k SKR-integration hook).
import type { Currency } from './catalog';

const COINGECKO = 'https://api.coingecko.com/api/v3/simple/price?ids=seeker,solana&vs_currencies=usd';
const FALLBACK = { SKR: 0.0196, SOL: 121 };          // 27 Sep 2026; only used while the API is unreachable
const SOL_PREMIUM = 1.2;
const TTL_MS = 5 * 60_000;

export type Rates = { SKR: number; SOL: number; at: number; source: 'coingecko' | 'fallback' | 'env' };
let cache: Rates | null = null;

export async function rates(env: { RATES_JSON?: string }): Promise<Rates> {
  if (env.RATES_JSON) { try { const r = JSON.parse(env.RATES_JSON); return { SKR: r.SKR, SOL: r.SOL, at: Date.now(), source: 'env' }; } catch { /* fall through */ } }
  if (cache && Date.now() - cache.at < TTL_MS) return cache;
  try {
    const r = await fetch(COINGECKO, { headers: { accept: 'application/json' }, signal: AbortSignal.timeout(4000) });
    const j: any = await r.json();
    const skr = Number(j?.seeker?.usd), sol = Number(j?.solana?.usd);
    if (skr > 0 && sol > 0) { cache = { SKR: skr, SOL: sol, at: Date.now(), source: 'coingecko' }; return cache; }
  } catch (e: any) { console.warn('price fetch failed', e?.message ?? e); }
  return cache ?? { ...FALLBACK, at: Date.now(), source: 'fallback' };
}

/** Base units for a USD price. Rounded to clean amounts so the sheet reads "150 SKR", never "152.83 SKR". */
export function baseUnits(usd: number, currency: Currency, r: Rates): bigint {
  if (currency === 'SKR') {
    const skr = usd / r.SKR;
    const clean = skr >= 50 ? Math.round(skr / 5) * 5 : Math.max(1, Math.round(skr));
    return BigInt(clean) * 1_000_000n;
  }
  const sol = (usd * SOL_PREMIUM) / r.SOL;
  return BigInt(Math.max(1, Math.round(sol * 10_000))) * 100_000n;   // 0.0001 SOL steps
}

export function usdOf(base: bigint | string, currency: Currency, r: Rates): number {
  const b = Number(BigInt(base));
  return currency === 'SKR' ? (b / 1e6) * r.SKR : (b / 1e9) * r.SOL;
}
