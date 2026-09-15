using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Five pips per stat. Filled pips use the accent colour, empty ones a dim version of it.
    public sealed class StatBar : MonoBehaviour
    {
        [SerializeField] Image[] pips = System.Array.Empty<Image>();
        [SerializeField] Color on = new(0.96f, 0.78f, 0.35f);
        [SerializeField] Color off = new(1f, 1f, 1f, 0.18f);

        /// value is a pip count from -2..+2; the bar shows 3 + value out of 5.
        public void Set(int value)
        {
            int filled = Mathf.Clamp(3 + value, 1, pips.Length);
            for (int i = 0; i < pips.Length; i++) if (pips[i] != null) pips[i].color = i < filled ? on : off;
        }
    }
}
