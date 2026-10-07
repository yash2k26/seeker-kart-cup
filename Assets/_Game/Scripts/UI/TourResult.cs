using TMPro;
using UnityEngine;

namespace KartCup
{
    /// On the results card after a Tour event or the Daily Challenge: the stars it gave (and if they were new), a cup it
    /// opened, a boss's prize; or the daily goal met and paid, or still to meet.
    public sealed class TourResult : MonoBehaviour
    {
        [SerializeField] TMP_Text line;

        void OnEnable()
        {
            if (line == null) return;
            bool show = RaceMode.InEvent && !Weekly.Racing;
            line.gameObject.SetActive(show);
            if (!show) return;
            if (RaceMode.EventId == "daily")
            {
                line.text = Daily.LastPaid ? "DAILY CHALLENGE DONE  ·  + SILVER CHEST  ·  + 3 STARS"
                    : Daily.Done ? "Daily challenge already done today" : $"Daily goal: {Daily.Current.goal.ToLowerInvariant()}. Try again!";
                return;
            }
            var r = Tour.Last;
            if (r == null) { line.text = ""; return; }
            line.text = r.prize ?? r.unlocked ?? (r.stars == 0 ? "No stars this time: a podium earns them"
                : r.gained > 0 ? $"{r.title.ToUpperInvariant()}  ·  {r.stars} STAR{(r.stars > 1 ? "S" : "")}  ·  +{r.gained} NEW"
                : $"{r.title.ToUpperInvariant()}  ·  {r.stars} STAR{(r.stars > 1 ? "S" : "")}  ·  your best stands");
        }
    }
}
