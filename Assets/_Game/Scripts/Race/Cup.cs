using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// The cup IS the game. A list of tracks, points carried between them, a champion at the end. Without it every
    /// race is an exhibition that changes nothing, and the player has no reason to run a second one. A single chosen
    /// track is just a one-race cup, so the results screen and the resume logic never need a second code path.
    ///
    /// Two rules give the standings teeth:
    ///   - points 10/8/6/4/3/2 for positions 1-6, so a bad race costs you but never ends the cup;
    ///   - the grid for every race after the first is the standings REVERSED, so leading means starting at the back.
    /// The second rule is what stops one good race from deciding the whole cup.
    public static class Cup
    {
        public const int Racers = 6;
        public static readonly int[] PointsFor = { 10, 8, 6, 4, 3, 2 };

        public static bool Active { get; private set; }
        public static int RaceIndex { get; private set; }          // 0-based: the race about to be driven
        public static int PlayerCharacter { get; private set; }

        static readonly Dictionary<int, int> points = new();       // character index -> points so far
        static readonly List<int> grid = new();                    // character indices, in starting order
        static readonly List<int> tracks = new();                  // indices into Tracks.All, in running order

        public static int Races => tracks.Count;
        public static bool IsSingleRace => Races == 1;
        public static bool IsFinalRace => RaceIndex >= Races - 1;
        public static int RaceNumber => RaceIndex + 1;
        public static int TrackIndex => tracks.Count == 0 ? 0 : tracks[Mathf.Clamp(RaceIndex, 0, tracks.Count - 1)];
        public static string TrackId => Tracks.At(TrackIndex).id;
        public static string TrackName => Tracks.At(TrackIndex).name;
        public static string SceneName => Tracks.SceneOf(TrackIndex);
        public static IReadOnlyList<int> Grid => grid;
        public static IReadOnlyList<int> TrackList => tracks;

        /// Start a fresh cup over every track, with the chosen character on pole.
        public static void Begin(int playerCharacter, int rosterSize) => Begin(playerCharacter, rosterSize, Enumerable.Range(0, Tracks.Count).ToList());

        /// Start a fresh cup over `trackList` (indices into Tracks.All). Copied before clearing: a caller restarting
        /// "the same cup" passes TrackList itself, which IS the list being cleared.
        public static void Begin(int playerCharacter, int rosterSize, IReadOnlyList<int> trackList)
        {
            var wanted = trackList.ToList();
            Active = true;
            RaceIndex = 0;
            PlayerCharacter = ((playerCharacter % rosterSize) + rosterSize) % rosterSize;   // on the roster, or the grid would leave the player out
            playerCharacter = PlayerCharacter;
            tracks.Clear(); tracks.AddRange(wanted);
            points.Clear();
            grid.Clear();
            for (int i = 0; i < Racers; i++)
            {
                int c = ((playerCharacter + i) % rosterSize + rosterSize) % rosterSize;
                grid.Add(c);
                points[c] = 0;
            }
            Save();
        }

        /// Called once when the player's race ends. `order` is character indices, finishing order first to last.
        public static void Record(IReadOnlyList<int> order)
        {
            if (!Active) return;
            for (int i = 0; i < order.Count; i++)
            {
                int c = order[i];
                if (!points.ContainsKey(c)) points[c] = 0;
                points[c] += i < PointsFor.Length ? PointsFor[i] : 0;
            }
            Save();
        }

        /// Move to the next race and rebuild the grid from the standings, leader at the back.
        public static void Advance()
        {
            if (!Active || IsFinalRace) return;
            RaceIndex++;
            var order = Standings.Select(s => s.character).ToList();
            order.Reverse();
            grid.Clear(); grid.AddRange(order);
            Save();
        }

        public static void End() { Active = false; PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        /// The last race has been scored. The standings stay on screen, but the cup is no longer resumable, so
        /// killing the app here offers a fresh cup rather than replaying the final race for ever.
        public static void MarkFinished()
        {
            if (!IsSingleRace && PlayerPlace == 1) Achievements.Add(Achievements.Stat.GrandPrixWon);
            PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        }

        public static int PointsOf(int character) => points.TryGetValue(character, out var p) ? p : 0;

        /// Best first. Ties break toward the racer who is further up the current grid, so the table never flickers.
        public static List<(int character, int points)> Standings =>
            points.Select(kv => (character: kv.Key, points: kv.Value))
                  .OrderByDescending(x => x.points)
                  .ThenBy(x => grid.IndexOf(x.character))
                  .ToList();

        public static int PlayerPlace
        {
            get
            {
                var s = Standings;
                for (int i = 0; i < s.Count; i++) if (s[i].character == PlayerCharacter) return i + 1;
                return 0;
            }
        }

        // ---- persistence: a cup survives the app being killed, which is the point of having one ----
        const string Key = "kc.cup";
        const string Version = "3";   // v1 had no track list; v2's character indices were into the roster before Runa and
                                      // Vee left (6 Oct). An older string restores as "no cup" rather than the wrong drivers

        static void Save()
        {
            // version|raceIndex|player|char:points,char:points|gridA,gridB,...|trackA,trackB,...
            var p = string.Join(",", points.Select(kv => $"{kv.Key}:{kv.Value}"));
            PlayerPrefs.SetString(Key, $"{Version}|{RaceIndex}|{PlayerCharacter}|{p}|{string.Join(",", grid)}|{string.Join(",", tracks)}");
            PlayerPrefs.Save();
        }

        /// Called once at boot. A half-finished cup is restored so "continue" means something.
        public static void Restore()
        {
            var raw = PlayerPrefs.GetString(Key, "");
            var parts = raw.Split('|');
            if (parts.Length != 6 || parts[0] != Version) { Active = false; return; }
            try
            {
                RaceIndex = int.Parse(parts[1]);
                PlayerCharacter = int.Parse(parts[2]);
                points.Clear();
                foreach (var pair in parts[3].Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split(':');
                    points[int.Parse(kv[0])] = int.Parse(kv[1]);
                }
                grid.Clear();
                grid.AddRange(parts[4].Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
                tracks.Clear();
                tracks.AddRange(parts[5].Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
                Active = grid.Count > 0 && tracks.Count > 0 && RaceIndex < Races;
            }
            catch (System.Exception e) { Debug.LogWarning("[cup] could not restore: " + e.Message); Active = false; }
        }
    }
}
