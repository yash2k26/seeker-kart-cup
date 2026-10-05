using UnityEngine;

namespace KartCup
{
    /// Moves a driver's eyes. The head FBX carries a window mesh over each eye opening (eye_R / eye_L, built by
    /// tools/blender heads.eye_windows), mapped onto a sprite of that eye's white and iris with white room round it.
    /// Sliding a window's texture offset moves the iris inside the opening, and the window's edge clips it like a lid.
    /// A glance somewhere new every second or two, plus a look into the turn; both eyes together. A blink every few
    /// seconds (now and then a double, sometimes with a glance) shows the lid meshes (lid_R / lid_L) over the windows
    /// for a moment: they are the skin round the eye with the lash along the bottom, hidden the rest of the time.
    /// Heads under shades have no windows, and this turns itself off.
    public sealed class EyeLook : MonoBehaviour
    {
        // Fractions of each window's half-size. The sprite has 0.6 / 0.3 of white round its window (heads.py
        // EYE_ROOM): keep these under that, or the slide runs off the sprite.
        [SerializeField] float sideways = 0.5f, upward = 0.2f, steerLook = 0.8f;
        [SerializeField] Renderer[] eyes;      // set by Bind() in the editor: the meshes are not readable in a player
        [SerializeField] Vector2[] halfUv;     // each window's half-size in UV
        [SerializeField] Renderer[] lids;      // set by Bind(), left disabled: an open eye in every preview and portrait
        [SerializeField] float blinkTime = 0.11f;

        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        MaterialPropertyBlock block;
        BlockyDriverPose pose;
        Vector2 target, gaze, shown = new Vector2(9f, 9f);
        float next, nextBlink, blinkUntil;
        bool closed;

#if UNITY_EDITOR
        /// ContentSetup: find the windows under this head and measure their UVs. Returns how many.
        public int Bind()
        {
            var found = new System.Collections.Generic.List<Renderer>();
            var half = new System.Collections.Generic.List<Vector2>();
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("eye_") || mf.sharedMesh == null) continue;
                var uv = mf.sharedMesh.uv;
                if (uv.Length == 0) continue;
                Vector2 lo = uv[0], hi = uv[0];
                foreach (var p in uv) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
                found.Add(mf.GetComponent<Renderer>());
                half.Add((hi - lo) * 0.5f);
            }
            eyes = found.ToArray(); halfUv = half.ToArray();
            var lidList = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("lid_")) { r.enabled = false; lidList.Add(r); }
            lids = lidList.ToArray();
            return eyes.Length;
        }
#endif

        void Awake()
        {
            if (eyes == null || eyes.Length == 0) { enabled = false; return; }
            block = new MaterialPropertyBlock();
            pose = GetComponentInParent<BlockyDriverPose>();
            next = Time.time + Random.Range(0.3f, 1.5f);       // drivers glance out of step with each other
            nextBlink = Time.time + Random.Range(0.5f, 4f);
        }

        /// The driver just made a sound (Voices): the eyes shut for `seconds` (a laugh squeezes them shut, anything
        /// else is a blink) and come back looking straight out.
        public void React(float seconds)
        {
            if (!enabled) return;
            blinkUntil = Time.time + seconds;
            nextBlink = Time.time + seconds + Random.Range(1.5f, 3.5f);
            target = Vector2.zero; next = Time.time + seconds + 0.6f;
        }

        void Blink(float now)
        {
            blinkUntil = now + blinkTime;
            nextBlink = now + (Random.value < 0.15f ? blinkTime + 0.14f : Random.Range(2.2f, 5.5f));   // now and then a double
        }

        void LateUpdate()
        {
            float now = Time.time;
            if (now >= next)
            {
                // a glance: back to the middle a third of the time, else somewhere in the eye, mostly sideways
                target = Random.value < 0.35f ? Vector2.zero : new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 0.6f));
                next = now + Random.Range(0.7f, 2.4f);
                if (Random.value < 0.25f && now > blinkUntil + 1f) Blink(now);    // people often blink as they look away
            }
            if (now >= nextBlink) Blink(now);
            bool shut = now < blinkUntil;
            if (shut != closed)
            {
                closed = shut;
                if (lids != null) foreach (var l in lids) if (l != null) l.enabled = shut;
            }
            var want = target;                                  // +x: toward his left (the sprite's +u)
            if (pose != null) want.x = Mathf.Clamp(want.x - pose.Steer * steerLook, -1f, 1f);   // a right turn looks right
            gaze = Vector2.Lerp(gaze, want, 1f - Mathf.Exp(-28f * Time.deltaTime));              // a saccade: quick, no overshoot
            if ((gaze - shown).sqrMagnitude < 1e-5f) return;
            shown = gaze;
            for (int i = 0; i < eyes.Length; i++)
            {
                // the texture offset slides the sprite the other way: the iris moves by +d inside the window
                eyes[i].GetPropertyBlock(block);
                block.SetVector(BaseMapSt, new Vector4(1f, 1f, -gaze.x * sideways * halfUv[i].x, -gaze.y * upward * halfUv[i].y));
                eyes[i].SetPropertyBlock(block);
            }
        }
    }
}
