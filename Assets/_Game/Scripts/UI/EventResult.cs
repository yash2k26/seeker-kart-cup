using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The results card after a series race (Beach Buggy's): GREAT JOB or TRY AGAIN, the circuit, the stars this run
    /// earned, what you got against the goal, the reward, your time against the best, PLAY AGAIN and CONTINUE. Every other
    /// race keeps the standings table; this only takes over for a Tour event.
    public sealed class EventResult : MonoBehaviour
    {
        [SerializeField] GameObject layout;
        [SerializeField] TMP_Text headline, table, status, primaryLabel, secondaryLabel;
        [SerializeField] Image picture;
        [SerializeField] TMP_Text trackName, eventTitle, gotValue, goalValue, reward, yourTime, best, rule;
        [SerializeField] Image[] stars = System.Array.Empty<Image>();
        [SerializeField] Sprite[] trackArt = System.Array.Empty<Sprite>();
        [SerializeField] Color starOn = new(1f, 0.69f, 0.15f), starOff = new(1f, 1f, 1f, 0.16f);

        void OnEnable()
        {
            var r = Tour.Last;
            var (cup, e) = r != null ? Tour.Find(r.id) : (null, null);
            bool tour = RaceMode.InEvent && RaceMode.EventId != "daily" && !Weekly.Racing && r != null && e != null;
            if (layout != null) layout.SetActive(tour);
            if (table != null) table.gameObject.SetActive(!tour);
            if (status != null) status.gameObject.SetActive(!tour);
            if (!tour) return;
            var track = Tracks.At(e.track);
            bool duel = e.mode == RaceMode.Kind.Duel;
            if (headline != null) headline.text = r.stars > 0 ? "GREAT JOB!" : "TRY AGAIN";
            if (picture != null) { picture.sprite = e.track < trackArt.Length ? trackArt[e.track] : null; picture.enabled = picture.sprite != null; }
            if (trackName != null) trackName.text = track.name.ToUpperInvariant();
            if (eventTitle != null) eventTitle.text = e.title.ToUpperInvariant();
            if (gotValue != null) gotValue.text = duel ? (r.place == 1 ? "WON" : "LOST") : RaceHud.Ordinal(r.place).ToUpperInvariant();
            if (goalValue != null) goalValue.text = Tour.Goal(e);
            if (reward != null) reward.text = Gems.LastPodium > 0 ? $"+{Gems.LastPodium} GEMS" : r.stars > 0 ? $"+{r.stars} STARS" : "NO REWARD";
            if (yourTime != null) yourTime.text = r.time > 0f ? RaceHud.Clock(r.time) : "--:--";
            float b = Tracks.Best(track.id);
            if (best != null) best.text = b > 0f ? RaceHud.Clock(b) : "--:--";
            if (rule != null) rule.text = Tour.Rule(e, null);
            for (int k = 0; k < stars.Length; k++) if (stars[k] != null) stars[k].color = k < r.stars ? starOn : starOff;
            if (primaryLabel != null) primaryLabel.text = "PLAY AGAIN";
            if (secondaryLabel != null) secondaryLabel.text = "CONTINUE";
        }
    }
}
