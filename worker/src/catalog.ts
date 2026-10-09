// What is for sale and how a prize pool is split. One file, so the numbers a judge or an accountant asks about
// live in one place. Prices are base units as strings (SOL: lamports, 1e9; SKR: 6 decimals, 1e6; ORE: 11 decimals, 1e11).
// PRICES_JSON (env) can override any sku's price without a redeploy: {"kart.race-future":{"SKR":"5000000"}}.

import { baseUnits, type Rates } from './prices';

export type Currency = 'SOL' | 'SKR' | 'ORE';
export const DECIMALS: Record<Currency, number> = { SOL: 9, SKR: 6, ORE: 11 };

export type Powers = { speed: number; turbo: number; grip: number; armor: number; attack: number };
export type Sku = { sku: string; kind: 'kart' | 'paint' | 'bundle' | 'item' | 'driver' | 'gems'; name: string; blurb: string; sol: number; skrOnly?: boolean; grants?: string[]; until?: string;
                    powers?: Powers; item?: string; qty?: number };

/** A kart's price follows its powers: five of 0..5 pips, and the total lands on a rung. The phone carries the same
 *  table and rule (KartSetup.cs, KartPricing.cs), so a card reads right before the Worker has quoted it. */
export function solForPowers(p: Powers): number {
  const total = p.speed + p.turbo + p.grip + p.armor + p.attack;
  return total <= 11 ? 0 : total <= 15 ? 0.05 : total <= 19 ? 0.08 : 0.12;
}
const kart = (id: string, name: string, blurb: string, speed: number, turbo: number, grip: number, armor: number, attack: number): Sku => {
  const powers = { speed, turbo, grip, armor, attack };
  return { sku: 'kart.' + id, kind: 'kart', name, blurb, powers, sol: solForPowers(powers) };
};
const pack = (id: string, item: string, name: string, blurb: string, qty: number, sol: number): Sku =>
  ({ sku: `item.${id}${qty}`, kind: 'item', name, blurb, item, qty, sol });
// The four judges (9 Oct 2026): locked drivers, each also earned free by beating them in their Kartel Tour boss duel
// (Tour.cs bossSku). The sku names must match TrackSetup.Roster's paid table.
const driver = (id: string, name: string, blurb: string, sol: number): Sku => ({ sku: 'driver.' + id, kind: 'driver', name, blurb, sol });
// Gems (9 Oct 2026): the premium currency that recruits the judges (Mert/Akshay 100, Chase 200, Toly 300); also earned
// from chests and podiums. The phone credits the pack once per verified payment (PendingPurchases).
const gems = (n: number, sol: number): Sku => ({ sku: `gems.${n}`, kind: 'gems', name: `${n} gems`, blurb: 'Recruit drivers.', qty: n, sol });

// research/23: sell karts, paint and what you take into a race, never drivers (three are real people). Every price
// is in SOL (1 Oct 2026: no dollars anywhere; SKR and ORE are derived from it in prices.ts). Karts by their powers: Street
// 0.05, Hero 0.08, Legend 0.12 (8 Oct, research/26: Beach Buggy's US/EU car prices and what Solana players pay for
// an item they cannot resell; were 0.10 / 0.15 / 0.25). Paint 0.03; packs of three race items 0.01; and two bundles:
// the Founder's Pack (0.32 of contents for 0.18) is the intended purchase, the Full Garage (0.64 for 0.35) the anchor.
export const CATALOG: Sku[] = [
  { sku: 'paint.sunset',   kind: 'paint', name: 'Sunset',   blurb: 'Ember fading to gold.',                    sol: 0.03 },
  { sku: 'paint.midnight', kind: 'paint', name: 'Midnight', blurb: 'Deep navy, gloss black trim.',             sol: 0.03 },
  { sku: 'paint.rally',    kind: 'paint', name: 'SKR Rally', blurb: 'Seeker violet. Only ever sold for SKR.',  sol: 0.03, skrOnly: true },
  { sku: 'paint.white',    kind: 'paint', name: 'White',    blurb: 'Clean white. Shows every scratch.',       sol: 0.03 },

  // SPEED, TURBO, GRIP, ARMOR, ATTACK — Street karts total 14, Hero karts 18 (the free five total 10)
  kart('taxi',             'Mempool',       'Takes everyone for a ride.',            3, 3, 2, 3, 3),
  kart('van',              'Airdrop',       'Delivers. Eventually.',                 2, 3, 2, 4, 3),
  kart('hatchback-sports', 'Hot Hatch',     'Point it and it goes.',                 4, 3, 3, 2, 2),
  kart('police',           'Slasher',       'Enforces the racing line.',             3, 3, 3, 2, 3),
  kart('firetruck',        'Hotfix',        'A fire truck. In a kart race. Yes.',    3, 4, 3, 5, 3),
  kart('garbage-truck',    'Garbage Chain', 'Hauls the whole mempool.',              2, 3, 3, 5, 5),
  kart('tractor',          'Yield Farmer',  'Slow off the line, unstoppable after.', 2, 5, 3, 5, 3),
  kart('race-future',      'Photon',        'Low, wide, glued to the road.',         5, 4, 5, 2, 2),

  driver('mert',   'Mert',   'Indexes every corner before he takes it. Or beat him in Cup 1.',             0.08),
  driver('akshay', 'Akshay', 'Top speed, turns like a ferry. Or beat him in Cup 2.',                       0.08),
  driver('chase',  'Chase',  'Knows every dev on the grid. Waves at all of them. Or beat him in Cup 3.',   0.08),
  driver('toly',   'Toly',   'Ships blocks faster than you can read them. Or beat him in Cup 4.',          0.12),
  gems(100, 0.012), gems(550, 0.05), gems(1200, 0.09),

  // packs: the item name is the ItemManager's, the count is credited to the phone's inventory when the purchase verifies
  pack('rocket',  'Rocket',      'Rockets x3',  'Straight, fast, no manners.',                3, 0.01),
  pack('homing',  'Homing Item', 'Homing x3',   'Finds the kart ahead and turns it round.',   3, 0.01),
  pack('bomb',    'Bomb',        'Bombs x3',    'Drops behind you. Waits.',                   3, 0.01),
  pack('nitro',   'Nitro',       'Nitro x3',    'A turbo with the lid off.',                  3, 0.01),
  pack('bouncer', 'Bouncer',     'Bouncers x3', 'Off the walls until it finds someone.',      3, 0.01),
  pack('sunbomb', 'Sun Bomb',    'Sun Bombs x3', 'Lob it, count to two, stand well back.',    3, 0.015),

  { sku: 'bundle.founder', kind: 'bundle', name: "Founder's Pack", blurb: 'Photon, Hotfix, Mempool, Slasher, two paints and the Founder badge. Sold once, never again.',
    sol: 0.18, until: '2027-01-10T00:00:00Z', grants: ['kart.race-future', 'kart.firetruck', 'kart.taxi', 'kart.police', 'paint.sunset', 'paint.midnight', 'badge.founder'] },
  { sku: 'bundle.garage',  kind: 'bundle', name: 'Full Garage', blurb: 'Every kart and paint, including the ones we add this season, plus the Founder badge.',
    sol: 0.35, grants: ['kart.taxi', 'kart.van', 'kart.hatchback-sports', 'kart.police', 'kart.firetruck', 'kart.garbage-truck', 'kart.tractor', 'kart.race-future',
                         'paint.sunset', 'paint.midnight', 'paint.rally', 'paint.white', 'badge.founder'] },
];

/** A price quote for the client: today's base units per currency (env PRICES_JSON overrides base units by sku). */
export function quote(item: Sku, r: Rates, pricesJson?: string): Record<Currency, string> {
  let over: Record<string, Partial<Record<Currency, string>>> = {};
  try { over = pricesJson ? JSON.parse(pricesJson) : {}; } catch { /* keep computed */ }
  const q: Record<Currency, string> = { SKR: baseUnits(item.sol, 'SKR', r).toString(), SOL: baseUnits(item.sol, 'SOL', r).toString(), ORE: baseUnits(item.sol, 'ORE', r).toString() };
  if (item.skrOnly) q.SOL = q.ORE = '';
  const o = over[item.sku];
  if (o && typeof o === 'object') for (const k of ['SKR', 'SOL', 'ORE'] as Currency[]) if (o[k]) q[k] = o[k]!;
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

// ---- paid rooms ----

// Stakes, in base units. The host picks a room's minimum from these and each player stakes any of them from there
// up (the escrow program itself takes any amount between the room's minimum and maximum). A whitelist, not a free
// field: it keeps the lobby's chips in step and caps what a first-time player can put on the line.
export const STAKE_TIERS: Record<Currency, string[]> = {
  SOL: ['10000000', '50000000', '100000000', '150000000', '500000000'],   // 0.01, 0.05, 0.1, 0.15, 0.5 SOL
  SKR: ['5000000', '25000000', '100000000', '500000000'],                  // 5, 25, 100, 500 SKR
  ORE: [],   // ponytail: ORE buys from the shop only; no room stakes it (rooms.ts coerces a room to SOL/SKR)
};

// Every stake is a whole number of these (the program checks it). With 0.01 SOL the smallest possible SOL payout is a
// few thousandths of a SOL, well over the rent minimum an empty wallet needs before it can receive anything.
export const STAKE_UNIT: Record<Currency, string> = { SOL: '10000000', SKR: '5000000', ORE: '100000000000' };   // ORE: unreachable, no tiers

export const MIN_PAID_PLAYERS = 2;

export function formatAmount(base: bigint | string, currency: Currency): string {
  const b = BigInt(base);
  const d = DECIMALS[currency];
  const whole = b / 10n ** BigInt(d);
  const frac = (b % 10n ** BigInt(d)).toString().padStart(d, '0').replace(/0+$/, '');
  return `${whole}${frac ? '.' + frac : ''} ${currency}`;
}
