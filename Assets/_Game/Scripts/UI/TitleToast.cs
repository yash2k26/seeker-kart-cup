using PrimeTween;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// One line at the top of the title with one action, a moment after it opens: last week's Kartel Weekly result to
    /// claim (CLAIM opens its chests), or else the rival who got past you since you last looked (RACE takes you to
    /// that circuit). At most one a visit; it goes by itself after a few seconds. No push notifications, ever.
    public sealed class TitleToast : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform pill;
        [SerializeField] TMP_Text text, actionLabel;

        System.Action action;
        float hideAt;
        static int shownForVisit = -1;    // Time.frameCount of the title visit that already had one

        void OnEnable()
        {
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
            hideAt = 0f;
            if (Application.isPlaying) Run();
        }

        void Update() { if (hideAt > 0f && Time.unscaledTime > hideAt) Hide(); }

        async void Run()
        {
            int visit = Time.frameCount;
            await Awaitable.WaitForSecondsAsync(1.6f);                // after the title has assembled
            if (this == null || !isActiveAndEnabled || shownForVisit >= visit - 200) return;
            var b = await Weekly.Fetch();
            if (this == null || !isActiveAndEnabled) return;
            if (b?.last?.me is { } last && !Chests.Claimed(b.last.week))
            {
                var track = Tracks.At(Mathf.Max(0, Tracks.IndexOf(b.last.track))).name;
                bool prize = last.prizePlace != null;
                string line = prize
                    ? $"KARTEL WEEKLY  ·  {RaceHud.Ordinal(last.prizePlace.Value).ToUpperInvariant()} SEEKER ON {track.ToUpperInvariant()}" + (last.prize != null ? $"  ·  {last.prize:N0} SKR on its way" : "")
                    : $"KARTEL WEEKLY  ·  you were #{last.rank} of {last.entrants} on {track}";
                Show(line, "CLAIM", () => { Chests.ClaimWeekly(b.last.week, prize); ChestReveal.Instance?.OpenNext(); }, 9f);
                shownForVisit = visit;
                return;
            }
            var pass = await Rivals.Check();
            if (this == null || !isActiveAndEnabled || pass == null) return;
            Show(pass.text, "RACE", () => { if (pass.weekly) ScreenFlow.Instance?.StartWeekly(); else ScreenFlow.Instance?.RaceOn(pass.track); }, 7f);
            shownForVisit = visit;
        }

        void Show(string line, string act, System.Action onAct, float seconds)
        {
            if (group == null) return;
            text.text = line; actionLabel.text = act; action = onAct;
            group.blocksRaycasts = true;
            Tween.Alpha(group, 1f, 0.25f, useUnscaledTime: true);
            if (pill != null) { pill.anchoredPosition = new Vector2(0, 120); Tween.UIAnchoredPosition(pill, Vector2.zero, 0.4f, Ease.OutBack, useUnscaledTime: true); }
            Haptics.Play(Haptics.Kind.Tick);
            hideAt = Time.unscaledTime + seconds;
        }

        public void Act() { var a = action; Hide(); a?.Invoke(); }

        public void Hide()
        {
            hideAt = 0f;
            if (group == null) return;
            group.blocksRaycasts = false;
            Tween.Alpha(group, 0f, 0.2f, useUnscaledTime: true);
        }

        /// Editor-side preview.
        public void ShowInEditor(string line, string act) { if (group == null) return; text.text = line; actionLabel.text = act; group.alpha = 1f; }
    }
}
