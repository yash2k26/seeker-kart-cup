using PrimeTween;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// What the driver on the turntable just said, in a comic bubble beside their head: pops in with the line,
    /// follows the head as the turntable turns, and goes once they have finished speaking. Voices.Spoke drives it.
    public sealed class SpeechBubble : MonoBehaviour
    {
        [SerializeField] RectTransform bubble;     // pivot on the tail's tip
        [SerializeField] RectTransform body;       // the rounded card the words sit on
        [SerializeField] TMP_Text words;
        [SerializeField] CanvasGroup group;
        [SerializeField] Transform podium;         // the driver on it is found by their head
        [SerializeField] float maxWidth = 560f;

        Transform head;
        float hideAt;
        bool showing;

        void OnEnable()
        {
            Voices.Spoke += OnSpoke;
            CharacterCarousel.Changed += Forget;
            if (group != null) group.alpha = 0f;
            showing = false;
        }

        void OnDisable()
        {
            Voices.Spoke -= OnSpoke;
            CharacterCarousel.Changed -= Forget;
        }

        void Forget(int _) => head = null;

        void OnSpoke(string driver, string line, float seconds)
        {
            if (bubble == null || words == null) return;
            words.text = line;
            var size = words.GetPreferredValues(line, maxWidth, 0f);
            float w = Mathf.Min(size.x, maxWidth) + 56f, h = Mathf.Max(size.y, 44f) + 36f;
            if (body != null) body.sizeDelta = new Vector2(w, h);
            hideAt = Time.unscaledTime + seconds + 0.8f;
            showing = true;
            Tween.StopAll(bubble); Tween.StopAll(group);
            group.alpha = 1f;
            bubble.localScale = Vector3.one * 0.55f;
            Tween.Scale(bubble, 1f, 0.24f, Ease.OutBack, useUnscaledTime: true);
            Follow();
        }

        void LateUpdate()
        {
            if (!showing) return;
            Follow();
            if (Time.unscaledTime < hideAt) return;
            showing = false;
            Tween.Alpha(group, 0f, 0.18f, useUnscaledTime: true);
            Tween.Scale(bubble, 0.85f, 0.18f, Ease.InCubic, useUnscaledTime: true);
        }

        /// Beside the head (the driver's Head, under the podium), a little above and to the right.
        void Follow()
        {
            if (head == null && podium != null)
                foreach (var t in podium.GetComponentsInChildren<Transform>()) if (t.name == "Head") { head = t; break; }
            var cam = Camera.main;
            if (head == null || cam == null) return;
            var screen = cam.WorldToScreenPoint(head.position + Vector3.up * 0.9f);
            var parent = (RectTransform)bubble.parent;
            // the bubble is anchored on its parent's pivot, so a local point is its anchored position
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
                bubble.anchoredPosition = local + new Vector2(70f, 30f);
        }
    }
}
