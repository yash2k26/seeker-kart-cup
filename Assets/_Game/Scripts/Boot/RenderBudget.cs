using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KartCup
{
    /// The 3D's cost, fitted to the phone in hand. Every race opens with the drone's lap of the circuit (PodiumShow), and
    /// its frame rate is the measure: a phone that cannot hold the lap gives something back before the flag drops, two
    /// steps if it is far off. First 4x MSAA becomes 2x, then the 3D's resolution steps down. Only down, and only for this
    /// run of the app: a phone that got hot once is not stuck with a soft picture tomorrow. The ceiling is the shipped
    /// setting (LookSetup). FpsCounter's sweep on the Nothing Phone, Palm Bay (10 Oct 2026): MSAA off was worth 4 ms and
    /// a higher resolution cost 4.7; this phone had stepped to 0.62 and still ran 40, so MSAA goes first now.
    public static class RenderBudget
    {
        // 13 Oct 2026, the user: 60 fps is a promise. The ladder, cheapest loss first (the phone is fill-bound: MSAA off
        // was worth 4 ms, resolution 4.7): MSAA 4x to 2x, then off, then the 3D's resolution down, and on the night maps
        // fewer lamps lighting each piece of road. Scale 0 keeps the shipped scale.
        static readonly (int msaa, float scale, int lights)[] Ladder = { (2, 0f, 8), (1, 0f, 8), (1, 0.7f, 6), (1, 0.62f, 4), (1, 0.55f, 4) };
        const float Slow = 56f, VerySlow = 44f;                        // fps: under 56 gives a step back, under 44 two
        static float shipped = -1f; static int shippedMsaa = -1, shippedLights = -1;
        static int step = -1;                                          // -1: as shipped

        /// Whether this race's intro has been measured (PerfLog falls back to the countdown when it has not).
        public static bool MeasuredThisRace => measuredScene == UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        static int measuredScene = -1;
        public static string State => step < 0 ? "shipped" : $"step {step}";

        /// A measured frame rate (capped at the game's 60): the intro's, the countdown's, or ten seconds of racing (PerfLog).
        /// Only ever steps down, and only for this run of the app.
        public static void Report(float fps)
        {
            measuredScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            var urp = UniversalRenderPipeline.asset;
            if (urp == null) return;
            if (shipped < 0f) { shipped = urp.renderScale; shippedMsaa = urp.msaaSampleCount; shippedLights = urp.maxAdditionalLightsCount; }
            int was = step;
            if (fps < VerySlow) step += 2; else if (fps < Slow) step += 1;
            step = Mathf.Min(step, Ladder.Length - 1);
            if (step == was) return;
            var l = Ladder[step];
            urp.msaaSampleCount = Mathf.Min(shippedMsaa, l.msaa);
            urp.renderScale = l.scale > 0f ? Mathf.Min(shipped, l.scale) : shipped;
            urp.maxAdditionalLightsCount = Mathf.Min(shippedLights, l.lights);
            Debug.Log($"[budget] {fps:F0} fps: the 3D drawn at {urp.renderScale}, MSAA {urp.msaaSampleCount}x, {urp.maxAdditionalLightsCount} lamps a piece (step {step})");
        }

#if UNITY_EDITOR
        /// tools/unity.sh RenderBudget.Check: the stepping, on the URP asset, put back afterwards.
        public static void Check()
        {
            var urp = UniversalRenderPipeline.asset ?? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            float keep = urp.renderScale; bool ok = true;
            void Expect(float fps, float scale) { Report(fps); bool good = Mathf.Approximately(urp.renderScale, scale); ok &= good; Debug.Log($"[budget] check: {fps} fps -> {urp.renderScale} (want {scale}) {(good ? "ok" : "WRONG")}"); }
            try
            {
                urp.renderScale = 0.78f; shipped = -1f; step = -1;
                Expect(59f, 0.78f);      // fine: stays
                Expect(52f, 0.78f);      // under 60: MSAA gives first
                ok &= urp.msaaSampleCount == 2;
                Expect(52f, 0.78f);      // still: MSAA off
                ok &= urp.msaaSampleCount == 1;
                Expect(52f, 0.7f);       // then the resolution
                Expect(58f, 0.7f);       // fine now: never climbs back in a run
                Expect(30f, 0.55f);      // very slow: two steps, to the floor, with four lamps a piece
                ok &= urp.maxAdditionalLightsCount <= 4;
                Expect(20f, 0.55f);      // the floor
            }
            finally { urp.renderScale = keep; if (shippedMsaa > 0) urp.msaaSampleCount = shippedMsaa; if (shippedLights > 0) urp.maxAdditionalLightsCount = shippedLights; shipped = -1f; shippedMsaa = shippedLights = -1; step = -1; }
            Debug.Log(ok ? "[budget] check passed" : "[budget] check FAILED");
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(ok ? 0 : 1);
        }
#endif
    }
}
