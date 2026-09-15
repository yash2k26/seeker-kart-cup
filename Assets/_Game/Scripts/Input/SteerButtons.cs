using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Two on-screen arrows instead of a drag area. Holding one ramps the steering toward full lock and releasing
    /// springs it back, so taps give small corrections and holds give full turns.
    public sealed class SteerButtons : MonoBehaviour
    {
        [SerializeField] InputManager input;
        [SerializeField] float pressRate = 4.2f;      // lock per second while held
        [SerializeField] float releaseRate = 7f;

        bool left, right;
        float steer;

        void Awake() { if (input == null) input = FindFirstObjectByType<InputManager>(); }

        public void SetLeft(bool held) { left = held; }
        public void SetRight(bool held) { right = held; }

        void Update()
        {
            float target = (right ? 1f : 0f) - (left ? 1f : 0f);
            float rate = Mathf.Approximately(target, 0f) ? releaseRate : pressRate;
            steer = Mathf.MoveTowards(steer, target, rate * Time.deltaTime);
            if (input != null) input.SetSteerMobile(steer);
        }

        void OnDisable() { left = right = false; steer = 0f; }
    }
}
