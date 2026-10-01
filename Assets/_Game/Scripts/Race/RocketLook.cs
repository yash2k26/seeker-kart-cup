using UnityEngine;

namespace KartCup
{
    /// A fired rocket points where it is going. Powerslide freezes a projectile's rotation, so the body would keep
    /// the angle it left the pod at while a homing shot curved away under it: this turns the model, not the rigidbody.
    public sealed class RocketLook : MonoBehaviour
    {
        public Transform model;
        Rigidbody rb;

        void Awake() => rb = GetComponent<Rigidbody>();

        void LateUpdate()
        {
            if (model == null || rb == null) return;
            var v = rb.linearVelocity;
            if (v.sqrMagnitude > 4f) model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(v), 1f - Mathf.Exp(-18f * Time.deltaTime));
        }
    }
}
