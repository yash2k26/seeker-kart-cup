using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KartCup
{
    /// One on-screen control. Press-and-hold for drift/brake, single press for the item slot.
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Control { Drift, Brake, Item, Boost }
        [SerializeField] Control control = Control.Drift;
        [SerializeField] InputManager input;
        [SerializeField] CanvasGroup fade;               // optional: dims while held

        void Awake() { if (input == null) input = FindFirstObjectByType<InputManager>(); }

        public void OnPointerDown(PointerEventData e)
        {
            Set(true);
            Haptics.Play(Haptics.Kind.Tick);
            if (fade != null) fade.alpha = 0.6f;
        }

        public void OnPointerUp(PointerEventData e)
        {
            Set(false);
            if (fade != null) fade.alpha = 1f;
        }

        void Set(bool down)
        {
            if (input == null) return;
            switch (control)
            {
                case Control.Drift: input.SetDriftMobile(down); break;
                case Control.Brake: input.SetBrakeMobile(down ? 1f : 0f); break;
                case Control.Boost: input.SetBoostMobile(down); break;
                case Control.Item: if (down) input.PressItemMobile(); break;
            }
        }

        void OnDisable() { Set(false); }
    }
}
