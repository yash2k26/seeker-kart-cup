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
            new("beach",    "Palm Bay",        "THE COAST AT NIGHT", "Sand causeways across the lagoon, the lighthouse sweeping the bay. Run wide and you swim.", 1),
            new("alpine",   "Frost Peak",      "ALPINE PASS",      "Switchbacks across a glacier lake. Brake early: the water is colder than it looks.", 2),
            new("forest",   "Pine Ridge",      "MOUNTAIN FOREST",  "Tight and technical on causeways through a flooded forest. Every corner hides the next one.", 3),
            new("speedway", "Sunset Speedway", "STADIUM CIRCUIT",  "The big oval, then down into the infield's hairpins. Walls both sides, nowhere to cut.", 2),
            new("volcano",  "Mount Ember",     "LAVA FIELDS",      "A basalt road across a lake of lava, round a smoking volcano. Miss a corner and you're toast.", 3),
        };

        public static int Count => All.Length;
        /// Laps a race runs (RaceManager.totalLaps, TrackSetup, worker/src/tracks.json): one since 10 Oct 2026, the user.
        /// A Tour event may ask for more (RaceMode.Laps).
        public const int Laps = 1;
        public static Def At(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];
        public static string SceneOf(int i) => "Race" + (Mathf.Clamp(i, 0, All.Length - 1) + 1);
        public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return 0; }

        // ---- personal bests, per track, on this phone (the Worker keeps the signed-in ones) ----
        /// "kc.best2.": the circuits were relaid longer on 11 Oct 2026 (1.0-1.4 km to 1.8-2.3 km), so a best from before
        /// could never be beaten again; the old keys are left where they are.
        internal static string BestKey(string id) => "kc.best2." + id;
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
