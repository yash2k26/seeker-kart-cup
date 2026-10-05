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

        /// Set by SwipeSelect: the thumb turns the podium 1:1, and it eases home when the finger lifts.
        public float DragYaw { get; set; }
        public bool Dragging { get; set; }
    }

    /// Finger on the vista: drag turns the kart with the thumb, a flick changes character. The arrows stay for
    /// anyone who prefers them; this is the gesture every phone select screen already teaches.
    public sealed class SwipeSelect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Carousel carousel;
        [SerializeField] float flickPixels = 90f;      // at 420 dpi; scaled below
        [SerializeField] float degreesPerPixel = 0.25f;

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
            float dx = e.position.x - startX;
            float flick = flickPixels * DpiScale();
            if (Mathf.Abs(dx) <= flick) return;
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
