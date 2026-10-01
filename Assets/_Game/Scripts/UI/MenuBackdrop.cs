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

        public bool Focused { get; set; }
        float blend;

        void LateUpdate()
        {
            blend = Mathf.MoveTowards(blend, Focused ? 1f : 0f, Time.deltaTime * 1.5f);
            Apply();
        }

        public void Apply()
        {
            if (target == null) return;
            float a = focusAngle * Mathf.Deg2Rad;
            var pos = target.position + new Vector3(Mathf.Sin(a) * focusRadius, focusHeight, Mathf.Cos(a) * focusRadius);
            var rot = Quaternion.LookRotation(target.position + Vector3.up * focusLook - pos) * Quaternion.Euler(0, -focusYaw, 0);
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
        public void Snap(bool focused)
        {
            Focused = focused; blend = focused ? 1f : 0f;
            Apply();
        }
    }
}
