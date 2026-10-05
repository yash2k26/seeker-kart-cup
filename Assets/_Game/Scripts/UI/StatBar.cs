using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Five pips per stat. Filled pips use the accent colour, empty ones a dim version of it. A pip that lights as
    /// the driver changes pops, left to right, so the difference between two drivers is something you see happen.
    public sealed class StatBar : MonoBehaviour
    {
        [SerializeField] Image[] pips = System.Array.Empty<Image>();
        [SerializeField] Color on = new(0.96f, 0.78f, 0.35f);
        [SerializeField] Color off = new(1f, 1f, 1f, 0.18f);

        int shown = -1;

        /// value is a pip count from -2..+2; the bar shows 3 + value out of 5.
        public void Set(int value) => SetCount(Mathf.Clamp(3 + value, 1, pips.Length));

        /// `filled` pips lit out of five (a kart's power, 0..5).
        public void SetCount(int filled)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].color = i < filled ? on : off;
                bool lit = shown >= 0 && i < filled && i >= shown;          // newly on since the last driver
                if (lit && Application.isPlaying && isActiveAndEnabled)
                {
                    var t = pips[i].transform;
                    Tween.StopAll(t);
                    t.localScale = new Vector3(0.6f, 0.6f, 1f);
                    Tween.Scale(t, Vector3.one, 0.26f, Ease.OutBack, startDelay: 0.035f * (i - shown), useUnscaledTime: true);
                }
            }
            shown = filled;
        }
    }
}
