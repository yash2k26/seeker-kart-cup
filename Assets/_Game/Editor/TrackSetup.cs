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

// Generates the four circuits and their whole race scenes headlessly. One closed spline gives us the road mesh,
// the AI waypoint chain, the checkpoints, the start grid and the prop placement; a heightfield flattened to that
// spline gives every map its terrain. Run: Unity -batchmode -projectPath . -executeMethod TrackSetup.BuildRace -quit
// (with graphics: BuildOne renders each map's track-select thumbnail).
public static partial class TrackSetup
{
    // Four circuits from one generator and a theme table (TrackThemes.cs). Order, ids, names and blurbs come from
    // KartCup.Tracks; Themes[i] is the look and shape of Tracks.At(i), and Race{i+1}.unity is its scene.
    // The circuits are also the cup's difficulty ramp: open and flowing, then hairpins, then a tight technical one
    // on a narrower road, then the flat-out stadium. Measured before building — lap 1371/1126/1054/1279 m,
    // 95th-percentile corner 3.6/6.4/20.5/4.8 deg per sample — so they differ in character, not just in dressing.
    static Theme V;                                          // the circuit currently being generated
    static int Index;                                        // its index in Tracks / Themes
    static string ScenePath => $"Assets/_Game/Scenes/{Tracks.SceneOf(Index)}.unity";
    static string MeshDir => Art + $"Models/Track/{Tracks.SceneOf(Index)}";
    const string Art = "Assets/_Game/Art/";
    const string Prefabs = "Assets/_Game/Prefabs/";
    const string PKP = "Assets/ThirdParty/PowerslideKartPhysics/";

    const float Kerb = 1.4f;
    static float HalfWidth => V.halfWidth;
    static int ControlPoints => V.controlPoints;
    static int PerSegment => Mathf.Max(8, 384 / V.controlPoints);   // ~384 samples whatever the control count

    static Vector3[] centre;      // sampled centreline, with elevation once TerrainGen.Profile has run
    static Vector3[] right;       // road right vector per sample

    [MenuItem("Build/Build Race Scenes")]
    public static void BuildRace()
    {
        // Drop any scene from a previous single-track build so the list cannot keep a stale "Race.unity".
        EditorBuildSettings.scenes = EditorBuildSettings.scenes
            .Where(s => !s.path.StartsWith("Assets/_Game/Scenes/Race")).ToArray();
        Geometry.Clear();
        FeelSetup.Build();   // the race-feel FX the scenes and the karts load (bubble material, pops, puffs, dust)
        for (int i = 0; i < Tracks.Count; i++)
        {
            var theme = System.Array.Find(Themes, t => t.id == Tracks.At(i).id);
            if (theme == null) { Debug.LogError($"[track] no theme for track '{Tracks.At(i).id}'"); continue; }
            BuildOne(theme, i);
        }
        WriteGeometry();
        Debug.Log($"[track] built {Tracks.Count} circuits");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// One circuit only (-track N, 1-based like the scenes), for iterating on a map's look: a third of the time.
    public static void BuildTrack()
    {
        int track = 1;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-track") int.TryParse(args[i + 1], out track);
        int index = Mathf.Clamp(track - 1, 0, Tracks.Count - 1);
        var theme = System.Array.Find(Themes, t => t.id == Tracks.At(index).id);
        BuildOne(theme, index);
        Debug.Log($"[track] built {Tracks.At(index).name} only");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void BuildOne(Theme theme, int index)
    {
        V = theme; Index = index;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Sample();
        // Elevation goes onto the centreline before any geometry exists, so road, kerbs, walls, waypoints,
        // checkpoints, grid and props all inherit it.
        TerrainGen.Profile(centre, V.shape, Length());
        Analyse();

        Environment();
        RenderMap();
        var road = Road();
        var waypoints = Waypoints();
        var checkpoints = Checkpoints();
        Geometry[V.id] = GeometryJson(checkpoints);
        var grid = StartGrid();
        Barriers();
        Dressing();
        ItemBoxes();

        // ---- systems ----
        var boot = new GameObject("Boot");
        boot.AddComponent<GameBoot>();
        boot.AddComponent<HitStop>();
        AudioSetup.Build(menu: false, map: V.id);

        var input = Instantiate(PKP + "Prefabs/Scene/Input Manager.prefab", "Input Manager").GetComponent<InputManager>();
        input.enableStandardInput = false;                 // Android runs the new Input System only; legacy axes would throw
        input.enableMobileInput = true;
        input.mobileAutoAccel = 0f;                        // as built; ControlLayout turns AUTO GAS (the default setting) on at runtime
        input.mobileDriftHoldSteer = 0.55f;
        EditorUtility.SetDirty(input);

        // our item set (ItemSetup) if it has been built, else Powerslide's four
        Instantiate(File.Exists(Prefabs + "Items/Items.prefab") ? Prefabs + "Items/Items.prefab" : PKP + "Prefabs/Items/Items.prefab", "Items");

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 67; cam.nearClipPlane = 0.3f; cam.farClipPlane = V.farClip; cam.clearFlags = CameraClearFlags.Skybox;   // far enough to keep the mountains
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var kartCam = camGo.AddComponent<KartCamera>();
        kartCam.useLegacyInput = false;                    // it would poll legacy axes every frame otherwise
        kartCam.initialDist = 4.2f; kartCam.initialHeight = 2.3f; kartCam.smoothRate = 9f;  // CameraAngles' CHASE: close, kart big on screen
        kartCam.lookAtHeight = 1.1f;      // ~16 deg down: any lower and the driver's head sits over the road ahead
        camGo.AddComponent<CameraFeel>();
        var angles = camGo.AddComponent<CameraAngles>();
        new SerializedObject(angles).Do(a => a.FindProperty("kartCamera").objectReferenceValue = kartCam);
        camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

        var volumeGo = new GameObject("Post");
        var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = PostProfile();

        var session = new GameObject("RaceSession").AddComponent<RaceSession>();   // lap/checkpoint times for the Worker; nothing reached the leaderboard without it
        new SerializedObject(session).Do(s => s.FindProperty("trackId").stringValue = Tracks.At(Index).id);   // what the Worker keys the leaderboard on
        var race = new GameObject("RaceManager").AddComponent<RaceManager>();
        race.totalLaps = 2;
        race.checkpoints = checkpoints;

        var hud = Hud(input, angles, out var hudComp);

        var spawner = new GameObject("RaceSpawner").AddComponent<RaceSpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("roster").objectReferenceValue = Roster();
        so.FindProperty("garage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
        Fill(so.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).Where(k => k != null).ToArray());
        Fill(so.FindProperty("driverPrefabs"), Directory.GetFiles(Prefabs + "Drivers", "*.prefab").Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        Fill(so.FindProperty("gridSlots"), grid.Cast<Object>().ToArray());
        so.FindProperty("firstWaypoint").objectReferenceValue = waypoints;
        so.FindProperty("raceCamera").objectReferenceValue = kartCam;
        so.FindProperty("racers").intValue = 6;
        so.ApplyModifiedPropertiesWithoutUndo();

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        Thumbnail(cam);      // here, with the scene complete and still open: re-opening scenes to render is a known trap

        EditorSceneManager.SaveScene(scene, ScenePath);
        // Menu is the entry point, so the race always goes last in the build list.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"[track] {Tracks.At(Index).name} ({V.id}): lap {Length():F0} m, {centre.Length} samples, {corners.Count} corners, {checkpoints.Length} checkpoints, {grid.Length} grid slots, " +
                  $"road grade max {TerrainGen.MaxGrade * 100f:F1}% kink {TerrainGen.MaxKink * 100f:F2}%, terrain {terrainTris} tris, {props} props / {propTris} tris");
    }

    /// The track-select card: a 3/4 aerial from behind the start straight, looking along it toward the first corner.
    /// The shared post profile, or a per-map copy with the theme's overrides applied (see Theme.post).
    static UnityEngine.Rendering.VolumeProfile PostProfile()
    {
        const string basePath = "Assets/Settings/PostProfile.asset";
        var basis = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(basePath);
        if (V.post == null) return basis;
        // The map's own copy keeps its GUID across builds: deleting and re-copying the asset gave it a new one every
        // time, so a scene committed without its .meta pointed at nothing and the map lost its grade.
        string path = $"Assets/Settings/Post_{V.id}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
        foreach (var c in profile.components.ToArray()) { profile.components.Remove(c); if (c != null) Object.DestroyImmediate(c, true); }
        foreach (var c in basis.components)
        {
            var copy = Object.Instantiate(c);
            copy.name = c.GetType().Name; copy.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(copy, profile);
            profile.components.Add(copy);
        }
        V.post(profile);
        profile.isDirty = true;
        foreach (var c in profile.components) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static void Thumbnail(Camera cam)
    {
        int n = centre.Length;
        // Key art: the tightest corner carries each map's landmarks (headland, cabins, rock wall, stands), so frame
        // that from low on the approach — a high shot over the first corner showed mostly empty ground.
        var tight = Tightest();
        var first = tight.Count > 0 ? tight[0] : new Corner { start = Wrap(Samples(140f), n), apex = Wrap(Samples(160f), n), outside = -1 };
        int back = Wrap(first.start - Samples(70f), n);
        // From the outside of the corner, except on the speedway: its stands face the track, so from the outside the
        // key art was the blank back of a grandstand. Shoot it from the infield instead.
        float side = V.id == "speedway" ? -first.outside : first.outside;
        Vector3 from = centre[back] + right[back] * (HalfWidth * 2.4f * side) + Vector3.up * 15f;
        Vector3 at = centre[first.apex] + Vector3.up * 4f;
        cam.transform.position = from;
        cam.transform.LookAt(at);
        float fov = cam.fieldOfView; cam.fieldOfView = 52f;

        Preview.Batcher(false);   // a manual cam.Render() in edit mode reuses the batcher's constants: every object comes out one colour
        int w = 2048, h = 1024;   // shown full-bleed at 2400 wide: 1024 was visibly soft
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Preview.Batcher(true);
        cam.fieldOfView = fov;

        const string dir = Art + "Textures/UI/Tracks";
        Directory.CreateDirectory(dir);
        string path = $"{dir}/{V.id}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        AssetDatabase.ImportAsset(path);      // the UI folder rule makes it a sprite
    }

    // ---------- minimap ----------
    static Sprite mapSprite; static Vector2 mapMin; static float mapSize;

    /// The centreline as a pale line with a dark outline on a transparent square, plus the world-to-picture fit the
    /// HUD's Minimap needs. Drawn by hand: a 256 px texture of a 400–500 m circuit is a two-metre pixel.
    static void RenderMap()
    {
        float minX = centre.Min(c => c.x), maxX = centre.Max(c => c.x), minZ = centre.Min(c => c.z), maxZ = centre.Max(c => c.z);
        mapSize = Mathf.Max(maxX - minX, maxZ - minZ) * 1.18f;
        mapMin = new Vector2((minX + maxX) * 0.5f - mapSize * 0.5f, (minZ + maxZ) * 0.5f - mapSize * 0.5f);
        const int N = 256;
        var px = new Color32[N * N];
        void Disc(Vector3 w, Color32 c, float radius)
        {
            float u = (w.x - mapMin.x) / mapSize * N, v = (w.z - mapMin.y) / mapSize * N;
            int r = Mathf.CeilToInt(radius);
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > radius * radius) continue;
                int ix = Mathf.RoundToInt(u) + x, iy = Mathf.RoundToInt(v) + y;
                if (ix < 0 || iy < 0 || ix >= N || iy >= N) continue;
                px[iy * N + ix] = c;
            }
        }
        void Stroke(Color32 c, float radius)
        {
            for (int i = 0; i < centre.Length; i++)
            {
                Vector3 a = centre[i], b = centre[Wrap(i + 1, centre.Length)];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / mapSize * N));
                for (int k = 0; k <= steps; k++) Disc(Vector3.Lerp(a, b, k / (float)steps), c, radius);
            }
        }
        Stroke(new Color32(10, 21, 28, 235), 7.5f);          // outline
        Stroke(new Color32(244, 241, 234, 255), 4.5f);      // road
        Disc(centre[0], new Color32(255, 90, 54, 255), 7f); // start line
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        const string dir = Art + "Textures/UI/Tracks";
        Directory.CreateDirectory(dir);
        string path = $"{dir}/{V.id}_map.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        mapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (mapSprite == null) Debug.LogWarning("[track] minimap sprite did not import: " + path);
    }

    // ---------- geometry ----------
    static void Sample()
    {
        var ctrl = new Vector3[ControlPoints];
        for (int i = 0; i < ControlPoints; i++)
        {
            float t = i / (float)ControlPoints * Mathf.PI * 2f;
            if (V.straight > 0f) { ctrl[i] = Stadium(i); continue; }
            float r = V.radius * (1f + V.ampA * Mathf.Sin(V.harmA * t) + V.ampB * Mathf.Cos(V.harmB * t));
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

    /// Control point i of a stadium: straights of V.straight along x, semicircles of V.radius, clockwise from the
    /// middle of the front straight (so sample 0 is the start line on a straight, like the other circuits), and a
    /// chicane — two adjacent points pushed out and in by V.chicane — in the middle of the back straight.
    static Vector3 Stadium(int i)
    {
        float R = V.radius, S = V.straight, L = 2f * S + 2f * Mathf.PI * R;
        float u = i / (float)ControlPoints * L;
        Vector3 p, normal;
        if (u < S / 2f) { p = new Vector3(u, 0, R); normal = Vector3.forward; }
        else if (u < S / 2f + Mathf.PI * R) { float a = (u - S / 2f) / R; normal = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); p = new Vector3(S / 2f, 0, 0) + normal * R; }
        else if (u < S * 1.5f + Mathf.PI * R) { p = new Vector3(S / 2f - (u - S / 2f - Mathf.PI * R), 0, -R); normal = Vector3.back; }
        else if (u < S * 1.5f + 2f * Mathf.PI * R) { float a = (u - S * 1.5f - Mathf.PI * R) / R; normal = new Vector3(-Mathf.Sin(a), 0, -Mathf.Cos(a)); p = new Vector3(-S / 2f, 0, 0) + normal * R; }
        else { p = new Vector3(-S / 2f + (u - S * 1.5f - 2f * Mathf.PI * R), 0, R); normal = Vector3.forward; }
        int mid = ControlPoints / 2;
        if (i == mid) p += normal * V.chicane;
        if (i == mid + 1) p -= normal * V.chicane;
        return p;
    }

    static int Wrap(int i, int n) => ((i % n) + n) % n;
    static Vector3 Forward(int i) => (centre[Wrap(i + 1, centre.Length)] - centre[Wrap(i - 1, centre.Length)]).normalized;

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) =>
        0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

    static GameObject Road()
    {
        var unlit = Shader.Find("KartCup/Unlit Colormap");
        var roadMat = RoadMaterial();
        arc = new float[centre.Length];
        for (int i = 1; i < centre.Length; i++) arc[i] = arc[i - 1] + Vector3.Distance(centre[i - 1], centre[i]);
        var kerbMat = LoadOrCreate(Art + "Materials/Kerb.mat", unlit, new Color(0.93f, 0.93f, 0.95f));

        var redMat = LoadOrCreate(Art + "Materials/KerbRed.mat", unlit, new Color(0.80f, 0.20f, 0.18f));
        var lineMat = LoadOrCreate(Art + "Materials/RoadLine.mat", unlit, new Color(0.92f, 0.92f, 0.88f));
        var darkMat = LoadOrCreate(Art + "Materials/RoadDark.mat", unlit, new Color(0.18f, 0.18f, 0.20f));

        var root = new GameObject("Track");
        var preset = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Default Road.asset");
        // The road in twice the pieces of everything else: it is half the screen, shaded per pixel by every light that
        // reaches its piece (eight at most). At ~55 m a piece holds three lamps and the headlights, not all eight.
        foreach (var road in Strips("Road", root.transform, roadMat, -HalfWidth, HalfWidth, 0.02f, metres: true, pieces: Chunks * 2))
        {
            road.AddComponent<MeshCollider>();
            road.AddComponent<GroundSurface>().props = preset;
        }

        // kerbs in alternating red/white blocks, the way a real circuit reads at speed
        Strips("KerbL", root.transform, kerbMat, -HalfWidth - Kerb, -HalfWidth, 0.03f);
        Strips("KerbR", root.transform, kerbMat, HalfWidth, HalfWidth + Kerb, 0.03f);
        Strips("KerbLRed", root.transform, redMat, -HalfWidth - Kerb, -HalfWidth, 0.035f, 4, 4);
        Strips("KerbRRed", root.transform, redMat, HalfWidth, HalfWidth + Kerb, 0.035f, 4, 4, 4);

        // dashed centre line and solid edge lines
        Strips("CentreLine", root.transform, lineMat, -0.35f, 0.35f, 0.035f, 5, 7);
        Strips("EdgeL", root.transform, lineMat, -HalfWidth + 0.3f, -HalfWidth + 0.75f, 0.035f);
        Strips("EdgeR", root.transform, lineMat, HalfWidth - 0.75f, HalfWidth - 0.3f, 0.035f);

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
                            int on = 0, int off = 0, int phase = 0, int start = 0, int count = 0, bool metres = false, Mesh container = null)
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
            if (on > 0 && off > 0 && ((start + k + phase) % (on + off)) >= on) continue;   // the lap's rhythm, whatever piece this is
            int j = Wrap(i + 1, n);
            int v = verts.Count;
            foreach (var (sample, side) in new[] { (i, from), (i, to), (j, from), (j, to) })
            {
                verts.Add(centre[sample] + right[sample] * side + Vector3.up * height);
                // metres (across, along) for a textured strip; otherwise one flat colour off the colormap
                uvs.Add(metres ? new Vector2(side, sample == i ? arc[i] : arc[i] + Vector3.Distance(centre[i], centre[j])) : new Vector2(0.03f, 0.5f));
                norms.Add(Vector3.up);
            }
            // wound so the face points up
            tris.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
        }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts.ToArray(), uv = uvs.ToArray(), normals = norms.ToArray(), triangles = tris.ToArray() };
        mesh.RecalculateBounds();
        if (metres) mesh.RecalculateTangents();          // the road's normal map needs them
        Directory.CreateDirectory(MeshDir);
        if (container != null) AssetDatabase.AddObjectToAsset(mesh, container);      // a strip's pieces share one asset file
        else AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}.asset");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        return go;
    }

    /// A strip in Chunks pieces round the lap, so a phone lights each piece by its own nearest lamps (it caps a
    /// renderer at eight lights). The first piece keeps the plain name: the probes look for "Road".
    const int Chunks = 12;
    static float[] arc;      // metres along the lap at each sample, for textured strips
    static List<GameObject> Strips(string name, Transform parent, Material mat, float from, float to, float height,
                                   int on = 0, int off = 0, int phase = 0, bool metres = false, int pieces = Chunks)
    {
        var list = new List<GameObject>();
        Mesh container = null;                       // the first piece's asset holds the rest as sub-assets
        int n = centre.Length, per = (n + pieces - 1) / pieces;
        for (int k = 0; k < pieces; k++)
        {
            int start = k * per, count = Mathf.Min(per, n - start);
            if (count <= 0) break;
            var piece = Strip(k == 0 ? name : $"{name}_{k:00}", parent, mat, from, to, height, on, off, phase, start, count, metres, container);
            if (k == 0) container = piece.GetComponent<MeshFilter>().sharedMesh;
            list.Add(piece);
        }
        return list;
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

    // ---- the Worker's referee reads the same circuit the scene is built from (worker/src/tracks.json) ----
    static readonly SortedDictionary<string, string> Geometry = new();
    const float GateSlack = 6f;      // the gate trigger reaches this far past the kerb on each side (Checkpoints below)

    static string N(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// Centreline (x, z), road half-width, gate half-width and each gate's position and forward (x, z): what the
    /// Worker needs to follow a kart round the lap from its streamed or uploaded positions.
    static string GeometryJson(Checkpoint[] gates)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"{{\"lengthM\":{N(Length())},\"laps\":2,\"halfWidth\":{N(HalfWidth)},\"kerb\":{N(Kerb)},\"gateHalfWidth\":{N(HalfWidth + Kerb + GateSlack)},");
        sb.Append("\"gates\":[");
        for (int i = 0; i < gates.Length; i++)
        {
            var t = gates[i].transform; var f = t.forward;
            sb.Append(i == 0 ? "" : ",").Append($"[{N(t.position.x)},{N(t.position.y)},{N(t.position.z)},{f.x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)},{f.z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}]");
        }
        sb.Append("],\"centre\":[");
        for (int i = 0; i < centre.Length; i++) sb.Append(i == 0 ? "" : ",").Append($"[{N(centre[i].x)},{N(centre[i].z)}]");
        return sb.Append("]}").ToString();
    }

    static void WriteGeometry()
    {
        var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "worker", "src", "tracks.json"));
        var body = string.Join(",\n", Geometry.Select(kv => $"\"{kv.Key}\":{kv.Value}"));
        File.WriteAllText(path, "{\n" + body + "\n}\n");
        Debug.Log($"[track] wrote the referee's geometry for {Geometry.Count} circuits to {path}");
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
            box.size = new Vector3((HalfWidth + Kerb + GateSlack) * 2f, 8f, 2f);
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

    static int terrainTris;

    static void Environment()
    {
        // Sky: Unity's procedural skybox, one material per map, with the sun disc following the directional light;
        // or, after dark, a photographed sky cube (the teaser's moonrise) turned so the moon hangs where the theme says.
        var sky = LoadOrCreate(Art + $"Materials/Sky_{V.id}.mat", Shader.Find("Skybox/Procedural"), Color.white);
        if (!string.IsNullOrEmpty(V.sky))
        {
            sky.shader = Shader.Find("Skybox/Cubemap");
            sky.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Cubemap>(Art + $"Textures/Sky/{V.sky}.png"));
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));           // this shader doubles its tint
            sky.SetFloat("_Exposure", V.skyExposure); sky.SetFloat("_Rotation", V.skyRotation);
        }
        else
        {
            sky.shader = Shader.Find("Skybox/Procedural");
            sky.SetColor("_SkyTint", V.skyTint); sky.SetColor("_GroundColor", V.horizon);
            sky.SetFloat("_Exposure", V.exposure); sky.SetFloat("_AtmosphereThickness", V.atmosphere);
            sky.SetFloat("_SunSize", V.sunSize); sky.SetFloat("_SunSizeConvergence", V.sunConvergence);
        }
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        // Gradient ambient: sky from above, a bounce from the ground colour below — flat ambient is what made
        // everything read as one colour.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = V.ambientSky;
        RenderSettings.ambientEquatorColor = V.ambientEquator;
        RenderSettings.ambientGroundColor = V.ambientGround;
        // Fog in the sky's horizon colour, so props fade into the sky instead of into a grey.
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = V.fogStart; RenderSettings.fogEndDistance = V.fogEnd;
        RenderSettings.fogColor = V.fog;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(V.sun);
        sun.shadows = LightShadows.Soft; sun.intensity = V.sunIntensity;
        sun.color = V.sunColor;
        sun.shadowStrength = 0.72f; sun.shadowBias = 0.06f; sun.shadowNormalBias = 0.5f;
        RenderSettings.sun = sun;
        if (V.night)
        {
            sun.shadowStrength = 0.6f;                   // moon shadows stay soft-edged and open
            RenderSettings.reflectionIntensity = 0.2f;   // the wet road mirrors the sky, but the lamps have to win
        }

        // Terrain: the theme's heightfield, painted per face, on the "KartCup/Terrain" shader (albedo = tint x vertex
        // colour). If that shader is not imported yet the map still builds, on Simple Lit in the palette's mid tone.
        // The material lives with the terrain meshes, not in Art/Materials: LookSetup moves every KartCup/* material
        // it finds there onto Simple Lit, which would drop the vertex colours and leave the terrain white.
        var terrainShader = Shader.Find("KartCup/Terrain");
        var lit = Shader.Find("Universal Render Pipeline/Simple Lit");
        Directory.CreateDirectory(MeshDir);
        var mat = LoadOrCreate($"{MeshDir}/Terrain_{V.id}.mat", terrainShader != null ? terrainShader : lit, Color.white);
        if (terrainShader != null)
        {
            mat.shader = terrainShader;
            mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_ShadowTint", 0.5f);
            mat.SetFloat("_Wrap", V.sun.x < 20f && !V.night ? 0.55f : 0.3f);   // a 17 deg sun barely grazes a flat field: wrap the lambert so dusk stays readable
            var detail = string.IsNullOrEmpty(V.groundDetail) ? null : Tex("Surfaces/" + V.groundDetail);
            mat.SetTexture("_DetailMap", detail);
            mat.SetFloat("_DetailTile", V.groundDetailTile); mat.SetFloat("_DetailStrength", detail != null ? 0.85f : 0f);
        }
        else
        {
            Debug.LogWarning("[track] KartCup/Terrain shader missing: terrain on Simple Lit without vertex colours");
            mat.shader = lit; mat.SetColor("_BaseColor", V.ground);
        }
        EditorUtility.SetDirty(mat);
        TerrainGen.Build(centre, V.shape, HalfWidth + Kerb + 1.5f, V.palette, mat, Offroad(), MeshDir, out terrainTris);

        // Water: the sea round the island, or a lake in a basin. One shared material; its surf ring is radial from
        // the origin at the island's shoreline, so the lake (well inside it) gets the caustics without the surf.
        if (!float.IsNaN(V.sea)) Water("Water", new Vector3(0, V.sea, 0), 200f);
        if (V.lakeRadius > 0f) Water("Lake", new Vector3(V.lake.x, -1.3f, V.lake.y), V.lakeRadius * 0.3f).radius = V.lakeRadius;

        // After dark (or at dusk, in mist) the karts spawn with their lamps lit (RaceSpawner reads this marker).
        if (V.night || V.headlights)
        {
            var night = new GameObject("Night").AddComponent<NightScene>();
            new SerializedObject(night).Do(s =>
            {
                s.FindProperty("lens").objectReferenceValue = Emissive("Headlight_Lens", new Color(1f, 0.95f, 0.85f), 3f);
                s.FindProperty("tail").objectReferenceValue = Emissive("Tail_Lens", new Color(1f, 0.12f, 0.08f), 2.4f);
            });
        }
    }

    static WaterBody Water(string name, Vector3 pos, float scale)
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = name;
        water.transform.position = pos;
        water.transform.localScale = new Vector3(scale, 1, scale);
        water.GetComponent<MeshRenderer>().sharedMaterial = WaterMaterial();
        Object.DestroyImmediate(water.GetComponent<Collider>());
        return water.AddComponent<WaterBody>();   // where the surface is: spray in the shallows, out of bounds when deep
    }

    /// Crash barriers, but only where a circuit would really have them: the outside of a corner. A wall down every
    /// straight would turn the map into a corridor and take away the ground you are allowed to run wide onto.
    static void Barriers()
    {
        var unlit = Shader.Find("KartCup/Unlit Colormap");
        var white = LoadOrCreate(Art + "Materials/Barrier.mat", unlit, new Color(0.88f, 0.88f, 0.90f));
        var red = LoadOrCreate(Art + "Materials/BarrierRed.mat", unlit, new Color(0.78f, 0.16f, 0.15f));
        var root = new GameObject("Barriers").transform;
        int n = centre.Length;
        foreach (var c in corners)
        {
            int from = Wrap(c.start - 3, n), len = Mathf.Min(c.count + 6, n - 1);
            Wall(root, $"Barrier{from}", white, red, (HalfWidth + Kerb + 0.7f) * c.outside, 1.15f, from, len);
        }
    }

    /// A vertical ribbon along the centreline: one mesh, one draw call, one static collider.
    static void Wall(Transform parent, string name, Material body, Material stripe, float lateral, float height, int start, int count)
    {
        int n = centre.Length;
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        var stripeTris = new System.Collections.Generic.List<int>();

        for (int k = 0; k <= count; k++)
        {
            int i = Wrap(start + k, n);
            Vector3 basePos = centre[i] + right[i] * lateral + Vector3.down * 0.3f;   // buried a little: the shoulder sits 5 cm under the road
            Vector3 outward = right[i] * Mathf.Sign(lateral);
            verts.Add(basePos); verts.Add(basePos + Vector3.up * (height + 0.3f));
            norms.Add(-outward); norms.Add(-outward);
            uvs.Add(new Vector2(k * 0.25f, 0)); uvs.Add(new Vector2(k * 0.25f, 1));
            if (k == 0) continue;
            int v = (k - 1) * 2;
            // Alternating panels give the chevron rhythm without needing a texture.
            var target = (k / 2) % 2 == 0 ? tris : stripeTris;
            target.AddRange(new[] { v, v + 1, v + 2, v + 1, v + 3, v + 2 });
        }

        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(tris, 0); mesh.SetTriangles(stripeTris, 1);
        mesh.RecalculateBounds();
        Directory.CreateDirectory(MeshDir);
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}.asset");

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { body, stripe };
        // What stops a kart is its own mesh, not the one drawn. A mesh collider only stops what meets its faces from
        // the front, and the ribbon is wound the same way whichever side of the road it stands on: on half the corners
        // it faced away from the road and karts drove straight through it. (The same panels wound both ways do not
        // survive: the collider's cooking drops the second of two triangles on the same three points.) So the wall
        // is a slab half a metre thick, each face wound to look outward from it, and three metres tall, because a
        // kart off a kerb clears the 1.15 m that is drawn.
        var solid = new Mesh { name = name + "_Solid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var slab = new System.Collections.Generic.List<Vector3>();
        var faces = new System.Collections.Generic.List<int>();
        for (int k = 0; k <= count; k++)
        {
            Vector3 foot = verts[k * 2], top = foot + Vector3.up * 3.3f, back = -norms[k * 2] * 0.5f;   // norms point at the road
            slab.Add(foot); slab.Add(top); slab.Add(foot + back); slab.Add(top + back);
            if (k == 0) continue;
            int v = (k - 1) * 4;
            void Face(int a, int b, int c, int d, Vector3 looks)      // a quad, wound so its front is toward `looks`
            {
                bool flip = Vector3.Dot(Vector3.Cross(slab[b] - slab[a], slab[c] - slab[a]), looks) < 0f;
                faces.AddRange(flip ? new[] { a, c, b, b, c, d } : new[] { a, b, c, b, d, c });
            }
            Face(v, v + 1, v + 4, v + 5, norms[k * 2]);           // the road's side
            Face(v + 2, v + 3, v + 6, v + 7, -norms[k * 2]);      // the far side
        }
        solid.SetVertices(slab); solid.SetTriangles(faces, 0); solid.RecalculateBounds();
        AssetDatabase.CreateAsset(solid, $"{MeshDir}/{name}_Solid.asset");
        go.AddComponent<MeshCollider>().sharedMesh = solid;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
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
        var bubble = FeelSetup.BubbleMaterial;
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        if (box == null) return;
        var root = new GameObject("ItemBoxes").transform;
        root.gameObject.AddComponent<ItemBag>();      // deals each row different items
        for (int lane = 0; lane < 5; lane++)
        {
            for (int i = centre.Length / 12; i < centre.Length; i += centre.Length / 6)   // offset so no cluster sits on the start line
            {
                var row = root.Find("Row" + i) ?? new GameObject("Row" + i).transform;
                row.SetParent(root, false);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(box, row);
                go.transform.SetPositionAndRotation(centre[i] + right[i] * (lane - 2) * 3.2f + Vector3.up * 1.4f,
                                                    Quaternion.LookRotation(Forward(i), Vector3.up));
                // a see-through violet bubble with a gold question mark (Beach Buggy's '?' bubble, Kartel's colours): a quad
                // that ItemBubble turns to the camera and bobs; 1.8 m across, its trigger (0.7 x scale) 1.26 m round
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = bubble;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                go.transform.localScale = Vector3.one * 1.8f;
                go.transform.position += Vector3.up * 0.25f;
                go.AddComponent<ItemBubble>();
            }
        }
    }

    static CharacterDatabase Roster()
    {
        const string dir = "Assets/_Game/Data/";
        Directory.CreateDirectory(dir);
        var db = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(dir + "Characters.asset");
        bool fresh = db == null;
        if (fresh) { db = ScriptableObject.CreateInstance<CharacterDatabase>(); AssetDatabase.CreateAsset(db, dir + "Characters.asset"); }

        // name, kart, driver, speed, accel, handling, weight, turbo, attack  (six pips that sum to 0 so nobody is
        // strictly better: a shape, not an edge). This table is the roster's stats; the tint stays LookSetup's. New
        // names are appended, so a roster that grows (Chase and Beeman arrived on 28 Sep 2026, Maroo on 5 Oct, Andrew on 6 Oct) never resets the
        // select-screen order or the saved character index. A name taken out of the table leaves the roster (Runa and
        // Vee on 6 Oct): the ones after it move up, so Cup.Version was bumped with it.
        var rows = new (string name, string kart, string driver, int sp, int ac, int hd, int wt, int tb, int at, string blurb)[]
        {
            ("Toly",     "oodi", "Toly",   1,  0,  0, -1,  1, -1, "Ships blocks faster than you can read them."),
            ("Mert",     "ooli", "Mert",   0,  2,  0, -2, -1,  1, "Indexes every corner before he takes it."),
            ("Akshay",   "oobi", "Akshay", 2, -1, -1,  0,  1, -1, "Top speed, turns like a ferry."),
            ("Emmett",   "oopi", "Emmett", -1, 1,  2, -2,  0,  0, "Threads corners nobody else sees."),
            ("Bitsy",    "oozi", "Bitsy",  1,  1, -1, -1,  2, -2, "All gas, some brakes."),
            ("Pip",      "oodi", "Pip",   -1, -1,  0,  2, -1,  1, "Hard to move, harder to pass."),
            ("Chase",    "oozi", "Chase",  0,  1,  1, -2, -1,  1, "Knows every dev on the grid. Waves at all of them."),
            ("Beeman",   "oobi", "Beeman", 2,  0, -1, -1, -1,  1, "Bald, fast, and debugging at sixty frames a second."),
            ("Maroo",    "oopi", "Maroo",  0,  0,  1, -1,  1, -1, "Big curls, bigger drifts."),
            ("Andrew",   "ooli", "Andrew", 1,  0,  1,  0, -1, -1, "Reviews every corner. Approves none."),
        };
        var defs = (db.characters ?? System.Array.Empty<CharacterDef>()).Where(d => d != null).ToList();
        foreach (var r in rows)
        {
            var def = defs.FirstOrDefault(d => d.displayName == r.name);
            if (def == null)
            {
                var path = dir + $"Character_{r.name}.asset";
                def = AssetDatabase.LoadAssetAtPath<CharacterDef>(path);
                if (def == null) { def = ScriptableObject.CreateInstance<CharacterDef>(); def.name = r.name; AssetDatabase.CreateAsset(def, path); }
                defs.Add(def);
                Debug.Log($"[track] roster: added {r.name}");
            }
            def.displayName = r.name; def.kartId = r.kart; def.driverId = r.driver; def.blurb = r.blurb;
            def.speed = r.sp; def.accel = r.ac; def.handling = r.hd; def.weight = r.wt; def.turbo = r.tb; def.attack = r.at;
            EditorUtility.SetDirty(def);
        }
        db.characters = defs.Where(d => rows.Any(r => r.name == d.displayName)).ToArray();
        EditorUtility.SetDirty(db);
        return db;
    }

    // ---------- UI ----------
    /// Everything off the road (sand, snow, grass, verge): the kit's Dirt, harsher. Dirt kept 90% of top speed, so
    /// cutting across the infield cost almost nothing; at 68% and with less grip, running wide or cutting a corner
    /// costs real time (6 Oct: "make the maps more difficult"). The racing line, the road and the referee's geometry
    /// (worker/src/tracks.json) are untouched.
    static GroundSurfacePreset Offroad()
    {
        const string path = "Assets/_Game/Data/Offroad.asset";
        var dirt = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Dirt.asset");
        var off = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(path);
        if (off == null)
        {
            off = Object.Instantiate(dirt);
            AssetDatabase.CreateAsset(off, path);
        }
        off.friction = 0.5f; off.speed = 0.68f; off.alwaysSlide = true;
        off.tireMarkMaterial = dirt.tireMarkMaterial; off.tireSnd = dirt.tireSnd;
        EditorUtility.SetDirty(off);
        return off;
    }

    static GameObject Hud(InputManager input, CameraAngles angles, out RaceHud hud)
    {
        MenuSetup.Fonts();
        var display = MenuSetup.display;
        var body = MenuSetup.body;
        var paper = Design.Paper;
        var sand = Design.Muted;      // the HUD's secondary text; the accent is reserved for the primary control

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

        // Speed lines round the edges while boosting (Beach Buggy blurs the edges): behind every other HUD element
        var lines = new GameObject("SpeedLines", typeof(RectTransform), typeof(Image));
        lines.transform.SetParent(canvasGo.transform, false);
        lines.transform.SetAsFirstSibling();
        var linesRt = (RectTransform)lines.transform;
        linesRt.anchorMin = Vector2.zero; linesRt.anchorMax = Vector2.one; linesRt.offsetMin = linesRt.offsetMax = Vector2.zero;
        var linesImg = lines.GetComponent<Image>(); linesImg.sprite = FeelSetup.SpeedLines; linesImg.color = new Color(1f, 0.96f, 0.86f, 0f); linesImg.raycastTarget = false;
        lines.AddComponent<SpeedLines>();

        // Steering is two arrows on the left thumb; the right thumb keeps drift, item and brake.
        var steering = canvasGo.AddComponent<SteerButtons>();
        new SerializedObject(steering).Do(s => s.FindProperty("input").objectReferenceValue = input);

        // One accent: GAS is the action a thumb holds for the whole race, so it carries the accent and nothing else does.
        // The item button lights up only while an item is actually armed (RaceHud tints it), which is a state, not chrome.
        var ink = Design.Ink700;
        Button("SteerLeft", safe.transform, input, HoldButton.Control.SteerLeft, ink, new Vector2(0, 0), new Vector2(64, 64), new Vector2(240, 184), "", display, steering, "arrow_basic_w");
        Button("SteerRight", safe.transform, input, HoldButton.Control.SteerRight, ink, new Vector2(0, 0), new Vector2(328, 64), new Vector2(240, 184), "", display, steering, "arrow_basic_e");
        var gasBtn = Button("Gas", safe.transform, input, HoldButton.Control.Accelerate, Design.Accent, new Vector2(1, 0), new Vector2(-64, 64), new Vector2(288, 184), "GAS", display, steering);
        var driftBtn = Button("Drift", safe.transform, input, HoldButton.Control.Drift, ink, new Vector2(1, 0), new Vector2(-376, 64), new Vector2(224, 184), "DRIFT", display, steering);
        var brakeBtn = Button("Brake", safe.transform, input, HoldButton.Control.Brake, ink, new Vector2(1, 0), new Vector2(-624, 64), new Vector2(184, 150), "BRAKE", display, steering);
        var itemBtn = Button("Item", safe.transform, input, HoldButton.Control.Item, ink, new Vector2(1, 0), new Vector2(-64, 272), new Vector2(288, 128), "ITEM", display, steering);
        // the driver's special, on the left thumb's side above the arrows (Beach Buggy's ability button); lit when charged
        var abilityBtn = Button("Ability", safe.transform, input, HoldButton.Control.Ability, ink, new Vector2(0, 0), new Vector2(64, 272), new Vector2(288, 128), "BURST", display, steering);
        var abilityTrack = Plate("AbilityCharge", safe.transform, new Vector2(0, 0), new Vector2(64, 404), new Vector2(288, 16), new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.75f));
        var abilityFillRt = Rect("Fill", abilityTrack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        abilityFillRt.offsetMin = new Vector2(4, 4); abilityFillRt.offsetMax = new Vector2(-4, -4);
        var abilityFill = abilityFillRt.gameObject.AddComponent<Image>(); abilityFill.sprite = Design.Row; abilityFill.color = Design.Accent; abilityFill.raycastTarget = false;
        abilityFill.type = Image.Type.Filled; abilityFill.fillMethod = Image.FillMethod.Horizontal; abilityFill.fillAmount = 0f;
        abilityTrack.SetParent(abilityBtn.transform, true);   // shown and hidden with the button

        // Readouts sit on dark plates: paper on sky and sand failed contrast on the device shots.
        var deep = new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.66f);
        Plate("PositionPlate", safe.transform, new Vector2(0, 1), new Vector2(32, -20), new Vector2(340, 196), deep);
        Plate("LapPlate", safe.transform, new Vector2(1, 1), new Vector2(-32, -20), new Vector2(342, 156), deep);
        var position = Text("Position", safe.transform, display, "1st", 130, paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(56, -24), new Vector2(420, 150));
        var lap = Text("Lap", safe.transform, body, "LAP 1/2", 52, paper, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-56, -38), new Vector2(520, 70));
        var time = Text("Time", safe.transform, body, "0:00.00", 46, sand, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-56, -108), new Vector2(520, 70));
        var item = Text("Item", safe.transform, body, "", 42, sand, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(-48, 400), new Vector2(620, 60));

        // Drift meter above DRIFT: fills through three tiers while sliding, drains while the boost is spent.
        var meterBack = Plate("BoostMeter", safe.transform, new Vector2(1, 0), new Vector2(-376, 268), new Vector2(224, 24), new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.75f));
        var fillRt = Rect("Fill", meterBack, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fillRt.offsetMin = new Vector2(5, 5); fillRt.offsetMax = new Vector2(-5, -5);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.sprite = Design.Row;
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;
        fill.color = Design.Accent; fill.raycastTarget = false;

        // Pause: top centre, out of both thumbs' way. Back on Android does the same thing.
        // Pause: beside the position plate (the top centre is the race bar's now). Back on Android does the same thing.
        var pauseBtn = IconButton("Pause", safe.transform, "icon_pause", new Vector2(0, 1), new Vector2(396, -28), new Vector2(104, 104));
        var big = Text("Big", safe.transform, display, "3", 240, paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1400, 300));
        big.enabled = false;
        // the start boost: a line under the countdown (StartBoost shows it only while 3-2-1 runs)
        var startHint = Text("StartHint", safe.transform, body, "TAP DRIFT RIGHT ON GO FOR A BOOST", 34, sand, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(1300, 50));
        var startBoost = canvasGo.AddComponent<StartBoost>();
        new SerializedObject(startBoost).Do(b => b.FindProperty("hint").objectReferenceValue = startHint);
        // Under the position readout, on its plate. Track name only: the race number is cup state, and a single race has none.
        Text("Track", safe.transform, body, Tracks.At(Index).name.ToUpperInvariant(), 30,
             Design.Paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(56, -170), new Vector2(700, 40));
        var fps = Text("Fps", safe.transform, body, "", 22, new Color(1, 1, 1, 0.30f), TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(52, 300), new Vector2(1200, 34));

        // AUTO GAS (on by default): no GAS button, DRIFT in its corner. The setting can change mid-race (pause menu).
        var layout = canvasGo.AddComponent<ControlLayout>();
        new SerializedObject(layout).Do(l =>
        {
            l.FindProperty("input").objectReferenceValue = input;
            l.FindProperty("gas").objectReferenceValue = gasBtn.rectTransform;
            l.FindProperty("drift").objectReferenceValue = driftBtn.rectTransform;
            l.FindProperty("brake").objectReferenceValue = brakeBtn.rectTransform;
            l.FindProperty("item").objectReferenceValue = itemBtn.rectTransform;
            l.FindProperty("meter").objectReferenceValue = meterBack;
            l.FindProperty("itemLabel").objectReferenceValue = item.rectTransform;
        });

        var fpsCounter = canvasGo.AddComponent<FpsCounter>();
        new SerializedObject(fpsCounter).Do(s => s.FindProperty("label").objectReferenceValue = fps);

        hud = canvasGo.AddComponent<RaceHud>();
        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("lapLabel").objectReferenceValue = lap;
        hudSo.FindProperty("positionLabel").objectReferenceValue = position;
        hudSo.FindProperty("timeLabel").objectReferenceValue = time;
        hudSo.FindProperty("itemLabel").objectReferenceValue = item;
        hudSo.FindProperty("bigLabel").objectReferenceValue = big;
        hudSo.FindProperty("boostFill").objectReferenceValue = fill;
        hudSo.FindProperty("itemButton").objectReferenceValue = itemBtn.GetComponent<Image>();
        // the held item's glyph on the slot's face, in the violet that reads on the lit (gold) slot
        var itemIconRt = Rect("ItemIcon", itemBtn.transform.Find("ItemFace"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92, 92));
        var itemIcon = itemIconRt.gameObject.AddComponent<Image>();
        itemIcon.color = Design.OnAccent; itemIcon.preserveAspect = true; itemIcon.raycastTarget = false; itemIcon.enabled = false;
        hudSo.FindProperty("itemIcon").objectReferenceValue = itemIcon;
        var ammo = Text("Ammo", itemBtn.transform.Find("ItemFace"), display, "", 40, Design.OnAccent, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(110, 60));
        hudSo.FindProperty("ammoLabel").objectReferenceValue = ammo;
        hudSo.FindProperty("abilityButton").objectReferenceValue = abilityBtn;
        hudSo.FindProperty("abilityFill").objectReferenceValue = abilityFill;
        hudSo.FindProperty("abilityLabel").objectReferenceValue = abilityBtn.GetComponentInChildren<TMP_Text>();
        // an achievement done mid-race: a banner under the race bar for a few seconds (RaceHud.Award)
        var awardPlate = Plate("AwardPlate", safe.transform, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(1040, 68), deep);   // low: the finish words own the top
        var award = Text("Award", awardPlate, display, "", 32, paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 60));
        hudSo.FindProperty("awardPlate").objectReferenceValue = awardPlate.gameObject;
        hudSo.FindProperty("awardLabel").objectReferenceValue = award;
        var held = new[] { "Rocket", "Homing Item", "Bomb", "Nitro", "Bouncer", "Sun Bomb", "Trap", "Turbo" };   // Turbo: the slot said ITEM
        var glyphNames = hudSo.FindProperty("glyphNames"); glyphNames.arraySize = held.Length;
        for (int i = 0; i < held.Length; i++) glyphNames.GetArrayElementAtIndex(i).stringValue = held[i];
        Fill(hudSo.FindProperty("glyphs"), held.Select(h => (Object)Design.Icon(Design.ItemGlyph(h))).ToArray());
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        // Camera angle: the pill names the current view; tapping it drops a picker with all four, current one lit.
        var camBtn = TextButton("Cam", safe.transform, "CAM · CHASE", new Color(Design.Ink700.r, Design.Ink700.g, Design.Ink700.b, 0.85f), paper, 28, display,
                                new Vector2(1, 1), new Vector2(-398, -28), new Vector2(250, 80));
        var picker = Rect("CamPicker", safe.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-398, -124), new Vector2(250, 4 * 78 + 12));
        picker.pivot = new Vector2(1, 1);
        var pickerBack = picker.gameObject.AddComponent<Image>();
        pickerBack.sprite = Design.Row; pickerBack.type = Image.Type.Sliced; pickerBack.color = new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.82f); pickerBack.raycastTarget = true;
        var optionPills = new Image[CameraAngles.Presets.Length];
        for (int i = 0; i < optionPills.Length; i++)
        {
            var opt = TextButton("Cam" + i, picker, CameraAngles.Presets[i].name, Design.Ink700, paper, 30, display,
                                 new Vector2(0.5f, 1), new Vector2(0, -6 - i * 78), new Vector2(226, 70));
            optionPills[i] = opt.GetComponent<Image>();
            if (angles != null) UnityEditor.Events.UnityEventTools.AddIntPersistentListener(opt.onClick, angles.Set, i);
        }
        picker.gameObject.SetActive(false);
        if (angles != null)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(camBtn.onClick, angles.Toggle);
            new SerializedObject(angles).Do(a =>
            {
                a.FindProperty("pillLabel").objectReferenceValue = camBtn.GetComponentInChildren<TMP_Text>();
                a.FindProperty("picker").objectReferenceValue = picker.gameObject;
                Fill(a.FindProperty("optionPills"), optionPills.Cast<Object>().ToArray());
                a.FindProperty("accent").colorValue = Design.Accent;
                a.FindProperty("idle").colorValue = Design.Ink700;
            });
        }

        // The race bar across the top (Beach Buggy's): START to FINISH with every racer's face riding along it.
        var barRt = Rect("RaceBar", safe.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(880, 12));
        Design.AsRow(barRt, new Color(Design.Paper.r, Design.Paper.g, Design.Paper.b, 0.45f)).raycastTarget = false;   // a light line: it must read over the night road
        Text("Start", safe.transform, display, "START", 24, paper, TextAlignmentOptions.Right, new Vector2(0.5f, 1), new Vector2(-462, -46), new Vector2(140, 36));
        var flag = Rect("FinishFlag", safe.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(476, -42), new Vector2(44, 44));
        var flagImg = flag.gameObject.AddComponent<Image>(); flagImg.sprite = Design.Icon("flag"); flagImg.color = paper; flagImg.raycastTarget = false;
        var raceBar = barRt.gameObject.AddComponent<RaceProgressBar>();
        var barPortraits = Directory.Exists(PortraitSetup.Dir) ? Directory.GetFiles(PortraitSetup.Dir, "*.png").OrderBy(f => f).ToArray() : new string[0];
        new SerializedObject(raceBar).Do(b =>
        {
            b.FindProperty("bar").objectReferenceValue = barRt;
            b.FindProperty("roster").objectReferenceValue = Roster();
            b.FindProperty("dot").objectReferenceValue = Design.Pill;
            Fill(b.FindProperty("portraits"), barPortraits.Select(f => (Object)AssetDatabase.LoadAssetAtPath<Sprite>(f)).ToArray());
            var ids = b.FindProperty("portraitIds"); ids.arraySize = barPortraits.Length;
            for (int i = 0; i < barPortraits.Length; i++) ids.GetArrayElementAtIndex(i).stringValue = Path.GetFileNameWithoutExtension(barPortraits[i]);
        });

        // Minimap under the lap plate: the circuit line with everyone on it, you as your face.
        var mapPlate = Plate("MapPlate", safe.transform, new Vector2(1, 1), new Vector2(-32, -196), new Vector2(236, 236), deep);
        var mapRt = Rect("Map", mapPlate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        mapRt.offsetMin = new Vector2(8, 8); mapRt.offsetMax = new Vector2(-8, -8);
        var mapImg = mapRt.gameObject.AddComponent<Image>();
        mapImg.sprite = mapSprite; mapImg.preserveAspect = true; mapImg.raycastTarget = false;
        var mini = mapRt.gameObject.AddComponent<Minimap>();
        var portraitFiles = Directory.Exists(PortraitSetup.Dir) ? Directory.GetFiles(PortraitSetup.Dir, "*.png").OrderBy(f => f).ToArray() : new string[0];
        new SerializedObject(mini).Do(m =>
        {
            m.FindProperty("map").objectReferenceValue = mapRt;
            m.FindProperty("worldMin").vector2Value = mapMin;
            m.FindProperty("worldSize").floatValue = mapSize;
            m.FindProperty("roster").objectReferenceValue = Roster();
            m.FindProperty("dot").objectReferenceValue = Design.Pill;
            Fill(m.FindProperty("portraits"), portraitFiles.Select(f => (Object)AssetDatabase.LoadAssetAtPath<Sprite>(f)).ToArray());
            var ids = m.FindProperty("portraitIds"); ids.arraySize = portraitFiles.Length;
            for (int i = 0; i < portraitFiles.Length; i++) ids.GetArrayElementAtIndex(i).stringValue = Path.GetFileNameWithoutExtension(portraitFiles[i]);
        });

        Results(safe.transform, display, body, paper, sand);
        Pause(safe.transform, display, paper, pauseBtn);   // last: it must draw over the results card too
        return canvasGo;
    }

    /// Pause overlay: resume is the one accent, restart and quit are neutral.
    static void Pause(Transform parent, TMP_FontAsset display, Color paper, UnityEngine.UI.Button pauseBtn)
    {
        var slate = Design.Ink700;
        var dim = Rect("PauseOverlay", parent, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.74f);
        var card = Rect("Card", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 900));
        Design.AsCard(card, Design.Ink800);
        Text("Headline", card, display, "PAUSED", 82, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 100));
        // Beach Buggy's pause has Audio and Controls: the switches a player reaches for mid-race, the settings screen's own rows
        foreach (var (which, caption, hint, y) in new[] { (SettingToggle.Which.AutoGas, "AUTO GAS", "The kart drives itself", -150f),
                                                          (SettingToggle.Which.Sound, "SOUND", "Everything you hear", -262f),
                                                          (SettingToggle.Which.Music, "MUSIC", "The soundtrack only", -374f) })
        {
            MenuSetup.SettingRow(card, which, caption, hint, y);
            var row = (RectTransform)card.Find(caption);
            row.sizeDelta = new Vector2(820, 100);
        }

        var menu = new GameObject("PauseMenu").AddComponent<PauseMenu>();
        var resume = TextButton("Resume", card, "RESUME", Design.Accent, paper, 48, display, new Vector2(0.5f, 0), new Vector2(0, 244), new Vector2(580, 124));
        var restart = TextButton("Restart", card, "RESTART", slate, paper, 38, display, new Vector2(0.5f, 0), new Vector2(-152, 80), new Vector2(276, 112));
        var quit = TextButton("Quit", card, "QUIT", slate, paper, 38, display, new Vector2(0.5f, 0), new Vector2(152, 80), new Vector2(276, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(resume.onClick, menu.Resume);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(restart.onClick, menu.Restart);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(pauseBtn.onClick, menu.Toggle);

        var so = new SerializedObject(menu);
        so.FindProperty("overlay").objectReferenceValue = dim.gameObject;
        so.FindProperty("restartButton").objectReferenceValue = restart.gameObject;   // hidden online: a room has no restart
        var hide = so.FindProperty("hideWhilePaused");
        hide.arraySize = 1; hide.GetArrayElementAtIndex(0).objectReferenceValue = pauseBtn.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        dim.gameObject.SetActive(false);
    }

    static RectTransform Plate(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 dims, Color colour)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        var img = Design.AsRow(rt, colour);
        img.raycastTarget = false;
        return rt;
    }

    static UnityEngine.UI.Button IconButton(string name, Transform parent, string icon, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        Design.AsButton(rt, new Color(Design.Ink700.r, Design.Ink700.g, Design.Ink700.b, 0.9f), out float lip);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var face = Rect(name + "Face", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        face.offsetMin = new Vector2(0, lip); face.offsetMax = Vector2.zero;
        feel.Configure(Design.PillDown, face, lip * (Design.Lip - 4f) / Design.Lip);
        var glyph = Rect(name + "Icon", face, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, dims * 0.42f);
        var g = glyph.gameObject.AddComponent<Image>();
        g.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Icons/{icon}.png");
        g.color = Design.Paper; g.raycastTarget = false;
        return button;
    }

    /// End-of-race card: order, times, and the two things you can do next.
    static void Results(Transform parent, TMP_FontAsset display, TMP_FontAsset body, Color paper, Color sand)
    {
        var flow = new GameObject("ScreenFlow").AddComponent<ScreenFlow>();
        var screen = Rect("Results", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // The dim and the card switch on together; leaving the dim on the always-active root greyed out the race.
        // Overhang the safe area (see MenuSetup): a dim that stops at the cutout leaves a bright live strip of track.
        var dim = Rect("Dim", screen, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.74f);

        var card = Rect("Card", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1220, 840));
        Design.AsCard(card, Design.Ink800);

        var headline = Text("Headline", card, display, "FINISH", 104, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(1000, 124));
        var table = Text("Table", card, body, "", 44, paper, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(940, 420));
        var status = Text("Status", card, body, "", 36, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 228), new Vector2(900, 60));

        var again = TextButton("Again", card, "NEXT RACE", Design.Accent, paper, 46, display, new Vector2(0.5f, 0), new Vector2(276, 80), new Vector2(500, 124));
        var menu = TextButton("Menu", card, "QUIT CUP", Design.Ink700, paper, 46, display, new Vector2(0.5f, 0), new Vector2(-276, 80), new Vector2(500, 124));

        ResultsRewards(card, display, body);
        var results = screen.gameObject.AddComponent<ResultsPanel>();
        // Both labels are rewritten at runtime: mid-cup they read NEXT RACE / QUIT CUP, after the last race NEW CUP / MENU.
        UnityEditor.Events.UnityEventTools.AddPersistentListener(again.onClick, results.Primary);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(menu.onClick, results.Secondary);

        var so = new SerializedObject(results);
        so.FindProperty("panel").objectReferenceValue = dim.gameObject;
        so.FindProperty("headline").objectReferenceValue = headline;
        so.FindProperty("table").objectReferenceValue = table;
        so.FindProperty("status").objectReferenceValue = status;
        so.FindProperty("primaryLabel").objectReferenceValue = again.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryLabel").objectReferenceValue = menu.GetComponentInChildren<TMP_Text>();
        so.FindProperty("roster").objectReferenceValue = Roster();
        so.ApplyModifiedPropertiesWithoutUndo();
        screen.gameObject.SetActive(true);
        dim.gameObject.SetActive(false);
    }

    /// A race control: the menus' pressable pill (face over lip, the face dropping as a thumb holds it), with a word
    /// or a glyph. `icon` is a Design glyph name (Design.Drawn) or a Kit sprite.
    static UnityEngine.UI.Image Button(string name, Transform parent, InputManager input, HoldButton.Control control, Color tint, Vector2 anchor, Vector2 offset, Vector2 dims, string caption, TMP_FontAsset font, SteerButtons steering = null, string icon = null)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        var img = Design.AsButton(rt, new Color(tint.r, tint.g, tint.b, 0.9f), out float lip);
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var hold = rt.gameObject.AddComponent<HoldButton>();
        var so = new SerializedObject(hold);
        so.FindProperty("control").enumValueIndex = (int)control;
        so.FindProperty("input").objectReferenceValue = input;
        so.FindProperty("fade").objectReferenceValue = group;
        so.FindProperty("steering").objectReferenceValue = steering;
        so.ApplyModifiedPropertiesWithoutUndo();
        var face = Rect(name + "Face", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        face.offsetMin = new Vector2(0, lip); face.offsetMax = Vector2.zero;
        feel.Configure(Design.PillDown, face, lip * (Design.Lip - 4f) / Design.Lip);

        if (icon != null)
        {
            var glyph = Rect(name + "Icon", face, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 104));
            var img2 = glyph.gameObject.AddComponent<Image>();
            img2.sprite = System.Array.IndexOf(Design.Drawn, icon) >= 0 ? Design.Icon(icon) : AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Kit/{icon}.png");
            img2.color = Design.Paper; img2.preserveAspect = true;
            img2.raycastTarget = false;
            return img;
        }

        var label = Text(name + "Text", face, font, caption, 44, tint == Design.Accent ? Design.OnAccent : Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = new Vector2(0, -2);
        return img;
    }

    static UnityEngine.UI.Button TextButton(string name, Transform parent, string caption, Color tint, Color textColour, float size, TMP_FontAsset font, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        Design.AsButton(rt, tint, out float lip);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var face = Rect(name + "Face", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        face.offsetMin = new Vector2(0, lip); face.offsetMax = Vector2.zero;
        var label = Text(name + "Label", face, font, caption, size, tint == Design.Accent ? Design.OnAccent : textColour, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16, 0); label.rectTransform.offsetMax = new Vector2(-16, -2);
        label.enableAutoSizing = true; label.fontSizeMin = size * 0.6f; label.fontSizeMax = size;
        feel.Configure(Design.PillDown, face, lip * (Design.Lip - 4f) / Design.Lip);
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

    /// Powerslide's own item box ships a built-in-pipeline material (pink under URP). ItemGiver shows and hides its
    /// own renderer as the box is taken and respawns, so the Kenney box has to BE that renderer: the prefab's mesh
    /// and material are swapped in place (a box hidden under a child came back at runtime and never blinked out).
    static void Reskin(GameObject go, string fbxPath, Material mat, float scale)
    {
        var art = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var mesh = art != null ? art.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;
        var filter = go.GetComponent<MeshFilter>();
        var renderer = go.GetComponent<MeshRenderer>();
        if (mesh == null || filter == null || renderer == null)
        {
            Debug.LogWarning("[track] item box: no mesh to swap in, hiding it");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            return;
        }
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = mat;
        go.transform.localScale = Vector3.one * scale;
    }

    /// What a prop does when a kart reaches it.
    ///   None   flat ground decals, distant scenery and marker flags — nothing a kart should ever feel
    ///   Solid  rocks, crates, wreck, tower: a static box you bounce off
    ///   Trunk  palms: a slim capsule around the trunk, so you hit the tree and not its canopy
    ///   Loose  cones: a light rigidbody that scatters when clipped
    enum Solid { None, Solid, Trunk, Loose }

    static readonly HashSet<string> missing = new();

    static GameObject Place(Transform parent, string fbxPath, Material mat, Vector3 pos, float yaw, float scale,
                            Solid solid = Solid.None, float tilt = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null)
        {
            if (missing.Add(fbxPath)) Debug.LogWarning("[track] missing " + fbxPath);   // once per prop, not once per placement
            return new GameObject("missing") { transform = { parent = parent } };
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt * 0.6f, yaw, tilt));
        go.transform.localScale = Vector3.one * scale;
        // Nature and Racing carry their own per-material colours: a null mat leaves them alone.
        if (mat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        if (solid != Solid.None) Collide(go, solid);
        Count(go);
        // A loose prop has to move, so it cannot be batched into the static geometry.
        if (solid != Solid.Loose) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    /// Colliders come off each mesh's own local bounds, so they fit the art at any scale or rotation.
    static void Collide(GameObject go, Solid solid)
    {
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var b = mesh.bounds;

            if (solid == Solid.Trunk)
            {
                // Palms are mostly canopy. A box would make a 6 m wall out of leaves, so wrap the trunk only.
                var cap = mf.gameObject.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.center = new Vector3(b.center.x, b.center.y, b.center.z);
                cap.height = b.size.y;
                cap.radius = Mathf.Max(0.12f, Mathf.Min(b.size.x, b.size.z) * 0.16f);
                continue;
            }

            var box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = new Vector3(b.size.x * 0.85f, b.size.y, b.size.z * 0.85f);   // slightly inside the art
        }

        if (solid != Solid.Loose) return;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 2.5f;
        rb.linearDamping = 0.6f; rb.angularDamping = 0.8f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.sleepThreshold = 0.05f;                      // sits asleep until something actually hits it
    }

    /// Slide a solid prop outward until its own bulk clears the kerb, so a collider never eats the racing line.
    static void PushClear(GameObject go, Vector3 trackPoint, Vector3 rightVec, float side, float clearance)
    {
        var b = Bounds(go);
        float radius = Mathf.Max(b.extents.x, b.extents.z);
        float outward = Vector3.Dot(go.transform.position - trackPoint, rightVec) * side;
        float need = clearance + radius;
        if (outward < need) go.transform.position += rightVec * (side * (need - outward));
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Art + $"Textures/{name}.png");

    /// The road's own material per map: the asphalt tile (Poly Haven, CC0) on the PBR road shader, wet after dark.
    static Material RoadMaterial()
    {
        var shader = Shader.Find("KartCup/Wet Road");
        if (shader == null)
        {
            Debug.LogWarning("[track] KartCup/Wet Road shader missing: road on Simple Lit");
            return LoadOrCreate(Art + "Materials/Road.mat", Shader.Find("Universal Render Pipeline/Simple Lit"), new Color(0.26f, 0.26f, 0.29f));
        }
        Directory.CreateDirectory(MeshDir);
        var m = LoadOrCreate($"{MeshDir}/Road_{V.id}.mat", shader, Color.white);
        m.shader = shader;
        m.SetTexture("_BaseMap", Tex("Surfaces/asphalt_diff")); m.SetTexture("_BumpMap", Tex("Surfaces/asphalt_nor"));
        m.SetTexture("_RoughMap", Tex("Surfaces/asphalt_rough")); m.SetTexture("_PuddleMap", Tex("Surfaces/puddles"));
        m.SetFloat("_Tile", 3f); m.SetFloat("_PuddleTile", 38f); m.SetFloat("_BumpScale", V.night ? 0.7f : 1f);
        m.SetFloat("_Wet", V.wet); m.SetFloat("_Puddles", 0.65f);
        m.SetFloat("_LaneOffset", HalfWidth * 0.5f); m.SetFloat("_LaneHalf", 3.4f);
        m.SetColor("_Tint", V.roadTint > 0f ? new Color(V.roadTint, V.roadTint, V.roadTint * 1.01f) : V.night ? new Color(1.7f, 1.68f, 1.7f) : new Color(0.75f, 0.75f, 0.78f));   // the tile is dark; night lifts it for the lamps
        EditorUtility.SetDirty(m);
        return m;
    }

    /// The shared day sea, or the theme's own copy of it (the night sea is darker, with the moon's glitter on it).
    static Material WaterMaterial()
    {
        string path = Art + $"Materials/{V.water}.mat";
        if (V.water != "Water" && AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CopyAsset(Art + "Materials/Water.mat", path);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null && V.night)
        {
            m.SetColor("_DeepColor", new Color(0.02f, 0.05f, 0.10f)); m.SetColor("_ShallowColor", new Color(0.05f, 0.13f, 0.20f));
            m.SetColor("_SurfColor", new Color(0.45f, 0.55f, 0.62f));
            float yaw = (V.sun.y + 180f) * Mathf.Deg2Rad;             // the moon is where the light comes from
            m.SetVector("_MoonDir", new Vector4(Mathf.Sin(yaw), Mathf.Cos(yaw), 0, 0));
            m.SetColor("_Glitter", new Color(1f, 0.85f, 0.6f)); m.SetFloat("_GlitterStrength", 1.6f); m.SetFloat("_GlitterWidth", 0.12f);
            EditorUtility.SetDirty(m);
        }
        return m;
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
