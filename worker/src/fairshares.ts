// Fair shares, as the escrow program divides a finished room (escrow/programs/kartel_escrow/src/sidepot.rs; read
// its header for the rules). This copy only previews: what a player can win, shown in the lobby. What the chain pays
// is the program's own arithmetic. Both are checked against escrow/vectors/fair-shares.json (npm run test:escrow).

export const MAX_SEATS = 6;
const BPS = 10_000n;
const U64_MAX = (1n << 64n) - 1n;

export class SplitError extends Error {
  constructor(readonly kind: 'seats' | 'ranking' | 'cut' | 'overflow') { super(`fair shares: ${kind}`); }
}

/** Percent of a contested layer's net each place among its contributors takes, best first. */
export function places(contributors: number): bigint[] {
  return contributors <= 1 ? [] : contributors === 2 ? [100n] : [50n, 30n, 20n];
}

/** `rank[i]` is seat i's finishing place (0 = the winner), a permutation of the seats. */
export function split(stakes: bigint[], rank: number[], cutBps: number): { payouts: bigint[]; house: bigint } {
  const n = stakes.length;
  if (n < 2 || n > MAX_SEATS || rank.length !== n) throw new SplitError('seats');
  if (!Number.isInteger(cutBps) || cutBps < 0 || cutBps > 10_000) throw new SplitError('cut');
  if (stakes.reduce((a, b) => a + b, 0n) > U64_MAX) throw new SplitError('overflow');
  const order: number[] = new Array(n).fill(-1);
  rank.forEach((place, seat) => {
    if (!Number.isInteger(place) || place < 0 || place >= n || order[place] !== -1) throw new SplitError('ranking');
    order[place] = seat;
  });

  const payouts = stakes.map(() => 0n);
  let house = 0n, floor = 0n;
  for (;;) {
    const above = stakes.filter(s => s > floor);
    if (!above.length) break;
    const level = above.reduce((a, b) => (b < a ? b : a));
    const layer = order.filter(seat => stakes[seat] >= level);
    const pot = (level - floor) * BigInt(layer.length);
    if (layer.length === 1) payouts[layer[0]] += pot;
    else {
      const cut = pot * BigInt(cutBps) / BPS, net = pot - cut;
      house += cut;
      let given = 0n;
      places(layer.length).forEach((percent, k) => { const share = net * percent / 100n; payouts[layer[k]] += share; given += share; });
      payouts[layer[0]] += net - given;
    }
    floor = level;
  }
  return { payouts, house };
}

/** What `seat` takes if it wins. The same whatever the order behind the winner: the winner is the best of every layer
 *  it paid into, and takes that layer's top share. With fewer than two stakes there is no race to win. */
export function ifWin(stakes: bigint[], seat: number, cutBps: number): bigint {
  if (stakes.length < 2) return 0n;
  const rank = stakes.map((_, i) => (i === seat ? 0 : i < seat ? i + 1 : i));
  return split(stakes, rank, cutBps).payouts[seat];
}
