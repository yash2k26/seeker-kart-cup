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
            if (race.state == RaceManager.State.Finished) { if (!told && frames > 0) Flush(race); told = true; return; }
            if (race.state != RaceManager.State.Racing) return;
            float dt = Time.unscaledDeltaTime;
            acc += dt; frames++;
            if (dt > worst) worst = dt;
            if (dt > 1f / 30f) hitches++;
            if (acc >= 10f) Flush(race);
        }

        void Flush(RaceManager race)
        {
            Debug.Log($"[perf] {Cup.TrackId} {RaceHud.Clock(race.raceTime).Replace("<mspace=0.56em>", "").Replace("</mspace>", "")}  {frames / Mathf.Max(acc, 0.001f):0} fps  worst {worst * 1000f:0} ms  {hitches} frames over 33 ms");
            acc = 0f; worst = 0f; frames = 0; hitches = 0;
        }
    }
}
