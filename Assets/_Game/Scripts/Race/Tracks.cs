using UnityEngine;

namespace KartCup
{
    /// The track catalog: the one list the cup, the track select, the HUD and the generator all read. Index here is
    /// the scene number (Race1..RaceN) and the cup order; `id` is what the Worker and the leaderboard key on, so
    /// "beach" stays "beach" for the rows already in D1.
    public static class Tracks
    {
        public sealed class Def
        {
            public readonly string id, name, kicker, blurb;
            public readonly int difficulty;   // 1..3, shown as pips on the select card
            public Def(string id, string name, string kicker, string blurb, int difficulty)
            { this.id = id; this.name = name; this.kicker = kicker; this.blurb = blurb; this.difficulty = difficulty; }
        }

        public static readonly Def[] All =
        {
            new("beach",    "Palm Bay",        "TROPICAL ISLAND",  "Wide sweepers along the surf. Run wide onto the sand and pay for it.",            1),
            new("alpine",   "Frost Peak",      "ALPINE PASS",      "Hairpins up the pass. Brake early: the cabins are closer than they look.",        2),
            new("forest",   "Pine Ridge",      "MOUNTAIN FOREST",  "Tight and technical through the pines. Every corner hides the next one.",         3),
            new("speedway", "Sunset Speedway", "STADIUM CIRCUIT",  "Two long straights and a chicane under the floodlights. Pure top speed.",         2),
        };

        public static int Count => All.Length;
        public static Def At(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];
        public static string SceneOf(int i) => "Race" + (Mathf.Clamp(i, 0, All.Length - 1) + 1);
        public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return 0; }

        // ---- personal bests, per track, on this phone (the Worker keeps the signed-in ones) ----
        static string BestKey(string id) => "kc.best." + id;
        public static float Best(string id) => PlayerPrefs.GetFloat(BestKey(id), 0f);

        /// Returns true when `seconds` is a new best for the track.
        public static bool RecordBest(string id, float seconds)
        {
            float prev = Best(id);
            if (prev > 0f && seconds >= prev) return false;
            PlayerPrefs.SetFloat(BestKey(id), seconds); PlayerPrefs.Save();
            return true;
        }
    }
}
