using UnityEngine;

namespace KartCup
{
    /// Turns a UI picture slowly (the daily wheel's tile): it reads as a wheel, not a pie chart.
    public sealed class SlowTurn : MonoBehaviour
    {
        public float degreesPerSecond = -14f;
        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
