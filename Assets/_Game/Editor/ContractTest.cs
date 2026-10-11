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
        introAt = countdownAt = podiumAt = cardAt = introFrame = podiumFrame = -1; podiumRacers = 0; tunnelFrame = false; surveyAt = -1; surveyN = 0;
        // arm a starting item the way the shop does: one rocket in the pack, picked as the loadout
        startItem = "<not checked>";
        KartCup.Inventory.Add("rocket", 1); KartCup.Inventory.Loadout = "rocket";
        packBefore = KartCup.Inventory.Count("rocket");
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
        Flow(race, t);

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
        // RaceSession sends 0.6 s after the line; wait for the Worker's answer (up to 10 s), and for the podium and
        // the results card that follows it.
        var s = Object.FindFirstObjectByType<RaceSession>();
        if (done && finishedAt < 0) finishedAt = t;
        if (done && s != null && s.LastSubmit == null && t - finishedAt < 10) return;
        if (done && t - finishedAt < 9) return;
        if (done && cardAt < 0 && t - finishedAt < 15) return;     // the ceremony runs ~9 s from 2.6 s after the line (PodiumShow.CeremonySeconds)

        EditorApplication.update -= Tick;
        Report(race, done, t);
        EditorApplication.Exit(0);
    }

    // ---- the ceremony around the race: prize shot, 3-2-1, race, podium, results card ----
    static double introAt = -1, countdownAt = -1, podiumAt = -1, cardAt = -1, introFrame = -1, podiumFrame = -1;
    static bool tunnelFrame;
    static double surveyAt = -1; static int surveyN;   // -survey: a frame every 2 s of the drive, to look round the whole lap
    static string startItem = "<not checked>";
    static int packBefore;
    static int podiumRacers;

    static void Flow(RaceManager race, double t)
    {
        var show = PodiumShow.Instance;
        bool active = show != null && show.Active;
        if (introAt < 0 && active && race.state == RaceManager.State.Idle) { introAt = t; Debug.Log($"[contract] flow: prize shot at {t:F1}s"); }
        if (introAt > 0 && introFrame < 0 && t - introAt > 2.2) { introFrame = t; Frame("flow-intro"); }
        if (race.state == RaceManager.State.Racing && surveyN < 24 && t - surveyAt > 2.0 && System.Environment.GetCommandLineArgs().Contains("-survey"))
        { surveyAt = t; Frame($"lap-{surveyN++:00}"); Debug.Log($"[contract] survey {surveyN - 1} at {race.Player.transform.position}"); }
        // one frame from inside the tunnel, if the map has one: the race camera, mid-drive
        if (!tunnelFrame && race.state == RaceManager.State.Racing)
        {
            var tunnel = GameObject.Find("Tunnel");
            var inside = tunnel != null && tunnel.GetComponent<Renderer>() != null && tunnel.GetComponent<Renderer>().bounds.Contains(race.Player.transform.position);
            if (inside && Vector3.Distance(race.Player.transform.position, tunnel.GetComponent<Renderer>().bounds.center) < 22f) { tunnelFrame = true; Frame("flow-tunnel"); }
        }
        if (countdownAt < 0 && race.state == RaceManager.State.Countdown)
        {
            countdownAt = t; Debug.Log($"[contract] flow: 3-2-1 at {t:F1}s, prize shot still up: {active}");
            var held = race.Player.GetComponent<ItemCaster>();
            startItem = held != null && held.item != null && held.ammo > 0 ? held.item.itemName : "<none>";
        }
        if (race.Player.finished && podiumAt < 0 && active)
        {
            podiumAt = t;
            podiumRacers = Object.FindObjectsByType<PodiumPose>(FindObjectsSortMode.None).Length;
            Debug.Log($"[contract] flow: podium at {t:F1}s with {podiumRacers} racers");
        }
        if (podiumAt > 0 && podiumFrame < 0 && t - podiumAt > 2.5) { podiumFrame = t; Frame("flow-podium"); }
        var card = Object.FindFirstObjectByType<ResultsPanel>();
        var dim = card != null ? card.transform.Find("Dim") : null;
        if (podiumAt > 0 && cardAt < 0 && dim != null && dim.gameObject.activeInHierarchy) { cardAt = t; Debug.Log($"[contract] flow: results card at {t:F1}s"); }
    }

    /// What the topmost camera sees (the podium's while a shot is up), without the overlay UI.
    static void Frame(string name)
    {
        var cam = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled).OrderByDescending(c => c.depth).FirstOrDefault();
        if (cam == null) return;
        int w = 1600, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var prevTarget = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevTarget;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        System.IO.Directory.CreateDirectory("Builds");
        System.IO.File.WriteAllBytes($"Builds/{name}.png", tex.EncodeToPNG());
        Debug.Log($"[contract] flow: wrote Builds/{name}.png from {cam.name}");
    }

    static void Report(RaceManager race, bool finished, double t)
    {
        Check("the prize shot plays before the race", introAt > 0 && countdownAt > introAt + 3.0, $"shot {introAt:F1}s, 3-2-1 {countdownAt:F1}s");
        Check("the shop's starting item is on the kart at the grid, and out of the pack", startItem == "Rocket" && KartCup.Inventory.Count("rocket") == packBefore - 1, $"held {startItem}, pack {packBefore} -> {KartCup.Inventory.Count("rocket")}");
        KartCup.Inventory.Loadout = "";
        Check("the podium plays after the finish, with three racers", podiumAt > 0 && podiumRacers == 3, $"podium {podiumAt:F1}s, racers {podiumRacers}");
        Check("the results card follows the podium", cardAt > podiumAt + 3.0, $"podium {podiumAt:F1}s, card {cardAt:F1}s");
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
