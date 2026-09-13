using UnityEngine;

namespace KartCup
{
    /// 50–70 ms time-scale dip, player hits only, 1 s cooldown. Audio keeps playing.
    public sealed class HitStop : MonoBehaviour
    {
        static float until, cooldown;
        public static bool Paused;

        public static void Do(float realSeconds = 0.06f)
        {
            if (Time.unscaledTime < cooldown) return;
            until = Time.unscaledTime + realSeconds;
            cooldown = until + 1f;
            Time.timeScale = 0.05f;
        }

        void Update()
        {
            if (Time.timeScale < 1f && Time.unscaledTime >= until && !Paused) Time.timeScale = 1f;
        }
    }
}
