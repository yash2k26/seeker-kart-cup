using UnityEngine;

namespace KartCup
{
    /// A one-shot effect that removes itself once it has played.
    public sealed class Lifetime : MonoBehaviour
    {
        public float seconds = 2f;
        void Start() => Destroy(gameObject, seconds);
    }
}
