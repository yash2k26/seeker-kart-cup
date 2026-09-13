using UnityEngine;

namespace KartCup
{
    /// Gate-weekend stand-in for the kart controller: drives a circle, spins wheels, feeds steer to the driver pose.
    /// Deleted once Ultimate Kart Racing's Kart component takes over.
    public sealed class DemoDrive : MonoBehaviour
    {
        [SerializeField] float radius = 22f, speed = 14f, wheelRadius = 0.32f;
        [SerializeField] Transform[] wheels;
        [SerializeField] BlockyDriverPose driver;
        [SerializeField] Rigidbody rb;
        float angle;

        void FixedUpdate()
        {
            float w = speed / radius;
            angle += w * Time.fixedDeltaTime;
            var pos = new Vector3(Mathf.Sin(angle) * radius, 0.0f, Mathf.Cos(angle) * radius);
            var fwd = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
            if (rb) { rb.MovePosition(pos); rb.MoveRotation(Quaternion.LookRotation(fwd, Vector3.up)); }
            else { transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up)); }
            float spin = speed / wheelRadius * Mathf.Rad2Deg * Time.fixedDeltaTime;
            foreach (var wh in wheels) if (wh) wh.Rotate(spin, 0f, 0f, Space.Self);
            if (driver) driver.Steer = 0.6f;   // constant left turn = constant lean, proves the pose script
        }
    }
}
