using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace KartCup
{
    /// Fair shares, as the escrow program divides a finished room (escrow/programs/kartel_escrow/src/sidepot.rs has
    /// the rules: layers like poker side pots, each contested only by those who paid into it, 50/30/20 or winner takes
    /// all, the cut on contested money only). The phone uses this copy to preview: what a stake wins if it comes
    /// first, and what the podium pays before the chain has settled. What is paid is the program's own arithmetic;
    /// every copy is checked against escrow/vectors/fair-shares.json (Unity: EscrowCheck).
    public static class FairShares
    {
        public const int MaxSeats = 6;

        public static int[] Places(int contributors) => contributors <= 1 ? new int[0] : contributors == 2 ? new[] { 100 } : new[] { 50, 30, 20 };

        /// `rank[i]` is seat i's finishing place (0 = the winner), a permutation of the seats. Null when that is not a
        /// room the program would settle.
        public static (BigInteger[] payouts, BigInteger house)? Split(IList<BigInteger> stakes, IList<int> rank, int cutBps)
        {
            int n = stakes.Count;
            if (n < 2 || n > MaxSeats || rank.Count != n || cutBps < 0 || cutBps > 10_000) return null;
            var order = Enumerable.Repeat(-1, n).ToArray();
            for (int seat = 0; seat < n; seat++)
            {
                int place = rank[seat];
                if (place < 0 || place >= n || order[place] != -1) return null;
                order[place] = seat;
            }
            var payouts = new BigInteger[n];
            BigInteger house = 0, floor = 0;
            while (true)
            {
                var above = stakes.Where(s => s > floor).ToList();
                if (above.Count == 0) break;
                var level = above.Min();
                var layer = order.Where(seat => stakes[seat] >= level).ToList();
                var pot = (level - floor) * layer.Count;
                if (layer.Count == 1) payouts[layer[0]] += pot;
                else
                {
                    var cut = pot * cutBps / 10_000;
                    var net = pot - cut;
                    house += cut;
                    BigInteger given = 0;
                    var places = Places(layer.Count);
                    for (int k = 0; k < places.Length; k++) { var share = net * places[k] / 100; payouts[layer[k]] += share; given += share; }
                    payouts[layer[0]] += net - given;
                }
                floor = level;
            }
            return (payouts, house);
        }

        /// What `seat` takes if it wins: the same whatever the order behind it. Zero with fewer than two stakes.
        public static BigInteger IfWin(IList<BigInteger> stakes, int seat, int cutBps)
        {
            if (stakes.Count < 2) return 0;
            var rank = Enumerable.Range(0, stakes.Count).Select(i => i == seat ? 0 : i < seat ? i + 1 : i).ToList();
            return Split(stakes, rank, cutBps)?.payouts[seat] ?? 0;
        }
    }
}
