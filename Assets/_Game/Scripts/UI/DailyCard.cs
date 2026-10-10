using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The title's Daily Challenge: today's circuit, mode and goal; tap and it starts. Met today, it says when the next
    /// one comes (no streak: a missed day costs nothing).
    public sealed class DailyCard : MonoBehaviour
    {
        [SerializeField] TMP_Text title, line;
        [SerializeField] GameObject done;

        float next;

        void OnEnable() => Show();
        void Update() { if (Time.unscaledTime > next) { next = Time.unscaledTime + 30f; Show(); } }

        public void Tap()
        {
            if (Daily.Done) { Haptics.Play(Haptics.Kind.Tick); return; }
            ScreenFlow.Instance?.StartDaily();
        }

        void Show()
        {
            var c = Daily.Current;
            if (title != null) title.text = $"DAILY  ·  {Tracks.At(c.track).name.ToUpperInvariant()}{(c.reverse ? "  ·  REVERSE" : "")}{(c.mist ? "  ·  MIST" : "")}";
            if (line != null)
                line.text = Daily.Done ? $"Done today  ·  next in {Daily.NextIn()}"
                    : $"{RaceMode.Name(c.mode).ToLowerInvariant().FirstUpper()}{(c.mode == RaceMode.Kind.Duel && RaceSpawner.Roster != null ? " vs " + RaceSpawner.Roster.At(c.rival).displayName : "")}  ·  {c.goal.ToLowerInvariant()}";
            if (done != null) done.SetActive(Daily.Done);
        }

        public void ShowInEditor() => Show();
    }

    static class StringCase
    {
        public static string FirstUpper(this string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
