using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// Things to do (Beach Buggy has 48, each paying gems): counted on the phone as they happen. Done, one waits to be
    /// claimed on the achievements screen (12 Oct 2026: a reward paid silently is a reward nobody sees), where CLAIM hands
    /// over its chest and stars; the Awards buttons carry a count while any wait. Nothing expires and nothing asks for a
    /// streak; each pays once. A race shows a banner the moment one lands.
    public static class Achievements
    {
        public enum Stat { Races, Wins, Podiums, TracksWon, GrandPrixWon, FromBehind, Smashes, StartBoosts, ItemsUsed, DriftBoosts, Swims, ChestsOpened, DailyDone, TourStars, BossesBeaten }

        public sealed class Def
        {
            public string id, title, line; public Stat stat; public int goal; public Chests.Tier reward; public int stars;
            public Def(string id, string title, string line, Stat stat, int goal, Chests.Tier reward, int stars)
            { this.id = id; this.title = title; this.line = line; this.stat = stat; this.goal = goal; this.reward = reward; this.stars = stars; }
        }

        public static readonly Def[] All =
        {
            new("races-1", "Green Light", "Finish a race", Stat.Races, 1, Chests.Tier.Wood, 1),
            new("races-10", "Regular", "Finish 10 races", Stat.Races, 10, Chests.Tier.Silver, 2),
            new("races-50", "Lifer", "Finish 50 races", Stat.Races, 50, Chests.Tier.Gold, 3),
            new("wins-1", "Top Step", "Win a race", Stat.Wins, 1, Chests.Tier.Silver, 2),
            new("wins-10", "Habit", "Win 10 races", Stat.Wins, 10, Chests.Tier.Gold, 3),
            new("wins-25", "Dynasty", "Win 25 races", Stat.Wins, 25, Chests.Tier.Treasure, 5),
            new("podiums-10", "Paparazzi", "Finish on the podium 10 times", Stat.Podiums, 10, Chests.Tier.Silver, 2),
            new("tracks-4", "Tourist", "Win on all five circuits", Stat.TracksWon, 5, Chests.Tier.Gold, 3),   // (the id kept: done is done)
            new("gp-1", "Champion", "Win a Grand Prix", Stat.GrandPrixWon, 1, Chests.Tier.Gold, 3),
            new("behind-1", "From the Back", "Win a race you were last in", Stat.FromBehind, 1, Chests.Tier.Gold, 3),
            new("smash-25", "Bull in a Shop", "Smash 25 crates, pots or cones", Stat.Smashes, 25, Chests.Tier.Wood, 1),
            new("smash-150", "Demolition", "Smash 150 of them", Stat.Smashes, 150, Chests.Tier.Silver, 2),
            new("start-1", "Jump Start", "Get a start boost", Stat.StartBoosts, 1, Chests.Tier.Wood, 1),
            new("start-10", "Launch Control", "Get 10 start boosts", Stat.StartBoosts, 10, Chests.Tier.Silver, 2),
            new("items-25", "Armed", "Use 25 items", Stat.ItemsUsed, 25, Chests.Tier.Wood, 1),
            new("items-150", "Arsenal", "Use 150 items", Stat.ItemsUsed, 150, Chests.Tier.Silver, 2),
            new("drift-25", "Sideways", "Fire 25 drift boosts", Stat.DriftBoosts, 25, Chests.Tier.Wood, 1),
            new("drift-200", "Smoke Show", "Fire 200 drift boosts", Stat.DriftBoosts, 200, Chests.Tier.Gold, 3),
            new("swim-1", "Took a Swim", "Drive into deep water", Stat.Swims, 1, Chests.Tier.Wood, 1),
            new("chests-10", "Collector", "Open 10 chests", Stat.ChestsOpened, 10, Chests.Tier.Silver, 2),
            new("daily-5", "Daily Driver", "Meet 5 daily challenges", Stat.DailyDone, 5, Chests.Tier.Gold, 3),
            new("tour-21", "Pro", "Earn 21 Tour stars", Stat.TourStars, 21, Chests.Tier.Gold, 3),
            new("bosses-4", "Giant Killer", "Beat all five Tour bosses", Stat.BossesBeaten, 5, Chests.Tier.Treasure, 5),
            new("boss-1", "Up Close", "Beat a Tour boss in their duel", Stat.BossesBeaten, 1, Chests.Tier.Silver, 2),
        };

        [Serializable] sealed class State { public int[] counts = new int[Enum.GetValues(typeof(Stat)).Length]; public List<string> done = new(), tracksWon = new(), claimed = new(); public int version; }
        const string Key = "kc.ach";
        static State s;
        internal static void Reload() { s = null; Changed?.Invoke(); }   // CloudSave replaced it
        static State S
        {
            get
            {
                if (s != null) return s;
                try { s = JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key, "")); } catch { s = null; }
                s ??= new State();
                int n = Enum.GetValues(typeof(Stat)).Length;
                if (s.counts == null || s.counts.Length < n) { var c = new int[n]; if (s.counts != null) Array.Copy(s.counts, c, s.counts.Length); s.counts = c; }
                s.done ??= new(); s.tracksWon ??= new(); s.claimed ??= new();
                // every one done before claims existed was paid the moment it was done: claimed already
                if (s.version < 2) { foreach (var id in s.done) if (!s.claimed.Contains(id)) s.claimed.Add(id); s.version = 2; }
                return s;
            }
        }
        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(S)); PlayerPrefs.Save(); }

        /// One just done (waiting to be claimed): the race HUD shows a banner for it.
        public static event Action<Def> Unlocked;
        /// Anything changed (a count, one done, one claimed): the list and the Awards badges redraw.
        public static event Action Changed;

        public static int DoneCount => All.Count(d => S.done.Contains(d.id));
        public static bool IsDone(Def d) => S.done.Contains(d.id);
        public static bool IsClaimed(Def d) => S.claimed.Contains(d.id);
        public static bool CanClaim(Def d) => S.done.Contains(d.id) && !S.claimed.Contains(d.id);
        public static int ClaimableCount => All.Count(CanClaim);

        /// Hand over a done one's chest and stars, once. False if it is not done or was claimed already.
        public static bool Claim(Def d)
        {
            if (d == null || !CanClaim(d)) return false;
            S.claimed.Add(d.id);
            Chests.Give(d.reward);
            Chests.AddStars(d.stars);
            Save();
            KartApi.Event("achievement_claimed", new { id = d.id });
            Changed?.Invoke();
            return true;
        }
        public static int Progress(Def d) => Mathf.Min(S.counts[(int)d.stat], d.goal);

        public static void Add(Stat stat, int n = 1) { S.counts[(int)stat] += n; Check(stat); }
        /// For counts the game keeps itself (Tour stars, bosses beaten): never lower than it was.
        public static void Set(Stat stat, int value) { if (value <= S.counts[(int)stat]) return; S.counts[(int)stat] = value; Check(stat); }

        /// A race finished (solo): place, the circuit, and whether the player was last at some point.
        public static void Race(int place, string trackId, bool wasLast)
        {
            Add(Stat.Races);
            if (place >= 1 && place <= 3) Add(Stat.Podiums);
            if (place != 1) return;
            Add(Stat.Wins);
            if (wasLast) Add(Stat.FromBehind);
            if (!string.IsNullOrEmpty(trackId) && !S.tracksWon.Contains(trackId)) { S.tracksWon.Add(trackId); Set(Stat.TracksWon, S.tracksWon.Count); }
        }

        static void Check(Stat stat)
        {
            foreach (var d in All)
            {
                if (d.stat != stat || S.done.Contains(d.id) || S.counts[(int)stat] < d.goal) continue;
                S.done.Add(d.id);                 // paid when claimed (Claim)
                KartApi.Event("achievement", new { id = d.id });
                Unlocked?.Invoke(d);
            }
            Save();
            Changed?.Invoke();
        }

#if UNITY_EDITOR
        /// Previews: some done, some part-way.
        public static void Preview()
        {
            s = new State { version = 2 };
            s.counts[(int)Stat.Races] = 12; s.counts[(int)Stat.Wins] = 3; s.counts[(int)Stat.Podiums] = 7; s.counts[(int)Stat.Smashes] = 31;
            s.counts[(int)Stat.DriftBoosts] = 64; s.counts[(int)Stat.StartBoosts] = 2; s.counts[(int)Stat.ItemsUsed] = 40;
            s.done.AddRange(new[] { "races-1", "races-10", "wins-1", "smash-25", "start-1", "items-25", "drift-25" });
            s.claimed.AddRange(new[] { "races-1", "races-10", "smash-25", "start-1", "items-25" });   // two waiting to be claimed
        }
#endif
    }
}
