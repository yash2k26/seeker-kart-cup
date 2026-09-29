using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KartCup;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Drives the PLAYER kart with the AI follower and sends the result to a local Worker exactly as the phone would:
// times, laps, checkpoints and the 5 Hz path. It passes only if the Worker's referee (worker/src/referee.ts) believes
// a real physics drive, so it catches a referee that would refuse honest racers, not just a malformed payload.
// Run: cd worker && npx wrangler dev --port 8787   then   ./tools/unity.sh ContractTest.Run [-track N] [-worker URL]
public static class ContractTest
{
    static double start;
    static bool driving;
    static int lastCrumb = -1;
    static int totalMsAtFinish = -1;
    static double finishedAt = -1;

    [MenuItem("Build/Contract Test")]
    public static void Run()
    {
        EditorUtility.audioMasterMute = true;   // a headless test run must not play the game's sound through the laptop
        if (!EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.DisableDomainReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
        }
        // -track N drives circuit N instead of the first, so every circuit gets contract-checked, not just one.
        int track = 1;
        var args = System.Environment.GetCommandLineArgs();
        string worker = "http://127.0.0.1:8787";
        for (int i = 0; i < args.Length - 1; i++) { if (args[i] == "-track") int.TryParse(args[i + 1], out track); if (args[i] == "-worker") worker = args[i + 1]; }
        KartApi.BaseUrl = worker;       // the race session starts and submits against this Worker (domain reload is off: it sticks)
        finishedAt = -1; totalMsAtFinish = -1;
        Debug.Log($"[contract] circuit {track} against {worker}");
        Cup.End();             // same reason as Playtest: start every harness run from a clean cup
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup;
        driving = false;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        AudioListener.volume = 0f;   // and silent in play mode too, whatever the game's settings set it to
        double t = EditorApplication.timeSinceStartup - start;

        var race = Object.FindFirstObjectByType<RaceManager>();
        if (race == null || race.Player == null) return;

        // Hand the player kart to the waypoint follower so it completes real laps through the real checkpoints.
        if (!driving)
        {
            driving = true;
            var go = race.Player.gameObject;
            var mobile = go.GetComponent<KartInputMobile>();
            if (mobile != null) Object.DestroyImmediate(mobile);
            var ai = go.AddComponent<KartAi>();
            // WP00 sits just past the grid. "First found" was whichever waypoint Unity listed first: on Pine Ridge
            // that was behind the line, the kart drove the wrong way off the road and never saw a checkpoint.
            ai.SetTarget(Object.FindObjectsByType<BasicWaypoint>(FindObjectsSortMode.None).OrderBy(w => w.name).First());
            ai.skill = 1.0f;
            // RaceSession.OnFinish derives totalMs from Time.time in the finish frame and keeps no copy of it, so
            // stamp the same expression here. This listener is added after ResultsPanel's, so it runs in that frame.
            var session = Object.FindFirstObjectByType<RaceSession>();
            race.onFinish.AddListener(p =>
            {
                if (!p.isPlayer || session == null) return;
                float raceStart = (float)typeof(RaceSession).GetField("raceStart", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                totalMsAtFinish = Mathf.RoundToInt((Time.time - raceStart) * 1000f);
            });
            Debug.Log("[contract] player kart handed to the waypoint follower");
        }

        // A breadcrumb every 10 s: when a circuit fails, where the kart sat is the whole diagnosis.
        if ((int)(t / 10) != lastCrumb)
        {
            lastCrumb = (int)(t / 10);
            var k = race.Player.GetComponent<Kart>();
            Debug.Log($"[contract] t={t:F0}s cp={race.Player.checkpointIndex} lap={race.Player.lap} speed={k.velMag:F1} grounded={k.grounded} pos={race.Player.transform.position}");
        }
        bool done = race.Player.finished;
        if (!done && t < 150) return;
        // RaceSession sends 0.6 s after the line; wait for the Worker's answer (up to 10 s).
        var s = Object.FindFirstObjectByType<RaceSession>();
        if (done && finishedAt < 0) finishedAt = t;
        if (done && s != null && s.LastSubmit == null && t - finishedAt < 10) return;

        EditorApplication.update -= Tick;
        Report(race, done, t);
        EditorApplication.Exit(0);
    }

    static void Report(RaceManager race, bool finished, double t)
    {
        var session = Object.FindFirstObjectByType<RaceSession>();
        if (session == null) { Debug.LogError("[contract] FAIL: no RaceSession in the race scene"); return; }

        // The lists are private; this is an editor-only probe, so read them rather than widen the runtime API.
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var lapMs = (List<int>)typeof(RaceSession).GetField("lapMs", flags).GetValue(session);
        var cpMs = (List<int>)typeof(RaceSession).GetField("checkpointMs", flags).GetValue(session);

        int gates = race.checkpoints.Length;
        int totalMs = lapMs.Sum();
        Debug.Log($"[contract] finished={finished} after {t:F0}s wall · track gates={gates} · laps={race.totalLaps}");
        Debug.Log($"[contract] lapMs   n={lapMs.Count} values={string.Join(",", lapMs)}");
        Debug.Log($"[contract] cpMs    n={cpMs.Count} first={(cpMs.Count > 0 ? cpMs[0] : -1)} last={(cpMs.Count > 0 ? cpMs[^1] : -1)}");

        var path = (List<float[]>)typeof(RaceSession).GetField("path", flags).GetValue(session);
        Check("lap count", lapMs.Count == race.totalLaps, $"{lapMs.Count} != {race.totalLaps}");
        Check("checkpoint count", cpMs.Count == (gates - 1) * race.totalLaps, $"{cpMs.Count} != {(gates - 1) * race.totalLaps}");
        Check("checkpoints monotonic", cpMs.Zip(cpMs.Skip(1), (a, b) => b > a).All(x => x), "not strictly increasing");
        var gaps = cpMs.Select((c, i) => i == 0 ? c : c - cpMs[i - 1]).ToList();
        Check("laps != total", totalMsAtFinish >= 0 && Mathf.Abs(lapMs.Sum() - totalMsAtFinish) <= 1500, $"sum {lapMs.Sum()} vs totalMs {totalMsAtFinish}");
        // what the referee will look at: the fastest second of the drive, the fastest lap as an average speed
        float best = 0f;
        for (int i = 0, j = 0; i < path.Count; i++)
        {
            while (j < i && path[i][0] - path[j + 1][0] >= 1f) j++;
            if (path[i][0] - path[j][0] < 1f) continue;
            float d = 0f; for (int k = j + 1; k <= i; k++) d += Vector2.Distance(new Vector2(path[k][1], path[k][3]), new Vector2(path[k - 1][1], path[k - 1][3]));
            best = Mathf.Max(best, d / (path[i][0] - path[j][0]));
        }
        Debug.Log($"[contract] path n={path.Count} · fastest 1 s {best:F1} m/s (referee cap 62) · Worker says: {session.LastSubmit ?? "<no answer>"}");
        Check("the Worker's referee accepts the drive", session.LastSubmit == "accepted", session.LastSubmit ?? "no answer from the Worker");
        Debug.Log($"[contract] gaps min={(gaps.Count > 0 ? gaps.Min() : 0)} max={(gaps.Count > 0 ? gaps.Max() : 0)} · laps sum={totalMs} · totalMs {totalMsAtFinish}");
        Debug.Log($"[contract] cpMs all={string.Join(",", cpMs)}");
    }

    static void Check(string rule, bool ok, string detail) => Debug.Log($"[contract] {(ok ? "PASS" : "FAIL")} {rule}{(ok ? "" : " — " + detail)}");
}
