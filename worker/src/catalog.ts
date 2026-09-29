// What is for sale and how a prize pool is split. One file, so the numbers a judge or an accountant asks about
// live in one place. Prices are base units as strings (SOL: lamports, 1e9; SKR: 6 decimals, 1e6).
// PRICES_JSON (env) can override any sku's price without a redeploy: {"kart.race-future":{"SKR":"5000000"}}.

import { baseUnits, type Rates } from './prices';

export type Currency = 'SOL' | 'SKR';
export const DECIMALS: Record<Currency, number> = { SOL: 9, SKR: 6 };

export type Sku = { sku: string; kind: 'kart' | 'paint' | 'bundle'; name: string; blurb: string; usd: number; skrOnly?: boolean; grants?: string[]; until?: string };

// research/23: sell karts and paint, never drivers (they carry the stat pips; three are real people) and never stats.
// Three rungs — paint $0.99, Street kart $2.99, Hero kart $4.99 — and two bundles: the Founder's Pack is the intended
// purchase, the Full Garage is the anchor that makes it look sane. Karts are cosmetic: same speed as every free kart.
export const CATALOG: Sku[] = [
  { sku: 'paint.sunset',   kind: 'paint', name: 'Sunset',   blurb: 'Ember fading to gold.',                    usd: 0.99 },
  { sku: 'paint.midnight', kind: 'paint', name: 'Midnight', blurb: 'Deep navy, gloss black trim.',             usd: 0.99 },
  { sku: 'paint.rally',    kind: 'paint', name: 'SKR Rally', blurb: 'Seeker violet. Only ever sold for SKR.',  usd: 0.99, skrOnly: true },
  { sku: 'paint.white',    kind: 'paint', name: 'White',    blurb: 'Clean white. Shows every scratch.',       usd: 0.99 },

  { sku: 'kart.taxi',             kind: 'kart', name: 'Mempool',   blurb: 'Takes everyone for a ride.',            usd: 2.99 },
  { sku: 'kart.van',              kind: 'kart', name: 'Airdrop',   blurb: 'Delivers. Eventually.',                 usd: 2.99 },
  { sku: 'kart.hatchback-sports', kind: 'kart', name: 'Hot Hatch', blurb: 'Point it and it goes.',                 usd: 2.99 },
  { sku: 'kart.police',           kind: 'kart', name: 'Slasher',   blurb: 'Enforces the racing line.',             usd: 2.99 },

  { sku: 'kart.firetruck',     kind: 'kart', name: 'Hotfix',       blurb: 'A fire truck. In a kart race. Yes.',    usd: 4.99 },
  { sku: 'kart.garbage-truck', kind: 'kart', name: 'Garbage Chain', blurb: 'Hauls the whole mempool.',             usd: 4.99 },
  { sku: 'kart.tractor',       kind: 'kart', name: 'Yield Farmer', blurb: 'Slow off the line, unstoppable after.', usd: 4.99 },
  { sku: 'kart.race-future',   kind: 'kart', name: 'Photon',       blurb: 'Low, wide, glued to the road.',         usd: 4.99 },

  { sku: 'bundle.founder', kind: 'bundle', name: "Founder's Pack", blurb: 'Photon, Hotfix, Mempool, Slasher, two paints and the Founder badge. Sold once, never again.',
    usd: 9.99, until: '2027-01-10T00:00:00Z', grants: ['kart.race-future', 'kart.firetruck', 'kart.taxi', 'kart.police', 'paint.sunset', 'paint.midnight', 'badge.founder'] },
  { sku: 'bundle.garage',  kind: 'bundle', name: 'Full Garage', blurb: 'Every kart and paint, including the ones we add this season, plus the Founder badge.',
    usd: 19.99, grants: ['kart.taxi', 'kart.van', 'kart.hatchback-sports', 'kart.police', 'kart.firetruck', 'kart.garbage-truck', 'kart.tractor', 'kart.race-future',
                         'paint.sunset', 'paint.midnight', 'paint.rally', 'paint.white', 'badge.founder'] },
];

/** A price quote for the client: the USD peg and today's base units per currency (env PRICES_JSON overrides base units by sku). */
export function quote(item: Sku, r: Rates, pricesJson?: string): Record<Currency, string> {
  let over: Record<string, Partial<Record<Currency, string>>> = {};
  try { over = pricesJson ? JSON.parse(pricesJson) : {}; } catch { /* keep computed */ }
  const q: Record<Currency, string> = { SKR: baseUnits(item.usd, 'SKR', r).toString(), SOL: baseUnits(item.usd, 'SOL', r).toString() };
  if (item.skrOnly) q.SOL = '';
  const o = over[item.sku];
  if (o && typeof o === 'object') for (const k of ['SKR', 'SOL'] as Currency[]) if (o[k]) q[k] = o[k]!;
  return q;
}

export function catalogQuoted(r: Rates, pricesJson?: string) {
  return CATALOG.map(s => ({ ...s, price: quote(s, r, pricesJson), onSale: !s.until || Date.parse(s.until) > Date.now() }));
}

/** What a purchase must have paid, in base units; null if the sku is unknown, off sale, or not sold in that currency. */
export function priceOf(sku: string, currency: Currency, r: Rates, pricesJson?: string): bigint | null {
  const item = CATALOG.find(s => s.sku === sku);
  if (!item || (item.until && Date.parse(item.until) <= Date.now())) return null;
  const q = quote(item, r, pricesJson)[currency];
  return q ? BigInt(q) : null;
}

export function grantsOf(sku: string): string[] {
  const item = CATALOG.find(s => s.sku === sku);
  return item ? [sku, ...(item.grants ?? [])] : [sku];
}

// ---- tournaments ----

// Entry fees a host may pick. A whitelist, not a free field: it keeps the pool maths and the UI chips in step and
// caps how much a first-time host can accidentally put on the line.
export const FEE_TIERS: Record<Currency, string[]> = {
  SOL: ['0', '10000000', '50000000', '100000000', '500000000'],     // free, 0.01, 0.05, 0.1, 0.5 SOL
  SKR: ['0', '5000000', '25000000', '100000000', '500000000'],      // free, 5, 25, 100, 500 SKR
};

// Places paid, by number of paying players (research/20: poker sit-and-go, Skillz, ZED Champions, Photo Finish).
// Percentages of the pool AFTER the platform fee.
export const SPLITS: Record<number, number[]> = { 2: [100], 3: [65, 35], 4: [65, 35], 5: [60, 25, 15], 6: [60, 25, 15] };

export const MIN_PAID_PLAYERS = 2;
export const HOST_SHARE_BPS = 2000;   // 20% of the platform fee goes back to whoever organised the room: inviting friends pays

export type Split = { feeBps: number; gross: bigint; cut: bigint; hostReward: bigint; prizes: bigint[] };

/** Pool → platform fee, host reward and prize per place (base units). Rounding dust goes to the winner so the sum is exact.
 *  Duels (2) and full grids (6) are fee-free: the first is the viral loop, the second is the "fill the grid" incentive. */
export function splitPool(pool: bigint, paidPlayers: number, cutBps: number): Split {
  if (paidPlayers < MIN_PAID_PLAYERS || pool <= 0n) return { feeBps: 0, gross: 0n, cut: 0n, hostReward: 0n, prizes: [] };
  const feeBps = paidPlayers === 2 || paidPlayers >= 6 ? 0 : cutBps;
  const gross = (pool * BigInt(feeBps)) / 10000n;
  const hostReward = (gross * BigInt(HOST_SHARE_BPS)) / 10000n;
  const cut = gross - hostReward;
  const distributable = pool - gross;
  const pct = SPLITS[Math.min(paidPlayers, 6)] ?? SPLITS[6];
  const prizes = pct.map(p => (distributable * BigInt(p)) / 100n);
  const dust = distributable - prizes.reduce((a, b) => a + b, 0n);
  prizes[0] += dust;
  return { feeBps, gross, cut, hostReward, prizes };
}

export function formatAmount(base: bigint | string, currency: Currency): string {
  const b = BigInt(base);
  const d = DECIMALS[currency];
  const whole = b / 10n ** BigInt(d);
  const frac = (b % 10n ** BigInt(d)).toString().padStart(d, '0').replace(/0+$/, '');
  return `${whole}${frac ? '.' + frac : ''} ${currency}`;
}
