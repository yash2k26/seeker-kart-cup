using UnityEngine;

namespace KartCup
{
    /// Frame times while racing, to the log every ten seconds and at the line: the phone's own word on lag, readable
    /// over adb logcat without the FPS setting on (FpsCounter only counts while its label shows).
    ///     [perf] beach 0:20  56 fps  worst 38 ms  2 frames over 33 ms
    public sealed class PerfLog : MonoBehaviour
    {
        float acc, worst; int frames, hitches; bool told;
        float countAcc; int countFrames; bool reported;
        float raceAcc, raceWorst; int raceFrames, raceHitches;     // the whole race, for the Worker

        void Update()
        {
            var race = RaceManager.Instance;
            if (race == null) return;
            // The 3-2-1 is the render budget's second chance: its first is the drone's lap, and a tap skips that before it
            // proves anything (on the phone with the intro skipped, Palm Bay ran 42 fps at the full 4x MSAA all session).
            if (race.state == RaceManager.State.Countdown) { countAcc += Time.unscaledDeltaTime; countFrames++; return; }
            if (!reported && race.state == RaceManager.State.Racing)
            {
                reported = true;
                if (!RenderBudget.MeasuredThisRace && countAcc > 1.5f) RenderBudget.Report(countFrames / countAcc);
            }
            if (race.state == RaceManager.State.Finished) { if (!told && frames > 0) Flush(race); if (!told) Send(); told = true; return; }
            if (race.state != RaceManager.State.Racing) return;
            float dt = Time.unscaledDeltaTime;
            acc += dt; frames++;
            if (dt > worst) worst = dt;
            if (dt > 1f / 30f) hitches++;
            raceAcc += dt; raceFrames++; if (dt > raceWorst) raceWorst = dt; if (dt > 1f / 30f) raceHitches++;
            if (acc >= 10f) Flush(race);
        }

        void Flush(RaceManager race)
        {
            if (acc >= 9f) RenderBudget.Report(frames / acc);      // racing is the real load: the intro can flatter it
            Debug.Log($"[perf] {Cup.TrackId} {RaceHud.Clock(race.raceTime).Replace("<mspace=0.56em>", "").Replace("</mspace>", "")}  {frames / Mathf.Max(acc, 0.001f):0} fps  worst {worst * 1000f:0} ms  {hitches} frames over 33 ms");
            acc = 0f; worst = 0f; frames = 0; hitches = 0;
        }

        /// The race's frame rate to the Worker's events, so every race on every phone measures the game (13 Oct 2026: no
        /// more guessing which map holds 60 from one phone on a cable).
        void Send()
        {
            if (Application.isEditor || raceFrames < 60) return;
            var urp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            KartApi.Event("perf", new
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                fps = Mathf.Round(raceFrames / raceAcc), worstMs = Mathf.Round(raceWorst * 1000f), hitches = raceHitches, seconds = Mathf.Round(raceAcc),
                scale = urp != null ? urp.renderScale : 0f, msaa = urp != null ? urp.msaaSampleCount : 0, budget = RenderBudget.State,
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
            });
        }
    }

    /// The frame-rate sweep (13 Oct 2026): kartcup://r/perf/Race1L loads that race scene straight from any screen, on a
    /// phone whose autopilot file is there (adb puts it), so every circuit and its long layout can be driven and measured
    /// without taps. Anywhere else the link does nothing: it is not a way round the fuel.
    static class PerfLink
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook() { Application.deepLinkActivated -= Open; Application.deepLinkActivated += Open; }

        static void Open(string url)
        {
            int at = url == null ? -1 : url.IndexOf("/perf/", System.StringComparison.OrdinalIgnoreCase);
            if (at < 0 || !RaceSpawner.DeviceAutopilot) return;
            var scene = url.Substring(at + 6).Trim('/').Split('?', '#')[0];
            if (!scene.StartsWith("Race") || !Application.CanStreamedLevelBeLoaded(scene)) return;
            Debug.Log("[perf] sweep: loading " + scene);
            Cup.End(); RaceMode.Clear(); Weekly.Racing = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
    }
}
