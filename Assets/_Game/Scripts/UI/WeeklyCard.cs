using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The title's line for Kartel Weekly: this week's circuit, what is on it and how long is left, or where you stand.
    /// Tap: the Weekly screen. Hidden while the Worker can't be reached (never a stale or empty promise).
    public sealed class WeeklyCard : MonoBehaviour
    {
        [SerializeField] GameObject content, fallback;   // fallback: the on-chain line the card stands in for
        [SerializeField] TMP_Text title, line;

        float next;

        void OnEnable() { Weekly.Changed += Show; Show(); if (Application.isPlaying) _ = Weekly.Fetch(); }
        void OnDisable() => Weekly.Changed -= Show;
        void Update() { if (Time.unscaledTime > next) { next = Time.unscaledTime + 20f; Show(); } }   // the countdown

        public void Tap() => ScreenFlow.Instance?.ShowWeekly();

        void Show()
        {
            var b = Weekly.Current;
            if (content != null) content.SetActive(b != null);
            if (fallback != null) fallback.SetActive(b == null);
            if (b == null) return;
            var track = Tracks.At(Mathf.Max(0, Weekly.TrackIndex)).name.ToUpperInvariant();
            if (title != null) title.text = $"KARTEL WEEKLY  ·  {track}";
            string where = b.me != null
                ? $"You're #{b.me.rank}" + (b.me.rank > 1 ? $" · {b.me.gapMs / 1000f:0.00} s to #{b.me.rank - 1}" : " · fastest this week")
                : b.pot != null ? $"Top 3 Seekers win {b.pot:N0} {b.currency}" : "Free time trial · the same kart for everyone";
            if (line != null) line.text = $"{where}   ·   ends in {Weekly.Countdown(Weekly.SecondsLeft)}";
        }

        /// Editor-side preview.
        public void ShowInEditor() => Show();
    }
}
