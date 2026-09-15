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
using TMPro;
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
        kartCam.initialDist = 9.5f; kartCam.initialHeight = 4.6f; kartCam.smoothRate = 9f;   // the rig is 3.75 m long: closer than this and the kart fills the phone screen
        camGo.AddComponent<CameraFeel>();
        camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

        var volumeGo = new GameObject("Post");
        var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/PostProfile.asset");

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

        var redMat = LoadOrCreate(Art + "Materials/KerbRed.mat", unlit, new Color(0.80f, 0.20f, 0.18f));
        var lineMat = LoadOrCreate(Art + "Materials/RoadLine.mat", unlit, new Color(0.92f, 0.92f, 0.88f));
        var darkMat = LoadOrCreate(Art + "Materials/RoadDark.mat", unlit, new Color(0.18f, 0.18f, 0.20f));

        var root = new GameObject("Track");
        var road = Strip("Road", root.transform, roadMat, -HalfWidth, HalfWidth, 0.02f);
        road.AddComponent<MeshCollider>();
        var surface = road.AddComponent<GroundSurface>();
        surface.props = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Default Road.asset");

        // kerbs in alternating red/white blocks, the way a real circuit reads at speed
        Strip("KerbL", root.transform, kerbMat, -HalfWidth - Kerb, -HalfWidth, 0.03f);
        Strip("KerbR", root.transform, kerbMat, HalfWidth, HalfWidth + Kerb, 0.03f);
        Strip("KerbLRed", root.transform, redMat, -HalfWidth - Kerb, -HalfWidth, 0.035f, 4, 4);
        Strip("KerbRRed", root.transform, redMat, HalfWidth, HalfWidth + Kerb, 0.035f, 4, 4, 4);

        // dashed centre line and solid edge lines
        Strip("CentreLine", root.transform, lineMat, -0.35f, 0.35f, 0.035f, 5, 7);
        Strip("EdgeL", root.transform, lineMat, -HalfWidth + 0.3f, -HalfWidth + 0.75f, 0.035f);
        Strip("EdgeR", root.transform, lineMat, HalfWidth - 0.75f, HalfWidth - 0.3f, 0.035f);

        // start/finish chequer: eight columns across the road, two rows deep
        for (int col = 0; col < 8; col++)
        {
            float a = -HalfWidth + col * (HalfWidth * 2f / 8f), b = a + HalfWidth * 2f / 8f;
            Strip($"Chequer{col}", root.transform, col % 2 == 0 ? darkMat : lineMat, a, b, 0.04f, 2, 0, 0, 0, 2);
            Strip($"Chequer{col}b", root.transform, col % 2 == 0 ? lineMat : darkMat, a, b, 0.04f, 2, 0, 0, 2, 2);
        }
        return root;
    }

    /// A ribbon along the track between two lateral offsets. `on`/`off` dash it (samples on, samples off),
    /// `phase` shifts the pattern, and `start`/`count` restrict it to a stretch — that covers kerb blocks,
    /// the dashed centre line and the start chequer without a texture between them.
    static GameObject Strip(string name, Transform parent, Material mat, float from, float to, float height,
                            int on = 0, int off = 0, int phase = 0, int start = 0, int count = 0)
    {
        int n = centre.Length;
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        int span = count > 0 ? count : n;
        for (int k = 0; k < span; k++)
        {
            int i = Wrap(start + k, n);
            if (on > 0 && off > 0 && ((k + phase) % (on + off)) >= on) continue;
            int j = Wrap(i + 1, n);
            int v = verts.Count;
            foreach (var (sample, side) in new[] { (i, from), (i, to), (j, from), (j, to) })
            {
                verts.Add(centre[sample] + right[sample] * side + Vector3.up * height);
                uvs.Add(new Vector2(0.03f, 0.5f));      // flat colour from the colormap
                norms.Add(Vector3.up);
            }
            // wound so the face points up
            tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
        }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts.ToArray(), uv = uvs.ToArray(), normals = norms.ToArray(), triangles = tris.ToArray() };
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
        // Gradient ambient: sky blue from above, warm sand bounce from below — flat ambient is what made
        // everything read as one colour.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.30f, 0.37f, 0.46f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.26f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.17f, 0.13f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 220; RenderSettings.fogEndDistance = 420;
        RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.92f);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(46, -38, 0);
        sun.shadows = LightShadows.Soft; sun.intensity = 1.0f;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.shadowStrength = 0.72f; sun.shadowBias = 0.06f; sun.shadowNormalBias = 0.5f;

        var sandMat = LoadOrCreate(Art + "Materials/Sand.mat", unlit, new Color(0.96f, 0.86f, 0.62f));
        var sand = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sand.name = "Sand";
        sand.transform.localScale = new Vector3(50, 1, 50);                  // 500 m island: the loop reaches ~200 m, so the shore stays in view from the track
        sand.GetComponent<MeshRenderer>().sharedMaterial = sandMat;
        var sandSurface = sand.AddComponent<GroundSurface>();
        sandSurface.props = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Dirt.asset");   // slower and slidey: cutting corners costs you
        GameObjectUtility.SetStaticEditorFlags(sand, StaticEditorFlags.BatchingStatic);

        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        water.transform.position = new Vector3(0, -0.9f, 0);
        water.transform.localScale = new Vector3(200, 1, 200);
        water.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water.mat");
        Object.DestroyImmediate(water.GetComponent<Collider>());
    }

    static void Dressing()
    {
        var pirate = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Pirate.mat");
        var craft = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Watercraft.mat");
        var toycar = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/ToyCar.mat");
        var root = new GameObject("Dressing").transform;
        var palms = new[] { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        var rocks = new[] { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        var clutter = new[] { "crate", "barrel", "crate-bottles", "chest" };
        var rnd = new System.Random(11);
        float edge = HalfWidth + Kerb;
        int n = centre.Length;

        // Start/finish gantry: the single prop that says "race track" before anything else does.
        var gate = Place(root, Art + "Models/Props/ToyCar/gate-finish.fbx", toycar, centre[0], 0, 1f);
        var gateLocal = new Bounds();
        bool first = true;
        foreach (var mf in gate.GetComponentsInChildren<MeshFilter>())       // local bounds: world bounds change with rotation
        {
            if (mf.sharedMesh == null) continue;
            if (first) { gateLocal = mf.sharedMesh.bounds; first = false; } else gateLocal.Encapsulate(mf.sharedMesh.bounds);
        }
        float gateSpan = Mathf.Max(gateLocal.size.x, gateLocal.size.z, 0.01f);
        gate.transform.localScale = Vector3.one * ((edge * 2.05f) / Mathf.Max(gateLocal.size.x, 0.01f));   // legs land just outside the kerbs
        gate.transform.rotation = Quaternion.LookRotation(Forward(0), Vector3.up);

        // Trackside: palms and rocks in clusters, clutter tight to the kerb, foliage patches breaking up the sand.
        for (int i = 0; i < n; i += 3)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                double roll = rnd.NextDouble();
                if (roll < 0.34)
                {
                    float off = edge + 2.5f + (float)rnd.NextDouble() * 10f;
                    var name = rnd.NextDouble() > 0.55 ? rocks[rnd.Next(rocks.Length)] : palms[rnd.Next(palms.Length)];
                    Place(root, Art + $"Models/Props/Pirate/{name}.fbx", pirate,
                          centre[i] + right[i] * off * side, (float)rnd.NextDouble() * 360f, 1.6f + (float)rnd.NextDouble() * 1.8f);
                }
                else if (roll < 0.44)
                {
                    Place(root, Art + $"Models/Props/Pirate/{clutter[rnd.Next(clutter.Length)]}.fbx", pirate,
                          centre[i] + right[i] * (edge + 1.6f) * side, (float)rnd.NextDouble() * 360f, 1.6f + (float)rnd.NextDouble() * 0.8f);
                }
                else if (roll < 0.60)
                {
                    // These are already flat slabs about 7 m across, so they only need a yaw and a modest scale.
                    var patch = rnd.NextDouble() > 0.5 ? "patch-sand-foliage" : "patch-sand";
                    Place(root, Art + $"Models/Props/Pirate/{patch}.fbx", pirate,
                          centre[i] + right[i] * (edge + 5f + (float)rnd.NextDouble() * 26f) * side + Vector3.up * 0.02f,
                          (float)rnd.NextDouble() * 360f, 0.8f + (float)rnd.NextDouble() * 1.1f);
                }
            }
        }

        // Cones on the apex of the tighter corners: they read as a racing line.
        for (int i = 0; i < n; i += 2)
        {
            float turn = Vector3.SignedAngle(Forward(Wrap(i - 6, n)), Forward(Wrap(i + 6, n)), Vector3.up);
            if (Mathf.Abs(turn) < 7f) continue;
            float side = -Mathf.Sign(turn);
            Place(root, Art + "Models/Props/ToyCar/item-cone.fbx", toycar,
                  centre[i] + right[i] * (HalfWidth - 0.6f) * side, 0, 1.6f);
        }

        // Marker flags every ~45 m so straights have rhythm.
        for (int i = 0; i < n; i += 18)
            for (int side = -1; side <= 1; side += 2)
                Place(root, Art + "Models/Props/Pirate/flag-high-pennant.fbx", pirate,
                      centre[i] + right[i] * (edge + 1.2f) * side, side * 90f, 1.5f);

        // The island rim. The Nature kit's cliff blocks ship without their colormap here, so they render as plain
        // grey boxes — the Pirate rocks are textured and read as a shoreline.
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            float r = 244f + (float)rnd.NextDouble() * 26f;
            Place(root, Art + $"Models/Props/Pirate/{rocks[rnd.Next(rocks.Length)]}.fbx", pirate,
                  new Vector3(Mathf.Sin(a) * r, -0.8f, Mathf.Cos(a) * r), (float)rnd.NextDouble() * 360f, 1.8f + (float)rnd.NextDouble() * 2.4f);
            if (rnd.NextDouble() > 0.6)
                Place(root, Art + $"Models/Props/Pirate/{palms[rnd.Next(palms.Length)]}.fbx", pirate,
                      new Vector3(Mathf.Sin(a + 0.02f) * (r - 14f), 0, Mathf.Cos(a + 0.02f) * (r - 14f)), (float)rnd.NextDouble() * 360f, 2f + (float)rnd.NextDouble() * 1.5f);
        }

        // Landmarks and water dressing, so the horizon has something to read against.
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f + 0.2f;
            Place(root, Art + $"Models/Props/Watercraft/{(i % 3 == 0 ? "buoy-flag" : "buoy")}.fbx", craft,
                  new Vector3(Mathf.Sin(a) * 300f, -0.5f, Mathf.Cos(a) * 300f), 0, 2.6f);
        }
        Place(root, Art + "Models/Props/Pirate/ship-wreck.fbx", pirate, new Vector3(-236, -0.6f, 96), 140, 2.6f);
        Place(root, Art + "Models/Props/Pirate/tower-complete-large.fbx", pirate, new Vector3(30, 0, 10), 15, 3.2f);
        Place(root, Art + "Models/Props/Pirate/structure-platform-dock.fbx", pirate, new Vector3(214, -0.2f, -60), 90, 3f);
    }

    static Bounds Bounds(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        return b;
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
            ("Nodey",    "oodi", "Nodey",  1,  0,  0, -1, "Runs the node that never misses a block."),
            ("Bitsy",    "ooli", "Bitsy",  0,  2,  0, -2, "Ships fast, brakes late."),
            ("Chunk",    "oobi", "Chunk",  2, -1, -1,  0, "Top speed, turns like a ferry."),
            ("Pip",      "oopi", "Pip",   -1,  1,  2, -2, "Threads corners nobody else sees."),
            ("Bolt",     "oozi", "Bolt",   1,  1, -1, -1, "All gas, some brakes."),
            ("Tank",     "oodi", "Tank",  -1, -1,  0,  2, "Hard to move, harder to pass."),
            ("Runa",     "ooli", "Runa",   0,  1,  1, -2, "Never lifts."),
            ("Vee",      "oopi", "Vee",    1, -1,  1, -1, "Late braker, early apex."),
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
        MenuSetup.Fonts();
        var display = MenuSetup.display;
        var body = MenuSetup.body;
        var paper = new Color(0.965f, 0.945f, 0.894f);
        var sand = new Color(0.957f, 0.773f, 0.404f);

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400, 1080);
        scaler.matchWidthOrHeight = 1f;

        var safe = new GameObject("SafeArea", typeof(RectTransform));
        safe.transform.SetParent(canvasGo.transform, false);
        var safeRt = (RectTransform)safe.transform;
        safeRt.anchorMin = Vector2.zero; safeRt.anchorMax = Vector2.one;
        safeRt.offsetMin = Vector2.zero; safeRt.offsetMax = Vector2.zero;
        safe.AddComponent<SafeArea>();

        // steering: the whole screen is the drag area, the buttons sit on top of it
        var steer = Rect("SteerArea", safe.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        steer.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        var steerArea = steer.gameObject.AddComponent<TouchSteerArea>();
        new SerializedObject(steerArea).Do(s => s.FindProperty("input").objectReferenceValue = input);

        Button("Drift", safe.transform, input, HoldButton.Control.Drift, "red_button_rectangle_depth_gradient", new Color(0.937f, 0.416f, 0.294f), new Vector2(1, 0), new Vector2(-70, 70), new Vector2(360, 200), "DRIFT", display);
        Button("Item", safe.transform, input, HoldButton.Control.Item, "yellow_button_rectangle_depth_flat", new Color(0.957f, 0.773f, 0.404f), new Vector2(1, 0), new Vector2(-450, 70), new Vector2(240, 200), "ITEM", display);
        Button("Brake", safe.transform, input, HoldButton.Control.Brake, "grey_button_rectangle_depth_flat", new Color(0.24f, 0.31f, 0.35f), new Vector2(0, 0), new Vector2(70, 70), new Vector2(260, 160), "BRAKE", display);

        var position = Text("Position", safe.transform, display, "1st", 130, paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(48, -30), new Vector2(420, 150));
        var lap = Text("Lap", safe.transform, body, "LAP 1/2", 52, paper, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-48, -34), new Vector2(520, 70));
        var time = Text("Time", safe.transform, body, "0:00.00", 46, sand, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-48, -106), new Vector2(520, 70));
        var item = Text("Item", safe.transform, body, "", 42, sand, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(-48, 300), new Vector2(620, 60));
        var big = Text("Big", safe.transform, display, "3", 240, paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1400, 300));
        big.enabled = false;
        var fps = Text("Fps", safe.transform, body, "", 26, new Color(1, 1, 1, 0.45f), TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(52, -180), new Vector2(1200, 40));

        var fpsCounter = canvasGo.AddComponent<FpsCounter>();
        new SerializedObject(fpsCounter).Do(s => s.FindProperty("label").objectReferenceValue = fps);

        hud = canvasGo.AddComponent<RaceHud>();
        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("lapLabel").objectReferenceValue = lap;
        hudSo.FindProperty("positionLabel").objectReferenceValue = position;
        hudSo.FindProperty("timeLabel").objectReferenceValue = time;
        hudSo.FindProperty("itemLabel").objectReferenceValue = item;
        hudSo.FindProperty("bigLabel").objectReferenceValue = big;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        Results(safe.transform, display, body, paper, sand);
        return canvasGo;
    }

    /// End-of-race card: order, times, and the two things you can do next.
    static void Results(Transform parent, TMP_FontAsset display, TMP_FontAsset body, Color paper, Color sand)
    {
        var flow = new GameObject("ScreenFlow").AddComponent<ScreenFlow>();
        var screen = Rect("Results", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // The dim and the card switch on together; leaving the dim on the always-active root greyed out the race.
        var dim = Rect("Dim", screen, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.06f, 0.09f, 0.72f);

        var card = Rect("Card", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 860));
        var panel = card.gameObject.AddComponent<Image>();
        panel.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Textures/UI/Kit/blue_button_rectangle_flat.png");
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = 0.55f;
        panel.color = new Color(0.071f, 0.227f, 0.282f, 0.97f);

        var headline = Text("Headline", card, display, "FINISH", 100, sand, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1000, 120));
        var table = Text("Table", card, body, "", 44, paper, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(880, 420));
        var status = Text("Status", card, body, "", 38, new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(900, 60));

        var again = TextButton("Again", card, "RACE AGAIN", "red_button_rectangle_depth_gradient", new Color(0.937f, 0.416f, 0.294f), paper, 46, display, new Vector2(0.5f, 0), new Vector2(-270, 90), new Vector2(480, 120));
        var menu = TextButton("Menu", card, "MENU", "grey_button_rectangle_depth_flat", new Color(0.24f, 0.31f, 0.35f), paper, 46, display, new Vector2(0.5f, 0), new Vector2(270, 90), new Vector2(480, 120));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(again.onClick, flow.Restart);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(menu.onClick, flow.BackToMenu);

        var results = screen.gameObject.AddComponent<ResultsPanel>();
        var so = new SerializedObject(results);
        so.FindProperty("panel").objectReferenceValue = dim.gameObject;
        so.FindProperty("headline").objectReferenceValue = headline;
        so.FindProperty("table").objectReferenceValue = table;
        so.FindProperty("status").objectReferenceValue = status;
        so.ApplyModifiedPropertiesWithoutUndo();
        screen.gameObject.SetActive(true);
        dim.gameObject.SetActive(false);
    }

    static void Button(string name, Transform parent, InputManager input, HoldButton.Control control, string sprite, Color tint, Vector2 anchor, Vector2 offset, Vector2 dims, string caption, TMP_FontAsset font)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Kit/{sprite}.png");
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.55f;
        img.color = new Color(tint.r, tint.g, tint.b, 0.82f);
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        var hold = rt.gameObject.AddComponent<HoldButton>();
        var so = new SerializedObject(hold);
        so.FindProperty("control").enumValueIndex = (int)control;
        so.FindProperty("input").objectReferenceValue = input;
        so.FindProperty("fade").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();

        var label = Text(name + "Text", rt, font, caption, 40, new Color(1, 1, 1, 0.92f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
    }

    static UnityEngine.UI.Button TextButton(string name, Transform parent, string caption, string sprite, Color tint, Color textColour, float size, TMP_FontAsset font, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Kit/{sprite}.png");
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.55f;
        img.color = tint;
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        var label = Text(name + "Label", rt, font, caption, size, textColour, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = new Vector2(0, -4);
        return button;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color colour, TextAlignmentOptions align, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = colour; t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
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
