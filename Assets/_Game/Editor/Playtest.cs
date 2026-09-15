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
    static double start;
    static int shot;
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
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup;
        shot = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double t = EditorApplication.timeSinceStartup - start;

        if (shot < shotTimes.Length && t >= shotTimes[shot])
        {
            Capture($"play{shot}");
            Report(t);
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
        Debug.Log($"[playtest] t={t:F0}s state={race.state} clock={race.raceTime:F1} racers={racers.Length}");
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
