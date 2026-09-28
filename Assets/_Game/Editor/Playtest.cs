using System.IO;
using System.Linq;
using KartCup;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Drives the race scene in editor play mode and reports what actually happened: did the karts move, did the AI
// follow the waypoints, did anyone cross a checkpoint. Catches the whole class of bugs a static render cannot.
// Run: Unity -batchmode -projectPath . -executeMethod Playtest.Run -logFile -   (no -quit: it exits itself)
public static class Playtest
{
    static double start, captureAt = -1;
    static int shot, track = 1, cam = -1;
    static bool camSet;
    static readonly double[] shotTimes = { 5, 12, 22, 34 };

    [MenuItem("Build/Playtest 30s")]
    public static void Run()
    {
        // Keep these statics alive across the transition. Writing the setting dirties an asset, so only touch it when it differs.
        if (!EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.DisableDomainReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
        }
        KartCup.Cup.End();     // PlayerPrefs survive batch runs; without this each run scores into the last run's cup
        // -track N drives circuit N (default 1), like ContractTest, so every map gets in-race frames.
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) { if (args[i] == "-track") int.TryParse(args[i + 1], out track); if (args[i] == "-cam") int.TryParse(args[i + 1], out cam); }
        camSet = false;
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup;
        shot = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double t = EditorApplication.timeSinceStartup - start;
        if (!camSet && cam >= 0 && t > 1) { camSet = true; var a = Object.FindFirstObjectByType<CameraAngles>(); a?.Set(cam); a?.Toggle(); }   // picker open for the first shots

        if (shot < shotTimes.Length && captureAt < 0 && t >= shotTimes[shot])
        {
            var pause = Object.FindFirstObjectByType<PauseMenu>();
            var race = Object.FindFirstObjectByType<RaceManager>();
            if (shot == 2 && pause != null) pause.Pause();                                   // overlay in the frame
            if (shot == 3 && race != null && race.Player != null) race.Finish(race.Player);   // results card in the frame
            captureAt = t + 0.6;                                                              // after the entrance has landed
        }
        if (captureAt >= 0 && t >= captureAt)
        {
            Capture($"play{track}-{shot}");
            Report(t);
            var pause = Object.FindFirstObjectByType<PauseMenu>();
            if (shot == 2 && pause != null) pause.Resume();
            captureAt = -1;
            shot++;
        }
        if (t < 38) return;

        EditorApplication.update -= Tick;
        Report(t);
        Debug.Log("[playtest] done");
        EditorApplication.Exit(0);
    }

    static void Report(double t)
    {
        var race = Object.FindFirstObjectByType<RaceManager>();
        var racers = Object.FindObjectsByType<KartProgress>(FindObjectsSortMode.None).OrderByDescending(r => r.score).ToArray();
        if (race == null) { Debug.LogError("[playtest] no RaceManager"); return; }
        var audio = Object.FindFirstObjectByType<KartCup.AudioDirector>();
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        Debug.Log($"[playtest] t={t:F0}s state={race.state} clock={race.raceTime:F1} racers={racers.Length} " +
                  $"audio={(audio != null ? "ok" : "MISSING")} sources={sources.Length} playing={sources.Count(s2 => s2.isPlaying)}");
        foreach (var r in racers)
        {
            var kart = r.GetComponent<Kart>();
            Debug.Log($"[playtest]   {(r.isPlayer ? "P" : " ")} {r.racerName,-8} lap={r.lap} cp={r.checkpointIndex,2} score={r.score,6:F2} speed={kart.velMag,5:F1} grounded={kart.grounded} pos={r.transform.position}");
        }
    }

    static void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        // Screen-space-overlay canvases never land in a RenderTexture: borrow the camera for the shot.
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();
        int w = 1780, h = 800;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = prevTarget;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/{name}.png", tex.EncodeToPNG());
    }
}
