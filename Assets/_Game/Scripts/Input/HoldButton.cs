using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KartCup
{
    /// One on-screen control. Press-and-hold for drift/brake, single press for the item slot.
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Control { Drift, Brake, Item, Boost, SteerLeft, SteerRight }
        [SerializeField] Control control = Control.Drift;
        [SerializeField] InputManager input;
        [SerializeField] CanvasGroup fade;               // optional: dims while held
        [SerializeField] SteerButtons steering;

        void Awake()
        {
            if (input == null) input = FindFirstObjectByType<InputManager>();
            if (steering == null) steering = FindFirstObjectByType<SteerButtons>();
        }

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
            if (input == null && steering == null) return;
            if (input == null && control != Control.SteerLeft && control != Control.SteerRight) return;
            switch (control)
            {
                case Control.Drift: input.SetDriftMobile(down); break;
                case Control.Brake: input.SetBrakeMobile(down ? 1f : 0f); break;
                case Control.Boost: input.SetBoostMobile(down); break;
                case Control.Item: if (down) input.PressItemMobile(); break;
                case Control.SteerLeft: steering?.SetLeft(down); break;
                case Control.SteerRight: steering?.SetRight(down); break;
            }
        }

        void OnDisable() { Set(false); }
    }
}
