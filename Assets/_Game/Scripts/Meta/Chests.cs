using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KartCup
{
    /// Treasure chests: a podium finish in a solo race or a free room earns one, its tier rolled the moment it is
    /// earned and shown before it opens. They open at once: no slots to fill and no clocks to wait on (Clash Royale
    /// dropped both in 2025: full slots wasted wins and timers asked for logins on a schedule). Stars (3/2/1 for the
    /// podium) fill a meter that never empties on its own; full, it gives a Golden Treasure Chest. Kartel Weekly adds
    /// a silver chest for everyone who drove and a golden one for its prize places.
    /// Never sold, never sped up, never from a paid room, and only game stuff inside (race item packs, a test drive in
    /// a kart you don't own, a paint): a chance prize with money in it is gambling in places where a free one is not,
    /// so SKR is only ever the Weekly's fixed skill prize. The drop rates below are the ones the chest screen prints.
    public static class Chests
    {
        public enum Tier { Wood, Silver, Gold, Treasure }
        [Serializable] public sealed class Reward { public string kind, id, label; public int qty; public Tier rarity; }
        [Serializable] sealed class State { public List<Tier> pending = new(); public int stars, fills, sinceGold; public int claimedWeek = -1; }

        public static readonly string[] Names = { "WOOD CHEST", "SILVER CHEST", "GOLD CHEST", "GOLDEN TREASURE CHEST" };
        /// % per tier (wood, silver, gold, golden) for 1st, 2nd, 3rd.
        public static readonly int[][] Odds = { new[] { 49, 32, 18, 1 }, new[] { 65, 27, 8, 0 }, new[] { 80, 17, 3, 0 } };
        public static readonly int[] StarsFor = { 3, 2, 1 };
        /// The 8th chest in a row without a gold (or better) is a gold.
        public const int GoldPity = 8;
        /// The first fill of the star meter is short, so a new player sees a golden chest on day one.
        public static int StarGoal => S.fills == 0 ? 15 : 30;

        public static event Action Changed;
        /// The garage's karts (set by whoever opens chests: ChestReveal), for the test drives.
        public static KartDatabase Karts;

        const string Key = "kc.chests";
        static State state;
        static State S
        {
            get
            {
                if (state != null) return state;
                try { state = JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key, "")); } catch { state = null; }
                return state ??= new State();
            }
        }
        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(S)); PlayerPrefs.Save(); Changed?.Invoke(); }

        public static int Pending => S.pending.Count;
        public static Tier? Next => S.pending.Count > 0 ? S.pending[0] : null;
        public static int Stars => S.stars;
        /// What the last race earned (the results card's ChestBadge reads it).
        public static (Tier? tier, int stars, bool filled) LastEarn { get; private set; }
        /// A new race: nothing earned in it yet (RaceSpawner).
        public static void ClearLast() => LastEarn = (null, 0, false);

        /// A race is over (solo, or a free room; never a paid one): a podium place earns a chest and stars.
        /// Returns the chest's tier (null off the podium) and whether the stars just filled the meter.
        public static (Tier? tier, int stars, bool filled) Earn(int place)
        {
            if (place < 1 || place > 3) return LastEarn = (null, 0, false);
            var odds = Odds[place - 1];
            int roll = Random.Range(0, 100), acc = 0; var tier = Tier.Wood;
            for (int i = 0; i < odds.Length; i++) { acc += odds[i]; if (roll < acc) { tier = (Tier)i; break; } }
            if (S.sinceGold + 1 >= GoldPity && tier < Tier.Gold) tier = Tier.Gold;
            S.sinceGold = tier >= Tier.Gold ? 0 : S.sinceGold + 1;
            S.pending.Add(tier);
            int stars = StarsFor[place - 1];
            S.stars += stars;
            bool filled = false;
            if (S.stars >= StarGoal) { S.stars -= StarGoal; S.fills++; S.pending.Add(Tier.Treasure); filled = true; }
            Save();
            KartApi.Event("chest_earn", new { tier = tier.ToString(), place, filled });
            return LastEarn = (tier, stars, filled);
        }

        /// A chest from outside a podium (a Tour boss, the Daily Challenge): waiting to be opened.
        public static void Give(Tier tier) { S.pending.Add(tier); Save(); }

        /// Stars from outside a podium (the Daily Challenge): into the meter, a golden chest if it fills.
        public static void AddStars(int n)
        {
            S.stars += n;
            if (S.stars >= StarGoal) { S.stars -= StarGoal; S.fills++; S.pending.Add(Tier.Treasure); }
            Save();
        }

        /// Kartel Weekly's Monday claim, once per week: a silver chest for driving it, a golden one for a prize place.
        public static bool ClaimWeekly(int week, bool prizePlace)
        {
            if (week <= S.claimedWeek) return false;
            S.claimedWeek = week;
            S.pending.Add(Tier.Silver);
            if (prizePlace) S.pending.Add(Tier.Treasure);
            Save();
            return true;
        }
        public static bool Claimed(int week) => week <= S.claimedWeek;

        /// Open the next chest: what is inside is handed over now.
        public static (Tier tier, List<Reward> rewards)? Open()
        {
            if (S.pending.Count == 0) return null;
            var tier = S.pending[0];
            S.pending.RemoveAt(0);
            var rewards = Roll(tier);
            Achievements.Add(Achievements.Stat.ChestsOpened);
            foreach (var r in rewards) Apply(r);
            Save();
            KartApi.Event("chest_open", new { tier = tier.ToString(), rewards = rewards.Select(r => $"{r.kind}:{r.id}x{r.qty}").ToArray() });
            return (tier, rewards);
        }

        // ---------- what is inside ----------
        /// Items by weight: nitro, rockets, bouncers common; homing and bombs less so; sun bombs rare.
        static readonly (string id, int weight)[] ItemWeights = { ("nitro", 6), ("rocket", 6), ("bouncer", 5), ("homing", 3), ("bomb", 3), ("sunbomb", 1) };

        /// wood: 2 items · silver: 4 items, a test drive 1 in 4 · gold: 6 items with a sun bomb, a test drive, a paint
        /// 3 in 10 · golden: 10 items, 2 test drives, a paint you don't have (2 more test drives once you have them all).
        static List<Reward> Roll(Tier t)
        {
            var list = new List<Reward>();
            switch (t)
            {
                case Tier.Wood: Items(list, 2, t); break;
                case Tier.Silver: Items(list, 4, t); if (Random.value < 0.25f) Add(list, TestDrive(t)); break;
                case Tier.Gold:
                    Items(list, 5, t); list.Add(Item("sunbomb", 1, t));
                    Add(list, TestDrive(t));
                    if (Random.value < 0.3f) Add(list, Paint(t));
                    break;
                default:
                    Items(list, 10, t);
                    Add(list, TestDrive(t)); Add(list, TestDrive(t));
                    var paint = Paint(t);
                    if (paint != null) list.Add(paint); else { Add(list, TestDrive(t)); Add(list, TestDrive(t)); }
                    break;
            }
            // gems in every chest (9 Oct 2026): the currency that recruits the judges
            var (lo, hi) = t switch { Tier.Wood => (5, 10), Tier.Silver => (15, 25), Tier.Gold => (40, 60), _ => (80, 120) };
            int gems = Random.Range(lo, hi + 1);
            list.Add(new Reward { kind = "gems", id = "gems", qty = gems, rarity = t, label = $"+{gems} GEMS" });
            // the same thing twice reads as one bigger pile; the best is revealed last
            return list.GroupBy(r => r.kind + r.id).Select(g => { var r = g.First(); r.qty = g.Sum(x => x.qty); r.label = Label(r); return r; })
                       .OrderBy(r => r.kind == "gems" ? 3 : r.kind == "paint" ? 2 : r.kind == "trial" ? 1 : 0).ToList();
        }

        static void Add(List<Reward> list, Reward r) { if (r != null) list.Add(r); }

        static void Items(List<Reward> list, int n, Tier t)
        {
            int total = ItemWeights.Sum(w => w.weight);
            for (int k = 0; k < n; k++)
            {
                int roll = Random.Range(0, total), acc = 0;
                foreach (var (id, weight) in ItemWeights) { acc += weight; if (roll < acc) { list.Add(Item(id, 1, t)); break; } }
            }
        }

        static Reward Item(string id, int qty, Tier t) { var r = new Reward { kind = "item", id = id, qty = qty, rarity = t }; r.label = Label(r); return r; }

        /// One more race in a kart you don't own (its free trial handed back). Null when you own every kart.
        static Reward TestDrive(Tier t)
        {
            var db = Karts;
            var karts = db != null ? db.karts.Where(k => k != null && !KartDatabase.Owned(k)).ToList() : new List<KartDef>();
            if (karts.Count == 0) return null;
            var k = karts[Random.Range(0, karts.Count)];
            return new Reward { kind = "trial", id = k.id, qty = 1, rarity = t, label = $"TEST DRIVE · {k.displayName.ToUpperInvariant()}" };
        }

        /// A paint you don't have yet, never one sold only for SKR. Null when there is none left to give.
        static Reward Paint(Tier t)
        {
            var left = Paints.All.Where(p => !p.skrOnly && !Unlocks.Has(p.sku)).ToList();
            if (left.Count == 0) return null;
            var p = left[Random.Range(0, left.Count)];
            return new Reward { kind = "paint", id = p.id, qty = 1, rarity = t, label = $"{p.name.ToUpperInvariant()} PAINT" };
        }

        static string Label(Reward r) => r.kind == "item" ? $"{Inventory.Find(r.id)?.item.ToUpperInvariant() ?? r.id.ToUpperInvariant()}  ×{r.qty}" : r.label;

        static void Apply(Reward r)
        {
            switch (r.kind)
            {
                case "item": Inventory.Add(r.id, r.qty); break;
                case "gems": Gems.Earn(r.qty, "chest"); break;
                case "trial": KartDatabase.GrantTrial(r.id); break;
                case "paint": if (Paints.Find(r.id) is { } p) Unlocks.Earn(p.sku); break;
            }
        }

#if UNITY_EDITOR
        /// MenuPreview / tests: a known state.
        public static void Preview(int stars, params Tier[] pending) { state = new State { stars = stars, fills = 1 }; state.pending.AddRange(pending); }
        public static List<Reward> PreviewRoll(Tier t) => Roll(t);
#endif
    }
}
