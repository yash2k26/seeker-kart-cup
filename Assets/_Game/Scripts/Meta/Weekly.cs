using System;
using System.Collections.Generic;
using UnityEngine;

namespace KartCup
{
    /// Kartel Weekly on the phone: this week's board from the Worker (GET /v1/weekly, weekly.ts), kept for the menus,
    /// and the time trial's flag. The time trial is the Weekly's only way in: this week's circuit, nobody else on
    /// track, no items, no turbo pads or oil, and no bought kart powers (RaceSpawner), so a prize is never bought.
    public static class Weekly
    {
        [Serializable] public class Row { public int rank, bestMs, gapMs, entrants; public string wallet, name; public bool isSeeker; public int? prizePlace, prize; }
        [Serializable] public class Last { public int week, seekers; public string track; public List<Row> winners = new(); public Row me; }
        [Serializable] public class Board
        {
            public int week, minSeekers, seekers, entrants; public int? pot;
            public string track, nextTrack, currency = "SKR";
            public long startsAt, endsAt, now;
            public List<int> split = new();
            public List<Row> entries = new();
            public Row me; public Last last;
        }

        /// The Worker's rules page (worker/public/weekly-rules.html), one tap from the Weekly screen.
        public static string RulesUrl => KartApi.BaseUrl + "/weekly-rules.html";

        public static Board Current { get; private set; }
        public static event Action Changed;
        static float fetchedAt = -999f;
        static long skew;            // the Worker's clock minus ours, so the countdown is the Worker's

        /// The board, at most once a minute unless `force`. Null when the Worker can't be reached (the menus hide the
        /// Weekly rather than show a stale or empty one).
        public static async Awaitable<Board> Fetch(bool force = false)
        {
            if (!force && Current != null && Time.realtimeSinceStartup - fetchedAt < 60f) return Current;
            try
            {
                var wallet = WalletFlow.Instance != null && WalletFlow.Instance.Profile != null ? WalletFlow.Instance.Profile.wallet : null;
                var b = await KartApi.WeeklyBoard(wallet);
                if (b == null || string.IsNullOrEmpty(b.track)) return Current;
                skew = b.now - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Current = b; fetchedAt = Time.realtimeSinceStartup;
                Changed?.Invoke();
            }
            catch (Exception e) { Debug.Log("[weekly] " + e.Message); }
            return Current;
        }

        public static int TrackIndex => Current == null ? -1 : Tracks.IndexOf(Current.track);
        public static long SecondsLeft => Current == null ? 0 : Math.Max(0, Current.endsAt - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + skew));

        /// "4d 12h", "5h 20m", "12m".
        public static string Countdown(long s) => s >= 86400 ? $"{s / 86400}d {s % 86400 / 3600}h" : s >= 3600 ? $"{s / 3600}h {s % 3600 / 60}m" : $"{Math.Max(1, s / 60)}m";

        /// "1,000 SKR" for a place (1-3), or null while the pot is unset or the week has too few Seekers.
        public static string Prize(int place)
        {
            var b = Current;
            if (b?.pot == null || place < 1 || place > b.split.Count) return null;
            return $"{b.pot.Value * b.split[place - 1] / 100:N0} {b.currency}";
        }

        /// The time trial: set by the menu (ScreenFlow.StartWeekly), read on the grid and by the results card; the menu
        /// clears it when the title shows again.
        public static bool Racing;

#if UNITY_EDITOR
        /// MenuPreview: a known board.
        public static void Preview(Board b) { Current = b; skew = b.now - DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
#endif
    }
}
