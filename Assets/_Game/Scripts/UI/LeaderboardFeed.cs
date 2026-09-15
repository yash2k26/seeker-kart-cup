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
            rows.text = "loading…";
            try
            {
                var board = await KartApi.Leaderboard(track);
                rows.text = board == null || board.entries.Count == 0
                    ? "no times yet — be the first"
                    : string.Join("\n", board.entries.Take(10).Select(e =>
                        $"{e.rank,2}.  {e.name,-16}{(e.isSeeker ? " *" : "  ")}  {RaceHud.Clock(e.bestMs / 1000f)}"));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[board] " + e.Message);
                rows.text = "offline";
            }
        }
    }
}
