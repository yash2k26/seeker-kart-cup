using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KartCup
{
    /// Press feedback lands on press-in, not release: the face drops into its lip (the Down sprite), what is written
    /// on it drops with it, the button gives a little (0.96 in 100 ms, back in 150 ms) and the phone ticks and clicks.
    /// A button that cannot be pressed shakes its head instead. Unscaled time so it still works on the pause overlay.
    /// One vocabulary for every button in the game; the race's hold controls get the movement but stay silent.
    public sealed class PressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] float pressed = 0.96f;
        [SerializeField] Sprite down;            // the face pressed into its lip (Design.PillDown); none: it only gives
        [SerializeField] RectTransform sink;     // the label or icon on the face
        [SerializeField] float sinkBy;           // how far the face drops, canvas units

        Image image; Sprite up; Vector2 home; bool isDown;
        Tween give, shake;     // its own tweens only: other scripts move these transforms too (the roster lifts a tile)

        public void Configure(Sprite downSprite, RectTransform onFace, float drop) { down = downSprite; sink = onFace; sinkBy = drop; }

        public void OnPointerDown(PointerEventData e)
        {
            var selectable = GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable())
            {
                if (!shake.isAlive) shake = Tween.PunchLocalPosition(transform, new Vector3(14f, 0f, 0f), 0.3f, frequency: 8, useUnscaledTime: true);
                AudioDirector.Instance?.Error();
                return;
            }
            Go(pressed, 0.10f, Ease.OutQuad);
            if (!isDown)
            {
                if (down != null) { image ??= GetComponent<Image>(); up = image.sprite; image.sprite = down; }
                if (sink != null) { home = sink.anchoredPosition; sink.anchoredPosition = home - new Vector2(0f, sinkBy); }
                isDown = true;
            }
            if (GetComponent<HoldButton>() != null) return;          // GAS held for a whole race must not click
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
        }

        public void OnPointerUp(PointerEventData e)
        {
            Release();
            Go(1f, 0.15f, Ease.OutBack);
        }

        void Release()
        {
            if (!isDown) return;
            isDown = false;
            if (down != null && image != null) image.sprite = up;
            if (sink != null) sink.anchoredPosition = home;
        }

        void Go(float to, float seconds, Ease ease)
        {
            if (give.isAlive) give.Stop();
            give = Tween.Scale(transform, to, seconds, ease, useUnscaledTime: true);
        }

        void OnDisable()
        {
            if (give.isAlive) give.Stop();
            if (shake.isAlive) shake.Complete();
            transform.localScale = Vector3.one;
            Release();
        }
    }
}
