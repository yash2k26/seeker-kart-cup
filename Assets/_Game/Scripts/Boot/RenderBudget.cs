using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KartCup
{
    /// The 3D's resolution, fitted to the phone in hand. Every race opens with the drone's lap of the circuit
    /// (PodiumShow), and its frame rate is the measure: a phone that cannot hold the lap steps the 3D's resolution down
    /// before the flag drops, two steps if it is far off. Only down, and only for this run of the app: a phone that got
    /// hot once is not stuck with a soft picture tomorrow. The ceiling is the shipped scale (LookSetup sets it).
    public static class RenderBudget
    {
        static readonly float[] Steps = { 0.7f, 0.62f, 0.55f };     // below the shipped scale, in order
        const float Slow = 46f, VerySlow = 36f;                       // fps over the drone lap
        static float shipped = -1f;
        static int step = -1;                                          // -1: the shipped scale

        /// The drone lap's frame rate (capped at the game's 60).
        public static void Report(float fps)
        {
            var urp = UniversalRenderPipeline.asset;
            if (urp == null) return;
            if (shipped < 0f) shipped = urp.renderScale;
            int was = step;
            if (fps < VerySlow) step += 2; else if (fps < Slow) step += 1;
            step = Mathf.Min(step, Steps.Length - 1);
            if (step == was) return;
            urp.renderScale = step < 0 ? shipped : Mathf.Min(shipped, Steps[step]);
            Debug.Log($"[budget] drone lap at {fps:F0} fps: the 3D drawn at {urp.renderScale} (was {(was < 0 ? shipped : Steps[was])})");
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
                Expect(58f, 0.78f);      // fine: stays
                Expect(44f, 0.7f);       // slow: one step
                Expect(50f, 0.7f);       // fine now: never climbs back in a run
                Expect(30f, 0.55f);      // very slow: two steps
                Expect(20f, 0.55f);      // the floor
            }
            finally { urp.renderScale = keep; shipped = -1f; step = -1; }
            Debug.Log(ok ? "[budget] check passed" : "[budget] check FAILED");
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(ok ? 0 : 1);
        }
#endif
    }
}
