using System.Linq;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// Pulls the track's fastest times from the Worker and prints them. Offline is a normal state, not an error.
    public sealed class LeaderboardFeed : MonoBehaviour
    {
        [SerializeField] TMP_Text rows;
        [SerializeField] string track = "beach";

        async void OnEnable()
        {
            if (rows == null) return;
            Centre(true);
            rows.text = "loading…";
            try
            {
                var board = await KartApi.Leaderboard(track);
                bool empty = board == null || board.entries.Count == 0;
                Centre(empty);                      // a one-line empty state orphaned in the top-left of a big card reads as a bug
                rows.text = empty
                    ? "No times yet.\nBe the first."
                    : string.Join("\n", board.entries.Take(10).Select(e =>
                        $"{e.rank}.<pos=12%>{Fit(e.name)}{(e.isSeeker ? " *" : "")}<pos=76%>{RaceHud.Clock(e.bestMs / 1000f)}"));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[board] " + e.Message);
                Centre(true);
                rows.text = "Can't reach the leaderboard.\nCheck your connection and reopen.";
            }
        }

        /// Loading, empty and error are one short line in a tall card, so they centre; a real table starts top-left.
        void Centre(bool on) => rows.alignment = on ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;

        /// <pos> sets an absolute pen position rather than a column width, so an over-long name would render
        /// straight through the time beside it. Clip it instead.
        static string Fit(string name) => string.IsNullOrEmpty(name) || name.Length <= 18 ? name : name[..17] + "…";
    }
}
