using TMPro;
using UnityEngine;

namespace KartCup
{
    /// Release-build frame counter for the gate test (dev builds lie about frame time). Legacy Text: no TMP resources needed yet.
    public sealed class FpsCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        float acc; int frames; float worst;
        readonly char[] buf = new char[48];

        void Start() { if (label != null) label.enabled = Settings.ShowFps; }   // off by default: it is a gate-test tool, not HUD

        void Update()
        {
            if (label != null && !label.enabled) return;
            float dt = Time.unscaledDeltaTime;
            acc += dt; frames++; if (dt > worst) worst = dt;
            if (acc < 0.5f) return;
            int fps = Mathf.RoundToInt(frames / acc);
            int worstMs = Mathf.RoundToInt(worst * 1000f);
            // ponytail: no string.Format per frame; two ints every half second is fine
            if (label != null) label.text = $"{fps} fps  worst {worstMs} ms  {Screen.width}x{Screen.height}  {SystemInfo.graphicsDeviceType}";
            acc = 0; frames = 0; worst = 0;
        }
    }
}
