using UnityEngine;

namespace KartCup
{
    /// Turns something at a steady rate: the lighthouse lamp makes one full turn every `period` seconds.
    public sealed class Rotor : MonoBehaviour
    {
        public float period = 8f;
        public Vector3 axis = Vector3.up;

        void Update() { if (period > 0f) transform.Rotate(axis, 360f / period * Time.deltaTime, Space.Self); }
    }
}
