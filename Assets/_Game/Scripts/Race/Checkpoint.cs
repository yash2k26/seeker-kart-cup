using UnityEngine;

namespace KartCup
{
    /// A gate across the track. Karts must cross them in order; the one with index 0 is the start/finish line.
    public sealed class Checkpoint : MonoBehaviour
    {
        public int index;
        public Vector3 RespawnPosition => transform.position + Vector3.up * 1.2f;
        public Quaternion RespawnRotation => transform.rotation;

        void OnTriggerEnter(Collider other)
        {
            var progress = other.GetComponentInParent<KartProgress>();
            if (progress != null) progress.Pass(this);
        }
    }
}
