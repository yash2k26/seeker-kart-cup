using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KartCup
{
    /// Relative-drag steering: wherever the thumb lands is centre, sliding sideways steers. No on-screen stick to
    /// hunt for, which is what every good phone racer does. Feeds Powerslide's mobile input struct. The STEERING
    /// setting's SLIDE: ControlLayout switches the left-half area this sits on on and off.
    public sealed class TouchSteerArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] float fullLockPixels = 220f;    // drag distance for full lock, scaled by dpi below
        [SerializeField] float returnSpeed = 8f;
        [SerializeField] InputManager input;

        float origin, steer, target;
        bool held;

        void Awake()
        {
            if (input == null) input = FindFirstObjectByType<InputManager>();
            if (Screen.dpi > 1f) fullLockPixels *= Mathf.Clamp(Screen.dpi / 420f, 0.6f, 2f);
        }

        public void OnPointerDown(PointerEventData e) { held = true; origin = e.position.x; }
        public void OnDrag(PointerEventData e) { if (held) target = Mathf.Clamp((e.position.x - origin) / fullLockPixels, -1f, 1f); }
        public void OnPointerUp(PointerEventData e) { held = false; target = 0f; }

        void Update()
        {
            steer = held ? Mathf.MoveTowards(steer, target, Time.deltaTime * 6f)
                         : Mathf.MoveTowards(steer, 0f, Time.deltaTime * returnSpeed);
            if (input != null) input.SetSteerMobile(steer);
        }

        void OnDisable() { held = false; target = steer = 0f; }
    }
}
