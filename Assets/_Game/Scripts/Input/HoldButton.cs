using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KartCup
{
    /// One on-screen control. Press-and-hold for drift/brake, single press for the item slot.
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Control { Drift, Brake, Item, Boost, SteerLeft, SteerRight, Accelerate, Ability }
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

        // A thumb that slides from one control to its neighbour must hand over: uGUI keeps sending enter/exit to a
        // touch pointer while it is down, so release on exit and engage on enter only when a finger is actually down.
        public void OnPointerEnter(PointerEventData e)
        {
            if (e.pointerPress == null) return;   // mouse hover in the editor must not engage the control
            Set(true);
            if (fade != null) fade.alpha = 0.6f;
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (e.pointerPress == null) return;
            Set(false);
            if (fade != null) fade.alpha = 1f;
        }

        void Set(bool down)
        {
            if (input == null && steering == null) return;
            if (control == Control.Ability) { if (down) RaceHud.Instance?.FireAbility(); return; }
            if (input == null && control != Control.SteerLeft && control != Control.SteerRight) return;
            switch (control)
            {
                case Control.Drift: input.SetDriftMobile(down); break;
                case Control.Brake: input.SetBrakeMobile(down ? 1f : 0f); break;
                case Control.Boost: input.SetBoostMobile(down); break;
                case Control.Item:
                    if (!down) break;
                    // while the slot is still spinning, a tap stops it on the item (it does not fire it)
                    if (RaceHud.Instance != null && RaceHud.Instance.Rolling) RaceHud.Instance.StopRoulette(); else input.PressItemMobile();
                    break;
                case Control.Accelerate: input.SetAccelMobile(down ? 1f : 0f); break;
                case Control.SteerLeft: steering?.SetLeft(down); break;
                case Control.SteerRight: steering?.SetRight(down); break;
            }
        }

        void OnDisable() { Set(false); }
    }
}
