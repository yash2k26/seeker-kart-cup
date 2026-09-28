using PrimeTween;
using UnityEngine;

namespace KartCup
{
    /// The one motion vocabulary for panels and callouts: enter from 0.96 + fade over 220 ms with a strong ease-out,
    /// and leave the way it came in, faster. Unscaled so pause and hit-stop never freeze the UI.
    public static class UiMotion
    {
        public const float EnterSeconds = 0.22f;
        public const float ExitSeconds = 0.15f;   // exits are ~0.7x the entrance; a one-frame cut out reads as a pop

        /// Activates a panel and plays its entrance. The whole panel fades; only `card` scales — a full-screen dim or
        /// an edge-anchored plate scaled from its centre shows a gap at the screen edge.
        public static void Show(GameObject panel, Transform card = null)
        {
            if (panel == null) return;
            bool wasHidden = !panel.activeSelf;
            panel.SetActive(true);

            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            Tween.StopAll(group);                       // it may be mid-exit; take it back
            group.blocksRaycasts = true;

            if (!wasHidden || !Application.isPlaying) { group.alpha = 1f; return; }

            Tween.Alpha(group, 0f, 1f, EnterSeconds, Ease.OutCubic, useUnscaledTime: true);
            if (card == null) return;
            Tween.StopAll(card);
            Tween.Scale(card, 0.96f, 1f, EnterSeconds, Ease.OutCubic, useUnscaledTime: true);
        }

        /// Fades a panel out and then deactivates it. Deactivating on the spot is a one-frame pop, which a recording
        /// of the flow shows immediately even though a screenshot never can.
        public static void Hide(GameObject panel)
        {
            if (panel == null || !panel.activeSelf) return;
            if (!Application.isPlaying) { panel.SetActive(false); return; }

            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            Tween.StopAll(group);
            group.blocksRaycasts = false;               // stop taking touches the moment it starts leaving
            Tween.Alpha(group, group.alpha, 0f, ExitSeconds, Ease.OutCubic, useUnscaledTime: true)
                 .OnComplete(panel, p => p.SetActive(false));
        }

        /// Scale punch for a value that just changed: position, countdown digit, a callout.
        public static void Punch(Transform t, float from = 1.25f, float seconds = 0.3f)
        {
            if (t == null || !Application.isPlaying) return;
            Tween.StopAll(t);
            Tween.Scale(t, from, 1f, seconds, Ease.OutBack, useUnscaledTime: true);
        }
    }
}
