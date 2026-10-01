using UnityEngine;

namespace KartCup
{
    /// On the burst effect: the gold light that turns the road to day for a moment, then the clean-up once the last
    /// glitter has gone out.
    public sealed class SunFlash : MonoBehaviour
    {
        public Light glow;
        public float fade = 0.9f, life = 4f;
        float t, start;

        void Start() { if (glow != null) start = glow.intensity; Destroy(gameObject, life); }

        void Update()
        {
            t += Time.deltaTime;
            if (glow != null) glow.intensity = start * Mathf.Pow(Mathf.Clamp01(1f - t / fade), 2f);
        }
    }
}
