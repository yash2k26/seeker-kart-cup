using UnityEngine;
using UnityEngine.EventSystems;

namespace KartCup
{
    /// What a flick steps: the character podium and the track cards. Only the podium turns with the thumb, so the
    /// drag state lives here once rather than as a second pair of properties the track select would have to ignore.
    public abstract class Carousel : MonoBehaviour
    {
        public abstract void Next();
        public abstract void Previous();
        /// A tap on the vista (no drag): the character select's driver answers it; others ignore it.
        public virtual void Poke() { }

        /// Set by SwipeSelect: the thumb turns the podium 1:1, and it eases home when the finger lifts.
        public float DragYaw { get; set; }
        public bool Dragging { get; set; }
    }

    /// Finger on the vista: drag turns the kart with the thumb, a flick changes character. The arrows stay for
    /// anyone who prefers them; this is the gesture every phone select screen already teaches.
    public sealed class SwipeSelect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        /// A tap that never became a drag (uGUI cancels the click once the drag threshold is crossed): poke the kart.
        public void OnPointerClick(PointerEventData e) { if (!e.dragging && carousel != null) carousel.Poke(); }

        [SerializeField] Carousel carousel;
        [SerializeField] float flickPixels = 90f;      // at 420 dpi; scaled below
        [SerializeField] float degreesPerPixel = 0.25f;
        /// A flick steps to the next one. Off on the driver and kart screens (10 Oct 2026, the user: a drag to look a
        /// kart over kept switching karts); the arrows and the roster strip step there, the drag only turns.
        [SerializeField] bool flick = true;

        float startX;

        public void OnBeginDrag(PointerEventData e)
        {
            startX = e.position.x;
            if (carousel != null) carousel.Dragging = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (carousel != null) carousel.DragYaw += e.delta.x * degreesPerPixel / DpiScale();
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (carousel == null) return;
            carousel.Dragging = false;
            if (!flick) return;
            float dx = e.position.x - startX;
            float threshold = flickPixels * DpiScale();
            if (Mathf.Abs(dx) <= threshold) return;
            if (dx < 0) carousel.Next(); else carousel.Previous();
            Haptics.Play(Haptics.Kind.Tick);          // a flick has no button to click for it
            AudioDirector.Instance?.Click();
        }

        /// uGUI does not deliver OnEndDrag to a deactivated object, so releasing the flag here is the only way a
        /// drag interrupted by Back (or a second finger on BACK) lets the podium ease home.
        void OnDisable() { if (carousel != null) carousel.Dragging = false; }

        static float DpiScale() => Screen.dpi > 1f ? Mathf.Clamp(Screen.dpi / 420f, 0.6f, 2f) : 1f;
    }
}
