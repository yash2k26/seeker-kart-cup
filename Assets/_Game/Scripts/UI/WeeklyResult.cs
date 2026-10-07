using TMPro;
using UnityEngine;

namespace KartCup
{
    /// On the results card after a Kartel Weekly time trial: where the lap stands this week once the Worker has it, or
    /// why it is not on the board (no wallet signed in, offline, refused by the referee).
    public sealed class WeeklyResult : MonoBehaviour
    {
        [SerializeField] TMP_Text line;

        void OnEnable()
        {
            if (line == null) return;
            line.gameObject.SetActive(Weekly.Racing);
            if (!Weekly.Racing || !Application.isPlaying) return;
            line.text = "Putting your lap on this week's board…";
            if (RaceSession.Instance != null) RaceSession.Instance.ResultSubmitted.AddListener(OnSubmitted);
            if (RaceSession.Instance != null && RaceSession.Instance.LastSubmit != null) OnSubmitted(false);
        }

        void OnDisable() { if (RaceSession.Instance != null) RaceSession.Instance.ResultSubmitted.RemoveListener(OnSubmitted); }

        async void OnSubmitted(bool _)
        {
            var submit = RaceSession.Instance != null ? RaceSession.Instance.LastSubmit : null;
            if (submit != "accepted") { line.text = submit == "offline" ? "Offline: this lap isn't on the board" : "This lap wasn't accepted for the board"; return; }
            if (!KartApi.HasSession) { line.text = "Sign in (SAVE) to put this lap on Kartel Weekly"; return; }
            var b = await Weekly.Fetch(force: true);
            if (this == null || line == null) return;
            line.text = b?.me == null ? "Your lap is in: see KARTEL WEEKLY on the title"
                : $"KARTEL WEEKLY  ·  #{b.me.rank} of {b.me.entrants}  ·  best {RaceHud.Clock(b.me.bestMs / 1000f)}" + (b.me.rank > 1 ? $"  ·  {b.me.gapMs / 1000f:0.00} s to #{b.me.rank - 1}" : "  ·  fastest this week");
        }
    }
}
