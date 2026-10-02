using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            // four taps in the screen's top-left corner inside two and a half seconds: the perf sweep (below)
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame && sweep == null)
            {
                var at = pointer.position.ReadValue();
                if (at.x < Screen.width * 0.12f && at.y > Screen.height * 0.8f)
                {
                    taps[tapN++ % taps.Length] = Time.unscaledTime;
                    if (tapN >= taps.Length && Time.unscaledTime - taps.Min() < 2.5f) { tapN = 0; sweep = StartCoroutine(Sweep()); }
                }
            }
            float dt = Time.unscaledDeltaTime;
            acc += dt; frames++; if (dt > worst) worst = dt;
            if (acc < 0.5f) return;
            int fps = Mathf.RoundToInt(frames / acc);
            int worstMs = Mathf.RoundToInt(worst * 1000f);
            // ponytail: no string.Format per frame; two ints every half second is fine
            if (label != null) label.text = $"{fps} fps  worst {worstMs} ms  {Screen.width}x{Screen.height}  {SystemInfo.graphicsDeviceType}  {step}";
            acc = 0; frames = 0; worst = 0;
        }

        readonly float[] taps = new float[4];
        int tapN;
        Coroutine sweep;
        string step = "";

        /// What costs what, on the phone in hand: holds each of a list of render settings for six seconds (two to
        /// settle, four measured), logs the frame rate as "[perf] <setting>: N fps", and puts everything back. A
        /// gate-test tool like the counter itself; it only runs from the corner taps while the counter is showing.
        IEnumerator Sweep()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var cam = Camera.main != null ? Camera.main.GetUniversalAdditionalCameraData() : null;
            if (urp == null || cam == null) { sweep = null; yield break; }
            float scale = urp.renderScale, shadows = urp.shadowDistance; int msaa = urp.msaaSampleCount, lights = urp.maxAdditionalLightsCount;
            var filter = urp.upscalingFilter;
            var town = GameObject.Find("BeachTown");
            Renderer[] Named(params string[] names) => FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => names.Contains(r.name)).ToArray();
            var townGlows = Named("BeachHalos", "BeachFlames", "SkyLanterns"); var lampGlows = Named("LampGlows");
            void Show(Renderer[] rs, bool on) { foreach (var r in rs) if (r != null) r.enabled = on; }
            var steps = new (string name, System.Action on, System.Action off)[]
            {
                ("as shipped", () => { }, () => { }),
                ("render scale 0.65", () => urp.renderScale = 0.65f, () => urp.renderScale = scale),
                ("render scale 0.9", () => urp.renderScale = 0.9f, () => urp.renderScale = scale),
                ("FSR", () => urp.upscalingFilter = UpscalingFilterSelection.FSR, () => urp.upscalingFilter = filter),
                ("MSAA 2x", () => urp.msaaSampleCount = 2, () => urp.msaaSampleCount = msaa),
                ("no shadows", () => urp.shadowDistance = 0f, () => urp.shadowDistance = shadows),
                ("no MSAA", () => urp.msaaSampleCount = 1, () => urp.msaaSampleCount = msaa),
                ("no town glows", () => Show(townGlows, false), () => Show(townGlows, true)),
                ("no beach town", () => { if (town != null) town.SetActive(false); }, () => { if (town != null) town.SetActive(true); }),
                ("no lamp glows", () => Show(lampGlows, false), () => Show(lampGlows, true)),
                ("no extra lights", () => urp.maxAdditionalLightsCount = 0, () => urp.maxAdditionalLightsCount = lights),
                ("no post", () => cam.renderPostProcessing = false, () => cam.renderPostProcessing = true),
                ("as shipped, again", () => { }, () => { }),      // if this is not the first line's number, the scene changed under the sweep
            };
            foreach (var s in steps)
            {
                step = s.name; s.on();
                yield return new WaitForSecondsRealtime(2f);
                int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(4f);
                Debug.Log($"[perf] {s.name}: {(Time.frameCount - f0) / (Time.realtimeSinceStartup - t0):F1} fps");
                s.off();
            }
            Debug.Log("[perf] done");
            step = ""; sweep = null;
        }
    }
}
