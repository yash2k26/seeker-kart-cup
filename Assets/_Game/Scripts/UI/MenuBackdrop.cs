using UnityEngine;

namespace KartCup
{
    /// Slow camera drift over the menu vista so the title screen is never a still image. On character select it eases
    /// to one fixed, composed pose: drifting *and* a spinning podium put the kart off the edge of the screen.
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float radius = 9f, height = 3.4f, speed = 4f, look = 1.1f;
        [SerializeField] float yawOffset = 13f;   // swings the subject into the right half, clear of the title text
        // Select-screen pose. Geometry, not taste: at fov 30 on a 2.22 screen the kart lands at 0.65 width, 0.56 height.
        [SerializeField] float focusAngle = 25f, focusRadius = 12.5f, focusHeight = 4.7f, focusLook = 1.5f, focusYaw = 7f;

        public bool Focused { get; set; }
        float angle, blend;

        void LateUpdate()
        {
            blend = Mathf.MoveTowards(blend, Focused ? 1f : 0f, Time.deltaTime * 1.5f);
            // Ease in BOTH directions: assigning the idle angle outright snapped the whole vista on the way out.
            float want = Focused ? focusAngle : focusAngle + Mathf.Sin(Time.time * 0.15f) * 5f;
            angle = Mathf.LerpAngle(angle, want, 1f - Mathf.Exp(-3.5f * Time.deltaTime));
            Apply();
        }

        public void Apply()
        {
            if (target == null) return;
            float r = Mathf.Lerp(radius, focusRadius, blend), h = Mathf.Lerp(height, focusHeight, blend);
            float l = Mathf.Lerp(look, focusLook, blend), y = Mathf.Lerp(yawOffset, focusYaw, blend);
            float a = angle * Mathf.Deg2Rad;
            transform.position = target.position + new Vector3(Mathf.Sin(a) * r, h, Mathf.Cos(a) * r);
            transform.LookAt(target.position + Vector3.up * l);
            transform.rotation *= Quaternion.Euler(0, -y, 0);
        }

        /// Edit mode and previews: jump to a pose without waiting for LateUpdate to ease into it.
        public void Snap(bool focused)
        {
            Focused = focused; blend = focused ? 1f : 0f;
            if (focused) angle = focusAngle;
            Apply();
        }
    }
}
