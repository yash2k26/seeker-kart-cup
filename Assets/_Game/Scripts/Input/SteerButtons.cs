using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KartCup
{
    /// The steering, whichever way the player chose (Settings.Steering; Beach Buggy offers Touch A / Touch B / Tilt).
    /// ARROWS: holding one ramps the steering toward full lock and releasing springs it back, so taps give small
    /// corrections and holds give full turns. TILT: the phone is the wheel. SLIDE is TouchSteerArea's. Under SLIDE and
    /// TILT the arrows stop being buttons and only light the way the kart is being steered (Beach Buggy's grey arrow).
    public sealed class SteerButtons : MonoBehaviour
    {
        [SerializeField] InputManager input;
        [SerializeField] float pressRate = 4.2f;      // lock per second while held
        [SerializeField] float releaseRate = 7f;
        // g along the screen's x (the Input System turns the sensor with the screen): ~3 deg of slack, full lock at ~22 deg
        [SerializeField] float tiltDead = 0.05f, tiltFull = 0.38f;
        [SerializeField] CanvasGroup leftArrow, rightArrow;

        bool left, right;
        float steer;

        void Awake() { if (input == null) input = FindFirstObjectByType<InputManager>(); }

        public void SetLeft(bool held) { left = held; }
        public void SetRight(bool held) { right = held; }

        void Update()
        {
            var mode = Settings.Steering;
            if (mode == Settings.Steer.Tilt)
                steer = Mathf.Lerp(steer, Tilt(), 1f - Mathf.Exp(-14f * Time.deltaTime));   // smooths the sensor's jitter
            else if (mode == Settings.Steer.Arrows)
            {
                float target = (right ? 1f : 0f) - (left ? 1f : 0f);
                float rate = Mathf.Approximately(target, 0f) ? releaseRate : pressRate;
                steer = Mathf.MoveTowards(steer, target, rate * Time.deltaTime);
            }
            if (mode != Settings.Steer.Slide && input != null) input.SetSteerMobile(steer);
            if (mode == Settings.Steer.Arrows) return;
            float s = InputManager.MobileInput.steerInput;
            if (leftArrow != null) leftArrow.alpha = 0.3f + 0.7f * Mathf.Clamp01(-s);
            if (rightArrow != null) rightArrow.alpha = 0.3f + 0.7f * Mathf.Clamp01(s);
        }

        float Tilt()
        {
            var acc = Accelerometer.current;
            if (acc == null) return 0f;
            if (!acc.enabled) InputSystem.EnableDevice(acc);
            float x = acc.acceleration.ReadValue().x;      // right side down: gravity leans to the screen's right, x > 0
            return Mathf.Sign(x) * Mathf.InverseLerp(tiltDead, tiltFull, Mathf.Abs(x));
        }

        void OnDisable()
        {
            left = right = false; steer = 0f;
            if (Accelerometer.current != null && Accelerometer.current.enabled) InputSystem.DisableDevice(Accelerometer.current);
        }
    }
}
