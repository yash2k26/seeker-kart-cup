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
    static bool camSet, noHud, vista, mobile;
    static string look = "Lighthouse", item;   // -item "Sun Bomb": the player throws it after GO and three frames follow it
    static int itemShot;
    static double[] itemTimes = { 0.5, 2.15, 2.55, 2.9, 3.8 };   // after the throw: in the air, burning, the burst, its wave, the glitter
    static double thrownAt = -1;
    static bool armed, heldShot;
    static Vector3 itemFrom, itemAt;   // a second camera for the item frames: beside the road, on where it lands
    static int prevQuality;
    static readonly double[] shotTimes = { 5, 12, 22, 34 };
    static readonly double[] introTimes = { 2.2, 3.4, 4.6, 7.2 };   // the drone's lap before the flag, and a turbo pad from above
    static int introShot;

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
        noHud = args.Contains("-nohud");     // a clean frame of the world, no HUD over it
        vista = args.Contains("-vista");     // and, per shot, a second frame from the player's spot toward the lighthouse
        mobile = args.Contains("-mobile");   // the phone's quality tier (Forward, eight lights per object), restored on exit
        look = "Lighthouse";
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-look") look = args[i + 1];   // what the vista frames look at
        item = null; itemShot = 0; thrownAt = -1; armed = false; heldShot = false;
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-item") item = args[i + 1];
        prevQuality = QualitySettings.GetQualityLevel();
        if (mobile) QualitySettings.SetQualityLevel(0, true);
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup;
        shot = 0; introShot = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double t = EditorApplication.timeSinceStartup - start;
        if (!camSet && cam >= 0 && t > 1) { camSet = true; var a = Object.FindFirstObjectByType<CameraAngles>(); a?.Set(cam); a?.Toggle(); }   // picker open for the first shots

        if (introShot < introTimes.Length && t >= introTimes[introShot])
        {
            Capture($"intro{track}-{introShot}");
            var pads = GameObject.Find("TurboPads");
            if (introShot == 0 && pads != null && pads.transform.childCount > 0)
            {
                var pad = pads.transform.GetChild(0);
                itemFrom = pad.position - pad.forward * 16f + Vector3.up * 7f + pad.right * 5f; itemAt = pad.position;
                SideShot($"pad{track}");
                Debug.Log($"[playtest] {pads.transform.childCount} turbo pad(s), first at {pad.position}");
            }
            if (introShot == 0)
            {
                // every corner wall must stop what comes at it from the road (a mesh collider only stops its faces' front)
                var walls = GameObject.Find("Barriers"); int stops = 0, outside = 0, all = 0;
                if (walls != null)
                    foreach (var wall in walls.GetComponentsInChildren<MeshCollider>())
                    {
                        // the collider's own slab (TrackSetup.Wall): per sample its foot, top, and the two set half a metre back.
                        // (The drawn mesh is no use here: static batching has replaced it with the scene's combined one.)
                        var v = wall.sharedMesh.vertices; int mid = v.Length / 8 * 4;
                        Vector3 foot = wall.transform.TransformPoint(v[mid]), toRoad = wall.transform.TransformDirection(v[mid] - v[mid + 2]).normalized;
                        all++; if (wall.Raycast(new Ray(foot + toRoad * 3f + Vector3.up * 0.9f, -toRoad), out _, 6f)) stops++;
                        if (wall.Raycast(new Ray(foot - toRoad * 3.5f + Vector3.up * 0.9f, toRoad), out _, 6f)) outside++;
                    }
                Debug.Log($"[playtest] barriers: {stops} of {all} stop a kart coming from the road, {outside} from behind");
            }
            introShot++;
        }
        if (shot < shotTimes.Length && captureAt < 0 && t >= shotTimes[shot])
        {
            var pause = Object.FindFirstObjectByType<PauseMenu>();
            var race = Object.FindFirstObjectByType<RaceManager>();
            if (shot == 2 && pause != null) pause.Pause();                                   // overlay in the frame
            if (shot == 3 && race != null && race.Player != null) race.Finish(race.Player);   // results card in the frame
            captureAt = t + 0.6;                                                              // after the entrance has landed
        }
        // the item is handed over a little before it is used, so the kart is seen carrying it (pod up, bomb in the cradle)
        if (item != null && !armed && t >= 9.6)
        {
            armed = true;
            var r0 = Object.FindFirstObjectByType<RaceManager>();
            var c0 = r0 != null && r0.Player != null ? r0.Player.GetComponent<ItemCaster>() : null;
            var d0 = Object.FindFirstObjectByType<ItemManager>()?.GetItem(item);
            if (c0 != null && d0 != null) c0.GiveItem(d0);
            if (item != "Sun Bomb") itemTimes = new[] { 0.05, 0.2, 0.5 };   // a rocket is gone in half a second
        }
        if (item != null && armed && !heldShot && t >= 10.7)
        {
            heldShot = true;
            var k = Object.FindFirstObjectByType<RaceManager>()?.Player?.transform;
            if (k != null) { itemFrom = k.position + k.forward * 4.2f - k.right * 3.4f + Vector3.up * 2.2f; itemAt = k.position + Vector3.up * 1.1f; SideShot($"item{track}-held"); itemFrom = k.position - k.forward * 4.5f + k.right * 3.2f + Vector3.up * 2.4f; SideShot($"item{track}-held-b"); }
        }
        if (item != null && thrownAt < 0 && t >= 11)
        {
            var race = Object.FindFirstObjectByType<RaceManager>();
            var caster = race != null && race.Player != null ? race.Player.GetComponent<ItemCaster>() : null;
            var def = Object.FindFirstObjectByType<ItemManager>()?.GetItem(item);
            if (caster != null) { var k = race.Player.transform; itemAt = k.position + k.forward * 11f + Vector3.up * 0.6f; itemFrom = k.position + k.forward * 3f + k.right * 7f + Vector3.up * 2.4f; }
            if (caster != null && def != null) { if (caster.ammo == 0) caster.GiveItem(def); caster.Cast(); thrownAt = t; Debug.Log($"[playtest] threw {item} at {t:0.0}s: ammo now {caster.ammo}, active {race.Player.GetComponent<Kart>().active}, spinning {race.Player.GetComponent<Kart>().spinningOut}, state {race.state}"); }
            else { Debug.LogError($"[playtest] cannot throw {item}: caster {(caster != null)}, item {(def != null)}"); thrownAt = t; }
        }
        if (thrownAt >= 0 && itemShot < itemTimes.Length && t >= thrownAt + itemTimes[itemShot]) { Capture($"item{track}-{itemShot}"); SideShot($"item{track}-{itemShot}-side"); var bombs = Object.FindObjectsByType<SunBomb>(FindObjectsSortMode.None);
            Debug.Log($"[playtest] item frame {itemShot}: {bombs.Length} sun bomb(s){string.Concat(bombs.Select(b => $" at {b.transform.position}"))}, {Object.FindObjectsByType<Kart>(FindObjectsSortMode.None).Count(k => k.spinningOut)} kart(s) spinning"); itemShot++; }
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
        if (mobile) QualitySettings.SetQualityLevel(prevQuality, true);
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
        if (race.Player != null)
            foreach (var l in race.Player.GetComponentsInChildren<Light>())
                Debug.Log($"[playtest]   light {l.name} {l.type} i={l.intensity} r={l.range} angle={l.spotAngle} at={l.transform.position} fwd={l.transform.forward}");
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
        // Screen-space-overlay canvases never land in a RenderTexture: borrow the camera for the shot (or, with
        // -nohud, leave them out for a clean frame of the world).
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (noHud) break;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
        }
        Canvas.ForceUpdateCanvases();
        Shoot(cam, name);

        var lighthouse = GameObject.Find(look);
        var player = Object.FindFirstObjectByType<RaceManager>()?.Player;
        if (!vista || lighthouse == null || player == null) return;
        var go = new GameObject("VistaCam");
        var vc = go.AddComponent<Camera>();
        vc.CopyFrom(cam);
        go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        var bounds = lighthouse.GetComponentInChildren<Renderer>()?.bounds ?? new Bounds(lighthouse.transform.position, Vector3.one);
        var at = look == "Lighthouse" ? lighthouse.transform.position + Vector3.up * 20f : bounds.center;
        var toward = (at - player.transform.position); toward.y = 0; toward.Normalize();
        // the lighthouse from over the grandstands; anything else from 45 m short of it, 14 m up
        var from = look == "Lighthouse" ? player.transform.position + Vector3.up * 16f + toward * 30f
                                        : at + Vector3.Cross(Vector3.up, toward) * 55f - toward * 40f + Vector3.up * 16f;   // a three-quarter view of it
        go.transform.position = from; go.transform.LookAt(at);
        vc.fieldOfView = 46f;
        Shoot(vc, name + "-vista");
        Object.Destroy(go);
    }

    static void SideShot(string name)
    {
        var cam = Camera.main; if (cam == null) return;
        var go = new GameObject("ItemCam");
        var vc = go.AddComponent<Camera>(); vc.CopyFrom(cam);
        go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        go.transform.position = itemFrom; go.transform.LookAt(itemAt); vc.fieldOfView = 50f;
        Shoot(vc, name);
        var bomb = Object.FindFirstObjectByType<SunBomb>();
        if (bomb != null)   // and the item itself, from its face side, close
        {
            var b = bomb.transform;
            go.transform.position = b.position - b.forward * 2.6f + b.right * 1.2f + Vector3.up * 0.9f; go.transform.LookAt(b.position); vc.fieldOfView = 40f;
            Shoot(vc, name.Replace("-side", "-close"));
        }
        Object.Destroy(go);
    }

    static void Shoot(Camera cam, string name)
    {
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
