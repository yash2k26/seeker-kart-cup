using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KartCup
{
    /// Press feedback lands on press-in, not release: 0.96 in 100 ms, back in 150 ms. Unscaled time so it still
    /// works on the pause overlay. One vocabulary for every button in the game.
    public sealed class PressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] float pressed = 0.96f;

        public void OnPointerDown(PointerEventData e) => Go(pressed, 0.10f, Ease.OutQuad);
        public void OnPointerUp(PointerEventData e) => Go(1f, 0.15f, Ease.OutBack);

        void Go(float to, float seconds, Ease ease)
        {
            Tween.StopAll(transform);
            Tween.Scale(transform, to, seconds, ease, useUnscaledTime: true);
        }

        void OnDisable() { Tween.StopAll(transform); transform.localScale = Vector3.one; }
    }
}
