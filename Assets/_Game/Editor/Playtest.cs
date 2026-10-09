using System.IO;
using System.Linq;
using System.Reflection;
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
    static double thrownAt = -1, goAt = -1;
    static bool armed, heldShot;
    static Vector3 itemFrom, itemAt;   // a second camera for the item frames: beside the road, on where it lands
    static int prevQuality;
    static readonly double[] shotTimes = { 5, 12, 22, 34, 46 };   // the last: the results card, after the podium
    static readonly double[] introTimes = { 2.2, 3.4, 4.6, 7.2 };   // the drone's lap before the flag, and a turbo pad from above
    static int introShot;
    // -autopilot: the AI drives the player's kart, so the camera, the HUD and everything that happens to a kart are seen
    // in motion. -every S: a frame every S seconds from the green light (auto<track>-NN.png). -seconds N: run that long.
    static bool autopilot, autoSet;
    // -gatewalk: the player keeps the phone's touch input and is carried through every gate in turn, so the line is
    // crossed inside a real trigger callback, as on a phone (the autopilot's AI input hid the finish bug of 10 Oct 2026).
    static bool gatewalk; static int walkGate; static double nextWalk;
    static float[][] sweep;   // -camsweep "dist,height,look,fov;...": each auto frame with the next camera
    // with -autopilot, frames on the moments that matter: a pickup (the roulette), a hit (the throw), the finish
    static readonly System.Collections.Generic.List<(double at, string name)> moments = new();
    static int lastAmmo, pickups, hits; static bool wasSpinning, wasFinished;
    // -oob: the player is dropped into the sea 8 s after GO (out of bounds, respawn, blink); -smash: a smashable is put
    // on the player's line at 12 s. Frames of both, and of the countdown with its start-boost line.
    static bool oobTest, smashTest, oobDone, smashDone, countShot;
    static double every, nextAuto = -1, until = 50;
    static int autoShot;

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
        KartCup.Weekly.Racing = args.Contains("-weekly");   // Kartel Weekly's time trial: alone on track, no items
        KartCup.RaceMode.Clear();                             // -mode elim | duel (the Tour's and Daily's modes)
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-mode")
        {
            KartCup.RaceMode.Current = args[i + 1] == "duel" ? KartCup.RaceMode.Kind.Duel : KartCup.RaceMode.Kind.Elimination;
            if (KartCup.RaceMode.Current == KartCup.RaceMode.Kind.Duel) KartCup.RaceMode.Rival = 1;
        }
        look = "Lighthouse";
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-look") look = args[i + 1];   // what the vista frames look at
        item = null; itemShot = 0; thrownAt = -1; goAt = -1; armed = false; heldShot = false;
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-item") item = args[i + 1];
        autopilot = args.Contains("-autopilot"); gatewalk = args.Contains("-gatewalk"); walkGate = 0; nextWalk = 0; autoSet = false; autoShot = 0; nextAuto = -1; every = 0; until = 50;
        moments.Clear(); lastAmmo = 0; pickups = 0; hits = 0; wasSpinning = false; wasFinished = false;
        oobTest = args.Contains("-oob"); smashTest = args.Contains("-smash"); oobDone = smashDone = countShot = false;
        Smashable.Smashed -= OnSmashed; Smashable.Smashed += OnSmashed;
        Ability.Used -= OnAbility; Ability.Used += OnAbility; abilities = 0;
        for (int i = 0; i < args.Length - 1; i++) { if (args[i] == "-every") double.TryParse(args[i + 1], out every); if (args[i] == "-seconds") double.TryParse(args[i + 1], out until); }
        sweep = null;
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-camsweep")
            sweep = args[i + 1].Split(';').Select(v => v.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToArray();
        prevQuality = QualitySettings.GetQualityLevel();
        if (mobile) QualitySettings.SetQualityLevel(0, true);
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup;
        shot = 0; introShot = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void GateWalk(double t)
    {
        var race = Object.FindFirstObjectByType<RaceManager>();
        var p = race != null ? race.Player : null;
        if (race == null || p == null || race.state != RaceManager.State.Racing) return;
        if (p.finished)
        {
            gatewalk = false;
            Debug.Log($"[gatewalk] finished: state {race.state}, input {(p.GetComponent<KartInputMobile>() != null ? "touch" : "-")}/{(p.GetComponent<KartAi>() != null ? "ai" : "-")}");
            return;
        }
        nextWalk = t + 0.35;
        var cp = race.checkpoints[(walkGate++) % race.checkpoints.Length];
        var rb = p.GetComponent<Rigidbody>();
        // just short of the gate, moving through it: the physics step after this one carries the kart across the trigger
        var at = cp.transform.position - cp.transform.forward * 3f + Vector3.up * 0.5f;
        rb.position = at; rb.rotation = cp.transform.rotation; rb.linearVelocity = cp.transform.forward * 20f;
        Debug.Log($"[gatewalk] gate {cp.index}: lap {p.lap}, last {p.checkpointIndex}");
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
            var slicks = GameObject.Find("OilSlicks");
            if (introShot == 0 && slicks != null && slicks.transform.childCount > 0)
            {
                var slick = slicks.transform.GetChild(0);
                itemFrom = slick.position - slick.forward * 9f + Vector3.up * 4.5f + slick.right * 3f; itemAt = slick.position;
                SideShot($"oil{track}");
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
        if (autopilot && !autoSet && t > 1) { autoSet = true; if (!Autopilot()) autoSet = false; }
        if (gatewalk && t >= nextWalk) GateWalk(t);   // set first: a throw must not retry every frame
        if (every > 0 && goAt >= 0)
        {
            if (nextAuto < 0) nextAuto = goAt + every;
            if (t >= nextAuto)
            {
                Capture($"auto{track}-{autoShot:00}"); autoShot++; nextAuto += every;
                if (autoShot <= 3) Probe();
                if (sweep != null) SetCamera(sweep[autoShot % sweep.Length]);   // the next frame is taken with the next camera
            }
            else if (sweep != null && autoShot == 0 && nextAuto - t > every - 0.1) SetCamera(sweep[0]);
        }
        if (autopilot) Moments(t);
        if (!autopilot && shot < shotTimes.Length && captureAt < 0 && t >= shotTimes[shot])
        {
            var pause = Object.FindFirstObjectByType<PauseMenu>();
            var race = Object.FindFirstObjectByType<RaceManager>();
            if (shot == 2 && pause != null) pause.Pause();                                   // overlay in the frame
            if (shot == 3 && race != null && race.Player != null) race.Finish(race.Player);   // results card in the frame
            captureAt = t + 0.6;                                                              // after the entrance has landed
        }
        // the item is handed over a little before it is used, so the kart is seen carrying it (pod up, bomb in the cradle)
        // (timed from the green light: the drone's lap before it is as long as the circuit)
        var raceNow = Object.FindFirstObjectByType<RaceManager>();
        if (goAt < 0 && raceNow != null && raceNow.state == RaceManager.State.Racing) goAt = t;
        if (item != null && !armed && goAt >= 0 && t >= goAt + 2.0)
        {
            armed = true;
            var r0 = Object.FindFirstObjectByType<RaceManager>();
            var c0 = r0 != null && r0.Player != null ? r0.Player.GetComponent<ItemCaster>() : null;
            var d0 = Object.FindFirstObjectByType<ItemManager>()?.GetItem(item);
            if (c0 != null && d0 != null) c0.GiveItem(d0);
            if (item != "Sun Bomb") itemTimes = new[] { 0.05, 0.2, 0.5, 1.2, 2.5 };   // a rocket is gone in half a second; a trap must still be lying there after two
        }
        if (item != null && armed && !heldShot && t >= goAt + 3.1)
        {
            heldShot = true;
            Capture($"item{track}-hud");                       // the race view with the item in the HUD's slot
            var k = Object.FindFirstObjectByType<RaceManager>()?.Player?.transform;
            if (k != null) { itemFrom = k.position + k.forward * 4.2f - k.right * 3.4f + Vector3.up * 2.2f; itemAt = k.position + Vector3.up * 1.1f; SideShot($"item{track}-held"); itemFrom = k.position - k.forward * 4.5f + k.right * 3.2f + Vector3.up * 2.4f; SideShot($"item{track}-held-b"); }
        }
        if (item != null && armed && thrownAt < 0 && t >= goAt + 3.4)
        {
            var race = Object.FindFirstObjectByType<RaceManager>();
            var caster = race != null && race.Player != null ? race.Player.GetComponent<ItemCaster>() : null;
            var def = Object.FindFirstObjectByType<ItemManager>()?.GetItem(item);
            if (caster != null) { var k = race.Player.transform; itemAt = k.position + k.forward * 11f + Vector3.up * 0.6f; itemFrom = k.position + k.forward * 3f + k.right * 7f + Vector3.up * 2.4f; }
            if (caster != null && def != null) { if (caster.ammo == 0) caster.GiveItem(def); caster.Cast(); thrownAt = t; Debug.Log($"[playtest] threw {item} at {t:0.0}s: ammo now {caster.ammo}, active {race.Player.GetComponent<Kart>().active}, spinning {race.Player.GetComponent<Kart>().spinningOut}, state {race.state}"); }
            else { Debug.LogError($"[playtest] cannot throw {item}: caster {(caster != null)}, item {(def != null)}"); thrownAt = t; }
        }
        if (thrownAt >= 0 && itemShot < itemTimes.Length && t >= thrownAt + itemTimes[itemShot]) { Capture($"item{track}-{itemShot}"); SideShot($"item{track}-{itemShot}-side"); var bombs = Object.FindObjectsByType<SunBomb>(FindObjectsSortMode.None);
            { var pk = Object.FindFirstObjectByType<RaceManager>()?.Player?.GetComponent<Kart>(); var shots = Object.FindObjectsByType<SpawnedProjectileItem>(FindObjectsSortMode.None);
              if (pk != null) Debug.Log($"[playtest] item frame {itemShot}: thrower spinning {pk.spinningOut}, speed {pk.velMag:F1}, {shots.Length} shot(s){string.Concat(shots.Select(x => $" {pk.transform.InverseTransformPoint(x.transform.position).z:F1} m ahead"))}"); }
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
        if (t < until) return;

        EditorApplication.update -= Tick;
        Report(t);
        Debug.Log("[playtest] done");
        if (mobile) QualitySettings.SetQualityLevel(prevQuality, true);
        EditorApplication.Exit(0);
    }

    /// The player's touch input off, the field's AI in its place, from the first waypoint like everyone else.
    static bool Autopilot()
    {
        var race = Object.FindFirstObjectByType<RaceManager>();
        var player = race != null ? race.Player : null;
        var spawner = Object.FindFirstObjectByType<RaceSpawner>();
        if (player == null || spawner == null) return false;
        var touch = player.GetComponent<KartInputMobile>(); if (touch != null) Object.DestroyImmediate(touch);   // one KartInput per kart
        var first = typeof(RaceSpawner).GetField("firstWaypoint", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(spawner) as BasicWaypoint;
        player.gameObject.AddComponent<KartAi>().Configure(null, first, 99);
        Debug.Log($"[playtest] autopilot drives {player.racerName}");
        return true;
    }

    static void Moments(double t)
    {
        var race = Object.FindFirstObjectByType<RaceManager>(); var p = race != null ? race.Player : null;
        if (p == null) return;
        if (!countShot && race.state == RaceManager.State.Countdown && race.countdown < 1.6f) { countShot = true; Capture($"countdown{track}"); }
        if (oobTest && !oobDone && goAt >= 0 && t > goAt + 8)
        {
            oobDone = true;
            var rb = p.GetComponent<Kart>().rb; var at = p.transform.position + p.transform.right * 3f; at.y = WaterBody.SurfaceAt(at, out float w) ? w - 2.5f : -9f;
            rb.position = at; rb.linearVelocity = Vector3.down * 2f;
            foreach (var d in new[] { 0.3, 0.8, 1.2, 1.5, 2.0 }) moments.Add((t + d, $"oob{track}-{d:0.0}"));
            Debug.Log($"[playtest] dropped the player at {at}");
        }
        if (smashTest && !smashDone && goAt >= 0 && t > goAt + 12)
        {
            smashDone = true;
            Smashable best = null; float bestD = float.MaxValue;
            foreach (var s in Object.FindObjectsByType<Smashable>(FindObjectsSortMode.None)) { if (s.name.Contains("cone")) continue; float d = (s.transform.position - p.transform.position).sqrMagnitude; if (d < bestD) { bestD = d; best = s; } }
            if (best != null) { var k = p.GetComponent<Kart>(); var v = k.rb.linearVelocity; var ahead = p.transform.position + (v.sqrMagnitude > 1f ? v.normalized : p.transform.forward) * 14f; best.transform.position = ahead + Vector3.up * 0.2f; Debug.Log($"[playtest] smashable {best.name} put on the line"); }
        }
        var caster = p.GetComponent<ItemCaster>(); var kart = p.GetComponent<Kart>();
        int ammo = caster != null && caster.item != null ? caster.ammo : 0;
        if (ammo > lastAmmo && pickups < 3) { pickups++; moments.Add((t + 0.2, $"pickup{track}-{pickups}a")); moments.Add((t + 1.0, $"pickup{track}-{pickups}b")); }
        lastAmmo = ammo;
        if (kart != null && kart.spinningOut && !wasSpinning && hits < 3) { hits++; moments.Add((t + 0.35, $"hit{track}-{hits}a")); moments.Add((t + 0.8, $"hit{track}-{hits}b")); }
        wasSpinning = kart != null && kart.spinningOut;
        if (p.finished && !wasFinished) foreach (var d in new[] { 0.3, 1.0, 1.7, 2.4, 3.2 }) moments.Add((t + d, $"finish{track}-{d:0.0}"));
        wasFinished = p.finished;
        for (int i = moments.Count - 1; i >= 0; i--)
            if (t >= moments[i].at)
            {
                var name = moments[i].name;
                if (name.StartsWith("side:")) SideShot(name.Substring(5)); else Capture(name);
                Debug.Log($"[playtest] moment {name}"); moments.RemoveAt(i);
            }
    }

    /// What the feel layer is doing right now: the bubbles drawn, the speed lines' state, the player's boost.
    static void Probe()
    {
        var bubbles = Object.FindObjectsByType<ItemBubble>(FindObjectsSortMode.None);
        var cam = UnityEngine.Camera.main;
        int shown = 0, near = 0; foreach (var b in bubbles) { var r = b.GetComponent<Renderer>(); if (r.enabled) shown++; if (cam != null && (b.transform.position - cam.transform.position).magnitude < 40f) near++; }
        var first = bubbles.Length > 0 ? bubbles[0].GetComponent<Renderer>() : null;
        Debug.Log($"[playtest] probe: {bubbles.Length} bubbles, {shown} shown, {near} within 40 m; first: {(first != null ? first.sharedMaterial?.shader.name + " bounds " + first.bounds.size + " visible " + first.isVisible + " scale " + first.transform.lossyScale : "none")}");
        var lines = Object.FindFirstObjectByType<SpeedLines>(); var img = lines != null ? lines.GetComponent<UnityEngine.UI.Image>() : null;
        var pk = Object.FindFirstObjectByType<RaceManager>()?.Player?.GetComponent<Kart>();
        Debug.Log($"[playtest] probe: speed lines {(img != null ? $"enabled {img.enabled} alpha {img.color.a:0.00} sprite {img.sprite?.name}" : "missing")}; player boostReserve {pk?.boostReserve:0.00} speed {pk?.velMag:0.0}");
    }

    static int abilities;
    /// Someone used their special: a frame from the race camera, and one from beside the kart that used it.
    static void OnAbility(Ability a)
    {
        if (!EditorApplication.isPlaying || abilities >= 8) return;
        abilities++;
        double t = EditorApplication.timeSinceStartup - start;
        var k = a.transform; string tag = $"ability{track}-{abilities}-{a.def.name.Replace(' ', '_')}";
        itemFrom = k.position + k.right * 6f - k.forward * 4f + Vector3.up * 3f; itemAt = k.position + k.forward * 4f + Vector3.up;
        moments.Add((t + 0.4, "side:" + tag + "-side"));
        if (a.isPlayer) moments.Add((t + 0.25, tag));
        Debug.Log($"[playtest] {a.name} used {a.def.name}");
    }

    static void OnSmashed(Smashable s, Kart k)
    {
        if (!EditorApplication.isPlaying) return;
        double t = EditorApplication.timeSinceStartup - start;
        if (k.GetComponent<KartProgress>()?.isPlayer != true) return;
        foreach (var d in new[] { 0.05, 0.25, 0.6 }) moments.Add((t + d, $"smash{track}-{d:0.00}"));
        itemFrom = k.transform.position + k.transform.right * 7f + k.transform.forward * 6f + Vector3.up * 2.5f; itemAt = s.transform.position + Vector3.up * 1.5f;
        foreach (var d in new[] { 0.15, 0.45 }) moments.Add((t + d, $"side:smash{track}-side-{d:0.00}"));
    }

    static void SetCamera(float[] v)
    {
        var kc = Object.FindFirstObjectByType<KartCamera>(); var feel = CameraFeel.Instance;
        if (kc != null) { kc.initialDist = v[0]; kc.initialHeight = v[1]; kc.lookAtHeight = v[2]; }
        if (feel != null && v.Length > 3) feel.BaseFov = v[3];
        Debug.Log($"[playtest] camera {string.Join(",", v)} for frame {autoShot}");
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
