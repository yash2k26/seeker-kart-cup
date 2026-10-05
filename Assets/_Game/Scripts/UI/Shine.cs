using UnityEngine;

namespace KartCup
{
    /// The primary action catches the light now and then: a soft band sweeps across its face every few seconds,
    /// clipped to the pill by the Mask on the button. It says "this one" without a second accent colour.
    public sealed class Shine : MonoBehaviour
    {
        [SerializeField] RectTransform band;
        [SerializeField] float every = 3.4f, sweep = 0.6f, first = 0.5f;
        float t;

        void OnEnable() { t = -first; Place(-1f); }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t > every) t -= every;
            Place(t < 0f ? -1f : Mathf.Clamp01(t / sweep) * 2f - 1f);
        }

        void Place(float u)   // -1 .. 1: from just off the left end to just off the right
        {
            if (band == null) return;
            float w = ((RectTransform)transform).rect.width * 0.5f + band.rect.width;
            band.anchoredPosition = new Vector2(u * w, band.anchoredPosition.y);
        }
    }
}
