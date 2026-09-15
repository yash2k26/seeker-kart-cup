using System.Collections.Generic;
using System.IO;
using System.Linq;
using KartCup;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Generates the beach circuit and the whole race scene headlessly. One closed spline gives us the road mesh,
// the AI waypoint chain, the checkpoints, the start grid and the prop placement — so the track is one edit away
// from a different shape. Run: Unity -batchmode -projectPath . -executeMethod TrackSetup.BuildRace -quit
public static class TrackSetup
{
    const string ScenePath = "Assets/_Game/Scenes/Race.unity";
    const string Art = "Assets/_Game/Art/";
    const string Prefabs = "Assets/_Game/Prefabs/";
    const string PKP = "Assets/ThirdParty/PowerslideKartPhysics/";

    // Track shape: a wobbled circle. Two sine terms give long sweepers, a hairpin and a couple of kinks.
    const float Radius = 150f, HalfWidth = 9f, Kerb = 1.4f;
    const int ControlPoints = 16, PerSegment = 24;

    static Vector3[] centre;      // sampled centreline
    static Vector3[] right;       // road right vector per sample

    [MenuItem("Build/Build Race Scene")]
    public static void BuildRace()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Sample();

        Environment();
        var road = Road();
        var waypoints = Waypoints();
        var checkpoints = Checkpoints();
        var grid = StartGrid();
        Dressing();
        ItemBoxes();

        // ---- systems ----
        var boot = new GameObject("Boot");
        boot.AddComponent<GameBoot>();
        boot.AddComponent<HitStop>();

        var input = Instantiate(PKP + "Prefabs/Scene/Input Manager.prefab", "Input Manager").GetComponent<InputManager>();
        input.enableStandardInput = false;                 // Android runs the new Input System only; legacy axes would throw
        input.enableMobileInput = true;
        input.mobileAutoAccel = 1f;                        // thumb steers, the kart drives itself — standard for phone racers
        input.mobileDriftHoldSteer = 0.55f;
        EditorUtility.SetDirty(input);

        Instantiate(PKP + "Prefabs/Items/Items.prefab", "Items");

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 58; cam.nearClipPlane = 0.3f; cam.farClipPlane = 400; cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var kartCam = camGo.AddComponent<KartCamera>();
        kartCam.useLegacyInput = false;                    // it would poll legacy axes every frame otherwise
        kartCam.initialDist = 10.5f; kartCam.initialHeight = 4.2f; kartCam.smoothRate = 9f;   // the rig is 3.75 m long: closer than this and the kart fills the phone screen
        camGo.AddComponent<CameraFeel>();

        var race = new GameObject("RaceManager").AddComponent<RaceManager>();
        race.totalLaps = 2;
        race.checkpoints = checkpoints;

        var hud = Hud(input, out var hudComp);

        var spawner = new GameObject("RaceSpawner").AddComponent<RaceSpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("roster").objectReferenceValue = Roster();
        Fill(so.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).ToArray());
        Fill(so.FindProperty("driverPrefabs"), Directory.GetFiles(Prefabs + "Drivers", "*.prefab").Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        Fill(so.FindProperty("gridSlots"), grid.Cast<Object>().ToArray());
        so.FindProperty("firstWaypoint").objectReferenceValue = waypoints;
        so.FindProperty("raceCamera").objectReferenceValue = kartCam;
        so.FindProperty("racers").intValue = 6;
        so.ApplyModifiedPropertiesWithoutUndo();

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"[track] race scene built: {centre.Length} samples, {checkpoints.Length} checkpoints, {grid.Length} grid slots");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ---------- geometry ----------
    static void Sample()
    {
        var ctrl = new Vector3[ControlPoints];
        for (int i = 0; i < ControlPoints; i++)
        {
            float t = i / (float)ControlPoints * Mathf.PI * 2f;
            float r = Radius * (1f + 0.22f * Mathf.Sin(3f * t) + 0.10f * Mathf.Cos(5f * t));
            ctrl[i] = new Vector3(Mathf.Sin(t) * r, 0, Mathf.Cos(t) * r);
        }
        int n = ControlPoints * PerSegment;
        centre = new Vector3[n]; right = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            int seg = i / PerSegment; float t = (i % PerSegment) / (float)PerSegment;
            centre[i] = CatmullRom(ctrl[Wrap(seg - 1, ControlPoints)], ctrl[seg], ctrl[Wrap(seg + 1, ControlPoints)], ctrl[Wrap(seg + 2, ControlPoints)], t);
        }
        for (int i = 0; i < n; i++)
        {
            var fwd = (centre[Wrap(i + 1, n)] - centre[Wrap(i - 1, n)]).normalized;
            right[i] = Vector3.Cross(Vector3.up, fwd).normalized;
        }
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;
    static Vector3 Forward(int i) => (centre[Wrap(i + 1, centre.Length)] - centre[Wrap(i - 1, centre.Length)]).normalized;

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) =>
        0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

    static GameObject Road()
    {
        var unlit = Shader.Find("KartCup/Unlit Colormap");
        var roadMat = LoadOrCreate(Art + "Materials/Road.mat", unlit, new Color(0.40f, 0.40f, 0.43f));
        var kerbMat = LoadOrCreate(Art + "Materials/Kerb.mat", unlit, new Color(0.93f, 0.93f, 0.95f));

        var root = new GameObject("Track");
        var road = Strip("Road", root.transform, roadMat, -HalfWidth, HalfWidth, 0.02f);
        road.AddComponent<MeshCollider>();
        var surface = road.AddComponent<GroundSurface>();
        surface.props = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Default Road.asset");
        Strip("KerbL", root.transform, kerbMat, -HalfWidth - Kerb, -HalfWidth, 0.03f);
        Strip("KerbR", root.transform, kerbMat, HalfWidth, HalfWidth + Kerb, 0.03f);
        return root;
    }

    static GameObject Strip(string name, Transform parent, Material mat, float from, float to, float height)
    {
        int n = centre.Length;
        var verts = new Vector3[n * 2]; var uvs = new Vector2[n * 2]; var norms = new Vector3[n * 2];
        var tris = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            var up = Vector3.up * height;
            verts[i * 2] = centre[i] + right[i] * from + up;
            verts[i * 2 + 1] = centre[i] + right[i] * to + up;
            uvs[i * 2] = uvs[i * 2 + 1] = new Vector2(0.03f, 0.5f);      // flat colour from the colormap shader
            norms[i * 2] = norms[i * 2 + 1] = Vector3.up;
            int j = (i + 1) % n;
            // wound so the face points up: left -> next-left -> right (the other order renders only from underneath)
            tris[i * 6] = i * 2; tris[i * 6 + 1] = j * 2; tris[i * 6 + 2] = i * 2 + 1;
            tris[i * 6 + 3] = i * 2 + 1; tris[i * 6 + 4] = j * 2; tris[i * 6 + 5] = j * 2 + 1;
        }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts, uv = uvs, normals = norms, triangles = tris };
        mesh.RecalculateBounds();
        Directory.CreateDirectory(Art + "Models/Track");
        AssetDatabase.CreateAsset(mesh, Art + $"Models/Track/{name}.asset");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        return go;
    }

    static BasicWaypoint Waypoints()
    {
        var root = new GameObject("Waypoints").transform;
        int every = Mathf.Max(1, Mathf.RoundToInt(centre.Length / 48f));       // ~48 points around the lap
        var list = new List<BasicWaypoint>();
        for (int i = 0; i < centre.Length; i += every)
        {
            var go = new GameObject($"WP{list.Count:00}");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre[i] + Vector3.up * 0.5f, Quaternion.LookRotation(Forward(i), Vector3.up));
            var wp = go.AddComponent<BasicWaypoint>();
            wp.radius = 9f;
            list.Add(wp);
        }
        for (int i = 0; i < list.Count; i++) list[i].nextPoint = list[(i + 1) % list.Count];
        return list[0];
    }

    static Checkpoint[] Checkpoints()
    {
        const int count = 12;
        var root = new GameObject("Checkpoints").transform;
        var list = new Checkpoint[count];
        for (int i = 0; i < count; i++)
        {
            int s = i * centre.Length / count;
            var go = new GameObject($"CP{i:00}");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre[s] + Vector3.up * 2f, Quaternion.LookRotation(Forward(s), Vector3.up));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3((HalfWidth + Kerb + 6f) * 2f, 8f, 2f);
            var cp = go.AddComponent<Checkpoint>();
            cp.index = i;
            list[i] = cp;
        }
        return list;
    }

    static Transform[] StartGrid()
    {
        var root = new GameObject("StartGrid").transform;
        var slots = new Transform[6];
        for (int i = 0; i < slots.Length; i++)
        {
            int row = i / 2, col = i % 2;
            int s = Wrap(-(row * 7 + 8) * centre.Length / Mathf.RoundToInt(Length()), centre.Length);   // metres back from the line
            var go = new GameObject($"Slot{i}");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(
                centre[s] + right[s] * (col == 0 ? -3.2f : 3.2f) + Vector3.up * 1.0f,
                Quaternion.LookRotation(Forward(s), Vector3.up));
            slots[i] = go.transform;
        }
        return slots;
    }

    static float Length()
    {
        float d = 0;
        for (int i = 0; i < centre.Length; i++) d += Vector3.Distance(centre[i], centre[(i + 1) % centre.Length]);
        return d;
    }

    static void Environment()
    {
        var unlit = Shader.Find("KartCup/Unlit Colormap");
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sky.mat");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.78f, 0.82f, 0.86f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 220; RenderSettings.fogEndDistance = 420;
        RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.92f);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(52, -35, 0);
        sun.shadows = LightShadows.None; sun.intensity = 1.05f;

        var sandMat = LoadOrCreate(Art + "Materials/Sand.mat", unlit, new Color(0.96f, 0.86f, 0.62f));
        var sand = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sand.name = "Sand";
        sand.transform.localScale = new Vector3(56, 1, 56);                  // 560 m island: the loop reaches ~200 m and its palms sit outside that
        sand.GetComponent<MeshRenderer>().sharedMaterial = sandMat;
        var sandSurface = sand.AddComponent<GroundSurface>();
        sandSurface.props = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Dirt.asset");   // slower and slidey: cutting corners costs you
        GameObjectUtility.SetStaticEditorFlags(sand, StaticEditorFlags.BatchingStatic);

        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        water.transform.position = new Vector3(0, -1.2f, 0);
        water.transform.localScale = new Vector3(200, 1, 200);
        water.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water.mat");
        Object.DestroyImmediate(water.GetComponent<Collider>());
    }

    static void Dressing()
    {
        var pirate = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Pirate.mat");
        var craft = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Watercraft.mat");
        var root = new GameObject("Dressing").transform;
        var palms = new[] { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        var rocks = new[] { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        var rnd = new System.Random(11);

        for (int i = 0; i < centre.Length; i += 9)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (rnd.NextDouble() > 0.55) continue;
                float offset = HalfWidth + Kerb + 4f + (float)rnd.NextDouble() * 22f;
                var pos = centre[i] + right[i] * offset * side;
                bool rock = rnd.NextDouble() > 0.62;
                var name = rock ? rocks[rnd.Next(rocks.Length)] : palms[rnd.Next(palms.Length)];
                Place(root, Art + $"Models/Props/Pirate/{name}.fbx", pirate, pos, (float)rnd.NextDouble() * 360f,
                      (rock ? 1.4f : 1.1f) + (float)rnd.NextDouble() * 0.8f);
            }
        }
        // far dressing so the horizon is not empty
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f;
            Place(root, Art + "Models/Props/Watercraft/buoy.fbx", craft, new Vector3(Mathf.Sin(a) * 250f, -0.4f, Mathf.Cos(a) * 250f), 0, 2f);
        }
        Place(root, Art + "Models/Props/Pirate/ship-wreck.fbx", pirate, new Vector3(-210, -0.4f, 120), 140, 2.2f);
        Place(root, Art + "Models/Props/Pirate/tower-complete-large.fbx", pirate, new Vector3(40, 0, 20), 15, 2.4f);
    }

    static void ItemBoxes()
    {
        var box = AssetDatabase.LoadAssetAtPath<GameObject>(PKP + "Prefabs/Items/ItemGiver.prefab");
        var toycar = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/ToyCar.mat");
        if (box == null) return;
        var root = new GameObject("ItemBoxes").transform;
        for (int lane = 0; lane < 5; lane++)
        {
            for (int i = centre.Length / 12; i < centre.Length; i += centre.Length / 6)   // offset so no cluster sits on the start line
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(box, root);
                go.transform.SetPositionAndRotation(centre[i] + right[i] * (lane - 2) * 3.2f + Vector3.up * 1.4f,
                                                    Quaternion.LookRotation(Forward(i), Vector3.up));
                Reskin(go, Art + "Models/Props/ToyCar/item-box.fbx", toycar, 1.5f);
                go.AddComponent<Spin>();
            }
        }
    }

    static CharacterDatabase Roster()
    {
        const string dir = "Assets/_Game/Data/";
        Directory.CreateDirectory(dir);
        var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(dir + "Characters.asset");
        if (db != null) return db;

        db = ScriptableObject.CreateInstance<CharacterDatabase>();
        // name, kart, driver, speed, accel, handling, weight  (pips sum to 0 so nobody is strictly better)
        var rows = new (string name, string kart, string driver, int sp, int ac, int hd, int wt, string blurb)[]
        {
            ("Nodey",    "oodi", "NodeRunner", 1,  0,  0, -1, "Runs the node that never misses a block."),
            ("Bitsy",    "ooli", "Builder",    0,  2,  0, -2, "Ships fast, brakes late."),
            ("Chunk",    "oobi", "Generic",    2, -1, -1,  0, "Top speed, turns like a ferry."),
            ("Pip",      "oopi", "Generic",   -1,  1,  2, -2, "Threads corners nobody else sees."),
            ("Bolt",     "oozi", "Generic",    1,  1, -1, -1, "All gas, some brakes."),
            ("Tank",     "oodi", "Generic",   -1, -1,  0,  2, "Hard to move, harder to pass."),
        };
        var defs = new List<CharacterDef>();
        foreach (var r in rows)
        {
            var def = ScriptableObject.CreateInstance<CharacterDef>();
            def.name = r.name;
            def.displayName = r.name; def.kartId = r.kart; def.driverId = r.driver; def.blurb = r.blurb;
            def.speed = r.sp; def.accel = r.ac; def.handling = r.hd; def.weight = r.wt;
            AssetDatabase.CreateAsset(def, dir + $"Character_{r.name}.asset");
            defs.Add(def);
        }
        db.characters = defs.ToArray();
        AssetDatabase.CreateAsset(db, dir + "Characters.asset");
        return db;
    }

    // ---------- UI ----------
    static GameObject Hud(InputManager input, out RaceHud hud)
    {
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var safe = new GameObject("SafeArea", typeof(RectTransform));
        safe.transform.SetParent(canvasGo.transform, false);
        safe.AddComponent<SafeArea>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // steering: the whole screen is the drag area, buttons sit on top of it
        var steer = Rect("SteerArea", safe.transform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        var steerImg = steer.gameObject.AddComponent<Image>();
        steerImg.color = new Color(0, 0, 0, 0);            // invisible but raycastable
        var steerArea = steer.gameObject.AddComponent<TouchSteerArea>();
        new SerializedObject(steerArea).Do(s => s.FindProperty("input").objectReferenceValue = input);

        Button("Drift", safe.transform, input, HoldButton.Control.Drift, "button_circle", new Vector2(1, 0), new Vector2(-190, 190), 300, "DRIFT");
        Button("Item", safe.transform, input, HoldButton.Control.Item, "button_square", new Vector2(1, 0), new Vector2(-430, 150), 210, "ITEM");
        Button("Brake", safe.transform, input, HoldButton.Control.Brake, "button_circle", new Vector2(0, 0), new Vector2(190, 160), 210, "BRAKE");

        var lap = Label("Lap", safe.transform, font, 54, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(420, 70));
        var pos = Label("Position", safe.transform, font, 96, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(28, -20), new Vector2(320, 120));
        var time = Label("Time", safe.transform, font, 44, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-24, -90), new Vector2(420, 60));
        var item = Label("Item", safe.transform, font, 40, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-24, 420), new Vector2(420, 60));
        var big = Label("Big", safe.transform, font, 190, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1200, 260));
        big.enabled = false;

        var fps = Label("Fps", safe.transform, font, 30, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(28, -150), new Vector2(1200, 44));
        var fpsCounter = canvasGo.AddComponent<FpsCounter>();
        new SerializedObject(fpsCounter).Do(s => s.FindProperty("label").objectReferenceValue = fps);

        hud = canvasGo.AddComponent<RaceHud>();
        var so = new SerializedObject(hud);
        so.FindProperty("lapLabel").objectReferenceValue = lap;
        so.FindProperty("positionLabel").objectReferenceValue = pos;
        so.FindProperty("timeLabel").objectReferenceValue = time;
        so.FindProperty("itemLabel").objectReferenceValue = item;
        so.FindProperty("bigLabel").objectReferenceValue = big;
        so.ApplyModifiedPropertiesWithoutUndo();
        return canvasGo;
    }

    static void Button(string name, Transform parent, InputManager input, HoldButton.Control control, string sprite, Vector2 anchor, Vector2 offset, float size, string caption)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, new Vector2(size, size));
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/MobileControls/{sprite}.png");
        img.color = new Color(1, 1, 1, 0.55f);
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        var hold = rt.gameObject.AddComponent<HoldButton>();
        var so = new SerializedObject(hold);
        so.FindProperty("control").enumValueIndex = (int)control;
        so.FindProperty("input").objectReferenceValue = input;
        so.FindProperty("fade").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();

        var label = Label(name + "Text", rt, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, 60));
        label.text = caption;
        label.color = new Color(1, 1, 1, 0.9f);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset; rt.sizeDelta = size;
        return rt;
    }

    static Text Label(string name, Transform parent, Font font, int size, TextAnchor anchor, Vector2 anchorPt, Vector2 offset, Vector2 sizeDelta)
    {
        var rt = Rect(name, parent, anchorPt, anchorPt, offset, sizeDelta);
        rt.pivot = anchorPt;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = rt.gameObject.AddComponent<Outline>();
        shadow.effectColor = new Color(0, 0, 0, 0.7f); shadow.effectDistance = new Vector2(2.5f, -2.5f);
        return t;
    }

    // ---------- helpers ----------
    static GameObject Instantiate(string path, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("[track] missing " + path); return new GameObject(name); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        return go;
    }

    /// Powerslide's own item box ships a built-in-pipeline material (pink under URP): hide its mesh and
    /// put the Kenney box in its place, keeping the prefab's trigger and cooldown logic.
    static void Reskin(GameObject go, string fbxPath, Material mat, float scale)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
        var art = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (art == null) return;
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(art, go.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * scale;
        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
    }

    static GameObject Place(Transform parent, string fbxPath, Material mat, Vector3 pos, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) { Debug.LogWarning("[track] missing " + fbxPath); return new GameObject("missing"); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one * scale;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Material LoadOrCreate(string path, Shader s, Color tint)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); m.SetColor("_BaseColor", tint); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static void Fill(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Do(this SerializedObject so, System.Action<SerializedObject> edit)
    {
        edit(so); so.ApplyModifiedPropertiesWithoutUndo();
    }
}
