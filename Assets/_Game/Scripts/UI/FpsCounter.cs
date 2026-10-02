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

        /// What costs what, on the phone in hand. The race camera is taken off its rig and flown round the circuit at a
        /// kart's height (the waypoints, 70 m/s by the clock, uncapped). Each setting is measured against the game as it
        /// ships on the same stretch of road seconds apart, a stretch at a time round the lap, so a phone warming up
        /// over the run does not read as a setting's cost. Logs "[perf] <setting>: X -> Y fps (dt ms)" and puts
        /// everything back. A gate-test tool like the counter itself: it runs from the corner taps while the counter
        /// is showing. The plan is one setting a line, toggles separated by spaces (see Toggle), read from
        /// sweep.txt in the app's files folder if one has been pushed there, else DefaultPlan.
        const string DefaultPlan = "msaa=2\nmsaa=1\nmsaa=1 fxaa\nmsaa=2 scale=0.85\nbloom=0\ngrain=0 ca=0\nhard\nlights=4";

        IEnumerator Sweep()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            var camera = Camera.main;
            var root = GameObject.Find("Waypoints");
            if (urp == null || camera == null || root == null || root.transform.childCount < 8) { Debug.Log("[perf] no race camera or waypoints"); sweep = null; yield break; }

            int n = root.transform.childCount;
            var path = new Vector3[n]; var cum = new float[n + 1];
            for (int i = 0; i < n; i++) path[i] = root.transform.GetChild(i).position;
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(path[i], path[(i + 1) % n]);
            float lap = cum[n];
            Vector3 P(int k) => path[((k % n) + n) % n];
            Vector3 Along(float w)                                                    // Catmull-Rom through the waypoints
            {
                int i = Mathf.FloorToInt(w); float u = w - i;
                Vector3 a = P(i - 1), b = P(i), c = P(i + 1), d = P(i + 2);
                return 0.5f * (2f * b + (c - a) * u + (2f * a - 5f * b + 4f * c - d) * u * u + (3f * b - a - 3f * c + d) * u * u * u);
            }
            float Waypoint(float metres)
            {
                metres = Mathf.Repeat(metres, lap);
                int i = 0; while (i < n - 1 && cum[i + 1] < metres) i++;
                return i + (metres - cum[i]) / Mathf.Max(0.001f, cum[i + 1] - cum[i]);
            }
            void Pose(float metres)
            {
                float w = Waypoint(metres); var at = Along(w) + Vector3.up * 2.4f;
                camera.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Along(w + 0.5f) + Vector3.up * 1.2f - at, Vector3.up));
            }

            string plan = DefaultPlan;
            var file = System.IO.Path.Combine(Application.persistentDataPath, "sweep.txt");
            if (System.IO.File.Exists(file)) plan = System.IO.File.ReadAllText(file);
            var lines = plan.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();

            var rig = camera.GetComponentInParent<PowerslideKartPhysics.KartCamera>();
            if (rig != null) rig.enabled = false;
            int cap = Application.targetFrameRate; Application.targetFrameRate = 120;    // uncapped: the panel runs 120 Hz
            var keepPos = camera.transform.localPosition; var keepRot = camera.transform.localRotation;
            const float Speed = 70f; const int Stretches = 3;
            float stretch = lap / Stretches;
            Debug.Log($"[perf] {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}: {lap:F0} m lap, {Screen.width}x{Screen.height}, render scale {urp.renderScale}, msaa {urp.msaaSampleCount}, {lines.Length} settings");

            float t0 = 0f; int f0 = 0;
            foreach (var line in lines)
            {
                float baseT = 0f, testT = 0f; int baseF = 0, testF = 0;
                for (int k = 0; k < Stretches; k++)
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var undo = pass == 1 ? Toggle(line, urp, camera) : null;
                        step = pass == 1 ? line : "as shipped";
                        Pose(k * stretch);
                        yield return new WaitForSecondsRealtime(0.5f);                  // settle (a new MSAA or scale reallocates)
                        t0 = Time.realtimeSinceStartup; f0 = Time.frameCount;
                        for (float d = k * stretch; d < (k + 1) * stretch; d += Speed * Time.unscaledDeltaTime) { Pose(d); yield return null; }
                        float t = Time.realtimeSinceStartup - t0; int f = Time.frameCount - f0;
                        if (pass == 0) { baseT += t; baseF += f; } else { testT += t; testF += f; }
                        undo?.Invoke();
                    }
                float a = baseF / baseT, b = testF / testT;
                Debug.Log($"[perf] {line}: {a:F1} -> {b:F1} fps ({1000f / b - 1000f / a:+0.0;-0.0} ms)");
            }
            camera.transform.SetLocalPositionAndRotation(keepPos, keepRot);
            if (rig != null) rig.enabled = true;
            Application.targetFrameRate = cap;
            Debug.Log("[perf] done");
            step = ""; sweep = null;
        }

        /// One line of the plan applied; returns what puts it back. msaa=N scale=F fxaa bloom=0 bloompasses=N grain=0
        /// ca=0 vignette=0 post=0 hdr=0 hard shadowdist=M noshadows lights=N town=0 glows=0 far=M
        static System.Action Toggle(string line, UniversalRenderPipelineAsset urp, Camera camera)
        {
            var undo = new System.Collections.Generic.List<System.Action>();
            var data = camera.GetUniversalAdditionalCameraData();
            var profile = FindAnyObjectByType<Volume>()?.sharedProfile;
            void Fx<T>(bool on) where T : VolumeComponent { if (profile != null && profile.TryGet(out T fx)) { bool was = fx.active; fx.active = on; undo.Add(() => fx.active = was); } }
            foreach (var word in line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = word.Split('='); string key = kv[0], val = kv.Length > 1 ? kv[1] : "";
                float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float num);
                switch (key)
                {
                    case "msaa": { int was = urp.msaaSampleCount; urp.msaaSampleCount = (int)num; undo.Add(() => urp.msaaSampleCount = was); break; }
                    case "scale": { float was = urp.renderScale; urp.renderScale = num; undo.Add(() => urp.renderScale = was); break; }
                    case "fxaa": { var was = data.antialiasing; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing; undo.Add(() => data.antialiasing = was); break; }
                    case "bloom": Fx<Bloom>(num > 0); break;
                    case "bloompasses": if (profile != null && profile.TryGet(out Bloom bloom)) { int was = bloom.maxIterations.value; bloom.maxIterations.value = (int)num; undo.Add(() => bloom.maxIterations.value = was); } break;
                    case "grain": Fx<FilmGrain>(num > 0); break;
                    case "ca": Fx<ChromaticAberration>(num > 0); break;
                    case "vignette": Fx<Vignette>(num > 0); break;
                    case "post": { bool was = data.renderPostProcessing; data.renderPostProcessing = num > 0; undo.Add(() => data.renderPostProcessing = was); break; }
                    case "hdr": { bool was = urp.supportsHDR; urp.supportsHDR = num > 0; undo.Add(() => urp.supportsHDR = was); break; }
                    case "hard":
                        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.shadows == LightShadows.Soft))
                        { l.shadows = LightShadows.Hard; undo.Add(() => l.shadows = LightShadows.Soft); }
                        break;
                    case "shadowdist": { float was = urp.shadowDistance; urp.shadowDistance = num; undo.Add(() => urp.shadowDistance = was); break; }
                    case "noshadows": { float was = urp.shadowDistance; urp.shadowDistance = 0f; undo.Add(() => urp.shadowDistance = was); break; }
                    case "lights": { int was = urp.maxAdditionalLightsCount; urp.maxAdditionalLightsCount = (int)num; undo.Add(() => urp.maxAdditionalLightsCount = was); break; }
                    case "town": { var town = GameObject.Find("BeachTown"); if (town != null) { town.SetActive(false); undo.Add(() => town.SetActive(true)); } break; }
                    case "glows":
                        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name is "BeachHalos" or "BeachFlames" or "SkyLanterns" or "LampGlows"))
                        { r.enabled = false; undo.Add(() => r.enabled = true); }
                        break;
                    case "far":
                    {
                        // the beach town and the trackside palms, rocks and clutter, on a layer the camera stops drawing at `num` m
                        var town = GameObject.Find("BeachTown"); var dressing = GameObject.Find("Dressing");
                        if (dressing == null) break;
                        foreach (var r in dressing.GetComponentsInChildren<Renderer>())
                        {
                            bool far = town != null && r.transform.IsChildOf(town.transform) && r.name is not ("BeachHalos" or "BeachFlames" or "SkyLanterns")
                                       || r.name.StartsWith("palm") || r.name.StartsWith("rocks") || r.name.StartsWith("crate") || r.name.StartsWith("barrel") || r.name.StartsWith("patch");
                            if (!far) continue;
                            int was = r.gameObject.layer; var go = r.gameObject; go.layer = 29; undo.Add(() => go.layer = was);
                        }
                        var cull = camera.layerCullDistances; var d = (float[])cull.Clone(); d[29] = num; camera.layerCullDistances = d;
                        undo.Add(() => camera.layerCullDistances = cull);
                        break;
                    }
                }
            }
            return () => { for (int i = undo.Count - 1; i >= 0; i--) undo[i](); };
        }
    }
}
