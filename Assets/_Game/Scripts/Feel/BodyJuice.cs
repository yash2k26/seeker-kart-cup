using UnityEngine;

namespace KartCup
{
    /// Squash, suspension bob, lean — on the kart's VISUAL child only (never scale the Rigidbody).
    public sealed class BodyJuice : MonoBehaviour
    {
        [SerializeField] Rigidbody rb;
        [SerializeField] float k = 140f, damp = 14f, squash = 0.6f, roll = 0.35f, pitch = 0.25f;
        float y, v; Vector3 lastVel, acc;

        /// Squash and lean for a Powerslide kart: a new "Juice" under the Visual Holder, at identity, takes the Body (the
        /// shell, the seat, the driver). The wheels are left where they are: their raycasts hold the kart up.
        public static BodyJuice Wrap(PowerslideKartPhysics.Kart kart)
        {
            var holder = kart != null ? kart.visualHolder : null;
            var bodyTr = holder != null ? holder.Find("Body") : null;
            if (bodyTr == null) return null;
            var juice = new GameObject("Juice").transform;
            juice.SetParent(holder, false);
            bodyTr.SetParent(juice, true);
            var bj = juice.gameObject.AddComponent<BodyJuice>();
            bj.rb = kart.rb;
            return bj;
        }

        void FixedUpdate()
        {
            if (rb == null) return;
            acc = (rb.linearVelocity - lastVel) / Time.fixedDeltaTime;
            lastVel = rb.linearVelocity;
        }

        /// Call on landing (negative impact) or hit.
        public void Kick(float dv) => v += dv;

        void LateUpdate()
        {
            if (rb == null) return;
            float dt = Time.deltaTime;
            var la = rb.transform.InverseTransformDirection(acc);
            v += (-k * y - damp * v) * dt; y += v * dt;
            float s = Mathf.Clamp(-y * squash, -0.12f, 0.12f);      // ponytail: fake volume preservation
            transform.localPosition = new Vector3(0, y, 0);
            transform.localScale = new Vector3(1 + s, 1 - s, 1 + s);
            transform.localRotation = Quaternion.Euler(Mathf.Clamp(-la.z * pitch, -6, 6), 0, Mathf.Clamp(-la.x * roll, -8, 8));
        }
    }
}
