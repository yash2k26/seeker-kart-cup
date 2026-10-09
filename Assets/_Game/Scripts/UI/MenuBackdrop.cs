using UnityEngine;

namespace KartCup
{
    /// The menu's camera. On the title it tracks slowly along the street meet (a dolly back and forth, so the karts
    /// slide against the hoarding behind them and the title is never a still). On the screens that choose a kart it
    /// eases to one fixed, composed pose on the turntable: drifting *and* a spinning podium put the kart off the edge
    /// of the screen.
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] Transform target;
        // The title pose, in the meet's own frame: where the camera stands, the point it looks at, how far it tracks
        // either way along the road and how long there and back takes.
        [SerializeField] Transform meet;
        [SerializeField] Vector3 eye = new Vector3(1.5f, 3.4f, -20.5f), aim = new Vector3(-2f, 2.8f, 0f);   // a little above head height, looking down a touch: the home stage sits under the row, not inside it
        [SerializeField] float travel = 2.6f, period = 44f;
        // The hub's pose (10 Oct 2026, Beach Buggy's): straight in front of the home stage, a touch above it and looking
        // down, with a short dolly either way. The eye/aim dolly along the meet is what the title does without a stage.
        [SerializeField] Vector3 homeEye = new Vector3(0f, 3.2f, -15f), homeAim = new Vector3(0f, 1.4f, 0f);
        [SerializeField] float homeTravel = 1.0f;
        // Select-screen pose. Geometry, not taste: at fov 30 on a 2.22 screen the kart lands at 0.65 width, 0.56 height.
        [SerializeField] float focusAngle = 25f, focusRadius = 12.5f, focusHeight = 4.7f, focusLook = 1.5f, focusYaw = 7f;
        // The driver screen's pose: a person, not a kart, so much closer and lower (Beach Buggy's driver fills the frame).
        [SerializeField] float closeAngle = 22f, closeRadius = 8.4f, closeHeight = 2.5f, closeLook = 1.05f, closeYaw = 13f;   // a standing driver is ~2.9 m at seat scale
        // The street select's pose (9 Oct 2026): on the road side of a driver's bay, a little to their right, so the driver
        // fills the right of the frame with their kart behind them (the card covers the left third). glide: how quickly
        // the camera arrives at the next bay (a dolly, not a cut).
        [SerializeField] float spotAngle = 22f, spotRadius = 10.2f, spotHeight = 3.0f, spotLook = 1.2f, spotYaw = 8f, glide = 4.5f;
        // A far-lane bay (MenuMeet FarZ) is looked at from between the near karts: straighter, closer, from higher up.
        [SerializeField] float farAngle = 0f, farRadius = 9.5f, farHeight = 5.2f, farLook = 1.6f, farYaw = 8f;   // straight down the gap between the near karts, high enough to look over their drivers

        public bool Focused { get; set; }
        public bool Close { get; set; }
        /// A driver's bay on the meet (their Driver_<name> holder): while set and Focused, the camera frames it instead
        /// of the turntable.
        public Transform Spot { get; set; }
        /// The chosen driver's bay: the title's dolly runs past it, so the home screen shows your driver and kart.
        public Transform Home { get; set; }
        public Transform Meet => meet;
        /// The bay MenuMeet built for a driver, or null (no meet, or a name not on the street).
        public Transform BayOf(string driverName)
        {
            if (meet == null || string.IsNullOrEmpty(driverName)) return null;
            foreach (var t in meet.GetComponentsInChildren<Transform>(true)) if (t.name == "Driver_" + driverName) return t;
            return null;
        }
        Vector3 curPos; Quaternion curRot; bool placed;
        /// The kart on show, as a share of the biggest (the 4.9 m trucks the pose was set for): a small kart is framed
        /// closer, so it fills the screen as Beach Buggy's cars do. Eased like the rest.
        public float Fit { get; set; } = 1f;
        float blend, close, fit = 1f;

        void LateUpdate()
        {
            blend = Mathf.MoveTowards(blend, Focused ? 1f : 0f, Time.deltaTime * 1.5f);
            close = Mathf.MoveTowards(close, Close ? 1f : 0f, Time.deltaTime * 1.5f);
            fit = Mathf.Lerp(fit, Fit, 1f - Mathf.Exp(-4f * Time.deltaTime));
            Apply();
        }

        public void Apply()
        {
            if (target == null) return;
            Pose(out var pos, out var rot);
            float k = placed && Application.isPlaying ? 1f - Mathf.Exp(-glide * Time.deltaTime) : 1f;
            curPos = Vector3.Lerp(curPos, pos, k); curRot = Quaternion.Slerp(curRot, rot, k); placed = true;
            transform.SetPositionAndRotation(curPos, curRot);
        }

        void Pose(out Vector3 pos, out Quaternion rot)
        {
            float c = Mathf.SmoothStep(0f, 1f, close);
            float f = Mathf.Lerp(fit, 1f, c);                                          // the driver screen frames a person, not a kart
            float a = Mathf.Lerp(focusAngle, closeAngle, c) * Mathf.Deg2Rad, radius = Mathf.Lerp(focusRadius * f, closeRadius, c);
            pos = target.position + new Vector3(Mathf.Sin(a) * radius, Mathf.Lerp(focusHeight * f, closeHeight, c), Mathf.Cos(a) * radius);
            rot = Quaternion.LookRotation(target.position + Vector3.up * Mathf.Lerp(focusLook, closeLook, c) - pos) * Quaternion.Euler(0, -Mathf.Lerp(focusYaw, closeYaw, c), 0);
            if (Spot != null && meet != null)
            {   // the bay's pose, in the meet's frame (the road, and the camera, are on its -Z side)
                var local = meet.InverseTransformPoint(Spot.position); local.y = 0f;
                bool far = local.z > 1f;
                float s = (far ? farAngle : spotAngle) * Mathf.Deg2Rad, rad = far ? farRadius : spotRadius;
                pos = meet.TransformPoint(local + new Vector3(Mathf.Sin(s) * rad, far ? farHeight : spotHeight, -Mathf.Cos(s) * rad));
                rot = Quaternion.LookRotation(meet.TransformPoint(local + Vector3.up * (far ? farLook : spotLook)) - pos) * Quaternion.Euler(0, -(far ? farYaw : spotYaw), 0);
            }
            if (meet != null && blend < 1f)
            {
                float x = Mathf.Sin(Time.time * 2f * Mathf.PI / period) * (Home != null ? homeTravel : travel);
                Vector3 p, at;
                if (Home != null) { p = Home.position + meet.rotation * (homeEye + new Vector3(x, 0f, 0f)); at = Home.position + meet.rotation * (homeAim + new Vector3(x * 0.35f, 0f, 0f)); }
                else { p = meet.TransformPoint(eye + new Vector3(x, 0f, 0f)); at = meet.TransformPoint(aim + new Vector3(x * 0.35f, 0f, 0f)); }
                var r = Quaternion.LookRotation(at - p);   // the aim trails the camera: a track, with a little pan
                float t = Mathf.SmoothStep(0f, 1f, blend);
                pos = Vector3.Lerp(p, pos, t); rot = Quaternion.Slerp(r, rot, t);
            }
        }

        /// Edit mode and previews: jump to a pose without waiting for LateUpdate to ease into it.
        public void Snap(bool focused, bool close = false)
        {
            Focused = focused; blend = focused ? 1f : 0f; fit = Fit;
            Close = close; this.close = close ? 1f : 0f;
            placed = false; Apply();
        }
    }
}
