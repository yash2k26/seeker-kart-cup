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
        [SerializeField] Vector3 eye = new Vector3(1.5f, 2.2f, -20.5f), aim = new Vector3(-2f, 3.5f, 0f);
        [SerializeField] float travel = 2.6f, period = 44f;
        // Select-screen pose. Geometry, not taste: at fov 30 on a 2.22 screen the kart lands at 0.65 width, 0.56 height.
        [SerializeField] float focusAngle = 25f, focusRadius = 12.5f, focusHeight = 4.7f, focusLook = 1.5f, focusYaw = 7f;
        // The driver screen's pose: a person, not a kart, so much closer and lower (Beach Buggy's driver fills the frame).
        [SerializeField] float closeAngle = 22f, closeRadius = 8.4f, closeHeight = 2.5f, closeLook = 1.05f, closeYaw = 13f;   // a standing driver is ~2.9 m at seat scale

        public bool Focused { get; set; }
        public bool Close { get; set; }
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
            float c = Mathf.SmoothStep(0f, 1f, close);
            float f = Mathf.Lerp(fit, 1f, c);                                          // the driver screen frames a person, not a kart
            float a = Mathf.Lerp(focusAngle, closeAngle, c) * Mathf.Deg2Rad, radius = Mathf.Lerp(focusRadius * f, closeRadius, c);
            var pos = target.position + new Vector3(Mathf.Sin(a) * radius, Mathf.Lerp(focusHeight * f, closeHeight, c), Mathf.Cos(a) * radius);
            var rot = Quaternion.LookRotation(target.position + Vector3.up * Mathf.Lerp(focusLook, closeLook, c) - pos) * Quaternion.Euler(0, -Mathf.Lerp(focusYaw, closeYaw, c), 0);
            if (meet != null && blend < 1f)
            {
                float x = Mathf.Sin(Time.time * 2f * Mathf.PI / period) * travel;
                var p = meet.TransformPoint(eye + new Vector3(x, 0f, 0f));
                var r = Quaternion.LookRotation(meet.TransformPoint(aim + new Vector3(x * 0.35f, 0f, 0f)) - p);   // the aim trails the camera: a track, with a little pan
                float t = Mathf.SmoothStep(0f, 1f, blend);
                pos = Vector3.Lerp(p, pos, t); rot = Quaternion.Slerp(r, rot, t);
            }
            transform.SetPositionAndRotation(pos, rot);
        }

        /// Edit mode and previews: jump to a pose without waiting for LateUpdate to ease into it.
        public void Snap(bool focused, bool close = false)
        {
            Focused = focused; blend = focused ? 1f : 0f; fit = Fit;
            Close = close; this.close = close ? 1f : 0f;
            Apply();
        }
    }
}
