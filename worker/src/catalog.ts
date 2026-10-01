// What is for sale and how a prize pool is split. One file, so the numbers a judge or an accountant asks about
// live in one place. Prices are base units as strings (SOL: lamports, 1e9; SKR: 6 decimals, 1e6).
// PRICES_JSON (env) can override any sku's price without a redeploy: {"kart.race-future":{"SKR":"5000000"}}.

import { baseUnits, type Rates } from './prices';

export type Currency = 'SOL' | 'SKR';
export const DECIMALS: Record<Currency, number> = { SOL: 9, SKR: 6 };

export type Powers = { speed: number; turbo: number; grip: number; armor: number; attack: number };
export type Sku = { sku: string; kind: 'kart' | 'paint' | 'bundle' | 'item'; name: string; blurb: string; usd: number; skrOnly?: boolean; grants?: string[]; until?: string;
                    powers?: Powers; item?: string; qty?: number };

/** A kart's price follows its powers: five of 0..5 pips, and the total lands on a rung. The phone carries the same
 *  table and rule (KartSetup.cs, KartPricing.cs), so a card reads right before the Worker has quoted it. */
export function usdForPowers(p: Powers): number {
  const total = p.speed + p.turbo + p.grip + p.armor + p.attack;
  return total <= 11 ? 0 : total <= 15 ? 2.99 : total <= 19 ? 4.99 : 7.99;
}
const kart = (id: string, name: string, blurb: string, speed: number, turbo: number, grip: number, armor: number, attack: number): Sku => {
  const powers = { speed, turbo, grip, armor, attack };
  return { sku: 'kart.' + id, kind: 'kart', name, blurb, powers, usd: usdForPowers(powers) };
};
const pack = (id: string, item: string, name: string, blurb: string, qty: number, usd: number): Sku =>
  ({ sku: `item.${id}${qty}`, kind: 'item', name, blurb, item, qty, usd });

// research/23: sell karts, paint and what you take into a race, never drivers (three are real people). Paint $0.99;
// karts by their powers, Street $2.99 and Hero $4.99; packs of three race items at $0.49; and two bundles: the
// Founder's Pack is the intended purchase, the Full Garage is the anchor that makes it look sane.
export const CATALOG: Sku[] = [
  { sku: 'paint.sunset',   kind: 'paint', name: 'Sunset',   blurb: 'Ember fading to gold.',                    usd: 0.99 },
  { sku: 'paint.midnight', kind: 'paint', name: 'Midnight', blurb: 'Deep navy, gloss black trim.',             usd: 0.99 },
  { sku: 'paint.rally',    kind: 'paint', name: 'SKR Rally', blurb: 'Seeker violet. Only ever sold for SKR.',  usd: 0.99, skrOnly: true },
  { sku: 'paint.white',    kind: 'paint', name: 'White',    blurb: 'Clean white. Shows every scratch.',       usd: 0.99 },

  // SPEED, TURBO, GRIP, ARMOR, ATTACK — Street karts total 14, Hero karts 18 (the free five total 10)
  kart('taxi',             'Mempool',       'Takes everyone for a ride.',            3, 3, 2, 3, 3),
  kart('van',              'Airdrop',       'Delivers. Eventually.',                 2, 3, 2, 4, 3),
  kart('hatchback-sports', 'Hot Hatch',     'Point it and it goes.',                 4, 3, 3, 2, 2),
  kart('police',           'Slasher',       'Enforces the racing line.',             3, 3, 3, 2, 3),
  kart('firetruck',        'Hotfix',        'A fire truck. In a kart race. Yes.',    3, 4, 3, 5, 3),
  kart('garbage-truck',    'Garbage Chain', 'Hauls the whole mempool.',              2, 3, 3, 5, 5),
  kart('tractor',          'Yield Farmer',  'Slow off the line, unstoppable after.', 2, 5, 3, 5, 3),
  kart('race-future',      'Photon',        'Low, wide, glued to the road.',         5, 4, 5, 2, 2),

  // packs: the item name is the ItemManager's, the count is credited to the phone's inventory when the purchase verifies
  pack('rocket',  'Rocket',      'Rockets x3',  'Straight, fast, no manners.',                3, 0.49),
  pack('homing',  'Homing Item', 'Homing x3',   'Finds the kart ahead and turns it round.',   3, 0.49),
  pack('bomb',    'Bomb',        'Bombs x3',    'Drops behind you. Waits.',                   3, 0.49),
  pack('nitro',   'Nitro',       'Nitro x3',    'A turbo with the lid off.',                  3, 0.49),
  pack('bouncer', 'Bouncer',     'Bouncers x3', 'Off the walls until it finds someone.',      3, 0.49),
  pack('sunbomb', 'Sun Bomb',    'Sun Bombs x3', 'Lob it, count to two, stand well back.',    3, 0.79),

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
