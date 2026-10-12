using System.IO;
using System.Linq;
using KartCup;
using PowerslideKartPhysics;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds Menu.unity: title, character select, track select and leaderboard, over a live 3D vista with the kart you
// will race.
// Run: Unity -batchmode -projectPath . -executeMethod MenuSetup.Build -quit   (menu Build > Build Menu Scene)
public static partial class MenuSetup
{
    const string ScenePath = "Assets/_Game/Scenes/Menu.unity";
    const string Art = "Assets/_Game/Art/";
    const string Prefabs = "Assets/_Game/Prefabs/";
    const string FontDir = "Assets/_Game/UI/Fonts/";

    // Every colour and typeface comes from Design, so the one-accent and shape-lock rules hold across both scenes.
    static readonly Color Deep = Design.Ink900;
    static readonly Color Panel = Design.Ink800;
    static readonly Color Paper = Design.Paper;
    static readonly Color Muted = Design.Muted;
    static readonly Color Accent = Design.Accent;
    static readonly Color Slate = Design.Ink700;

    public static TMP_FontAsset display, body, strong;

    [MenuItem("Build/Build Menu Scene")]
    public static void Build()
    {
        Fonts();
        VoiceSetup.Build();                                   // the drivers' voices (Resources/DriverVoices), for the select screen and the race
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var podium = Vista();

        var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400, 1080);
        scaler.matchWidthOrHeight = 1f;                       // phones are short: fit height

        var safe = Rect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        safe.gameObject.AddComponent<SafeArea>();

        var flow = canvasGo.AddComponent<ScreenFlow>();
        var carousel = podium.gameObject.AddComponent<CharacterCarousel>();
        var title = TitleScreen(safe);
        var select = SelectScreen(safe, podium, carousel);
        var board = BoardScreen(safe);
        var settings = SettingsScreen(safe);
        var track = TrackScreen(safe);
        var rooms = RoomsScreen(safe);
        var lobby = LobbyScreen(safe);
        var garage = GarageScreen(safe, podium, carousel);
        var items = ItemsScreen(safe, podium);
        var weeklyScreen = WeeklyScreen(safe);
        var tourScreen = TourScreen(safe);
        var achievementsScreen = AchievementsScreen(safe);
        var gemsScreen = GemsScreen(safe);
        var front = FrontScreen(safe);
        var connect = ConnectScreen(safe);
        FuelOverlay(safe);                                    // over every panel: an empty tank opens it
        // the title's hero: the chosen driver by the chosen kart on the street's home stage
        var home = title.AddComponent<HomeStage>();
        new SerializedObject(home).Do(s =>
        {
            s.FindProperty("carousel").objectReferenceValue = carousel;
            s.FindProperty("karts").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
            s.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
            Fill(s.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).Where(k => k != null).Cast<Object>().ToArray());
            const string kitAudio = "Assets/ThirdParty/PowerslideKartPhysics/Audio/";   // the drive-off's sound, when a kart's own KartAudio lacks one
            s.FindProperty("engineClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(kitAudio + "Engines/Engine1.wav");
            s.FindProperty("screechClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(kitAudio + "Kart/TireScreech.wav");
            s.FindProperty("launchClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(kitAudio + "Kart/BoostStart.wav");
        });

        var so = new SerializedObject(flow);
        so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        so.FindProperty("playLabel").objectReferenceValue = title.transform.Find("Plate/Actions/Quick/QuickFace/QuickLabel")?.GetComponent<TMP_Text>();   // the quick-race door says CONTINUE CUP mid-cup
        var panels = so.FindProperty("panels");
        panels.arraySize = 15;
        panels.GetArrayElementAtIndex(0).objectReferenceValue = title;
        panels.GetArrayElementAtIndex(1).objectReferenceValue = select;
        panels.GetArrayElementAtIndex(2).objectReferenceValue = board;
        panels.GetArrayElementAtIndex(3).objectReferenceValue = settings;
        panels.GetArrayElementAtIndex(4).objectReferenceValue = track;
        panels.GetArrayElementAtIndex(5).objectReferenceValue = rooms;
        panels.GetArrayElementAtIndex(6).objectReferenceValue = lobby;
        panels.GetArrayElementAtIndex(7).objectReferenceValue = garage;
        panels.GetArrayElementAtIndex(8).objectReferenceValue = items;
        panels.GetArrayElementAtIndex(9).objectReferenceValue = weeklyScreen;
        panels.GetArrayElementAtIndex(10).objectReferenceValue = tourScreen;
        panels.GetArrayElementAtIndex(11).objectReferenceValue = achievementsScreen;
        panels.GetArrayElementAtIndex(12).objectReferenceValue = gemsScreen;
        panels.GetArrayElementAtIndex(13).objectReferenceValue = front;
        panels.GetArrayElementAtIndex(14).objectReferenceValue = connect;   // shown once at first launch (ConnectPanel.Due)
        so.FindProperty("startPanel").intValue = 13;   // the front door first; PLAY opens the hub
        so.ApplyModifiedPropertiesWithoutUndo();

        Curtain();
        ChestOverlay();
        PurchaseOverlay();
        CarryPickerPrefab();   // a prefab the race scenes load, made here with the menu's kit
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var boot = new GameObject("Boot");
        boot.AddComponent<GameBoot>();
        AudioSetup.Build(menu: true);
        new GameObject("Web3Api").AddComponent<LeaderboardFeed>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));     // menu first: it is the entry point
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[menu] built " + ScenePath);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// The loading curtain (LoadingCurtain): its own canvas over everything, kept across scenes once the menu has run.
    static void Curtain()
    {
        var go = new GameObject("LoadingCurtain", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400, 1080);
        scaler.matchWidthOrHeight = 1f;
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;

        var back = Rect("Back", go.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        back.gameObject.AddComponent<Image>().color = Deep;
        var glow = Rect("Glow", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(2200, 1300));
        var glowImg = glow.gameObject.AddComponent<Image>();
        glowImg.sprite = Design.Glow; glowImg.color = new Color(Design.Ink600.r, Design.Ink600.g, Design.Ink600.b, 0.55f); glowImg.raycastTarget = false;
        var kicker = Label("Kicker", go.transform, strong, "", 32, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1800, 48));
        kicker.characterSpacing = 12f;
        var title = Label("Title", go.transform, display, "", 150, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(2000, 170));
        title.enableAutoSizing = true; title.fontSizeMin = 90; title.fontSizeMax = 150;
        var spin = Rect("Spinner", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(104, 104));
        var spinImg = spin.gameObject.AddComponent<Image>();
        spinImg.sprite = Design.Icon("arc"); spinImg.color = Paper; spinImg.raycastTarget = false;
        var tip = Label("Tip", go.transform, body, "", 32, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1900, 50));

        var curtain = go.AddComponent<LoadingCurtain>();
        new SerializedObject(curtain).Do(s =>
        {
            s.FindProperty("group").objectReferenceValue = group;
            s.FindProperty("kicker").objectReferenceValue = kicker;
            s.FindProperty("title").objectReferenceValue = title;
            s.FindProperty("tip").objectReferenceValue = tip;
            s.FindProperty("spinner").objectReferenceValue = spin;
        });
    }

    // ---------- fonts ----------
    public static void Fonts()
    {
        Design.Fonts();
        display = Design.Display; body = Design.Body; strong = Design.Strong;
    }

    public static TMP_FontAsset MakeFont(string ttf, int samplingSize)
    {
        Directory.CreateDirectory(FontDir);
        var path = FontDir + ttf + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(FontDir + ttf + ".ttf");
        if (source == null) { Debug.LogWarning("[menu] missing font " + ttf); return null; }
        var asset = TMP_FontAsset.CreateFontAsset(source, samplingSize, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        asset.name = ttf + " SDF";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // ---------- 3D backdrop ----------
    /// The coast after dark, the same night as the race: Palm Bay's sky cube, moon, sea and grade, reused from what
    /// TrackSetup built. Two sets on one beach: the street meet the title looks at (MenuMeet), toward the moon, and
    /// behind the camera the turntable the kart is chosen on, under a warm key.
    static readonly Vector3 MeetAt = new Vector3(8f, 0f, 40f);

    static Transform Vista()
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sky_beach.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sky.mat");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.20f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.15f, 0.22f);
        RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.07f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 40; RenderSettings.fogEndDistance = 260;
        RenderSettings.fogColor = new Color(0.22f, 0.27f, 0.33f);
        RenderSettings.reflectionIntensity = 0.2f;

        var sun = new GameObject("Sun").AddComponent<Light>();     // the moon, where the race's is
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(26, -150, 0);
        sun.shadows = LightShadows.Soft; sun.intensity = 0.7f;
        sun.color = new Color(0.62f, 0.72f, 1f); sun.shadowStrength = 0.6f;

        var sand = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sand.name = "Sand";
        sand.transform.localScale = new Vector3(14, 1, 14);
        sand.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sand.mat");

        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        water.transform.position = new Vector3(0, -0.6f, 0);
        water.transform.localScale = new Vector3(120, 1, 120);
        water.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water_night.mat") ?? AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water.mat");
        Object.DestroyImmediate(water.GetComponent<Collider>());

        var pirate = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Pirate.mat");
        var rnd = new System.Random(3);
        var palms = new[] { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
            float r = 26f + (float)rnd.NextDouble() * 40f;
            var at = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
            string palm = palms[rnd.Next(palms.Length)]; float yaw = (float)rnd.NextDouble() * 360f, size = 1.6f + (float)rnd.NextDouble() * 1.1f;
            if (!MenuMeet.Covers(MeetAt, at)) Place(Art + $"Models/Props/Pirate/{palm}.fbx", pirate, at, yaw, size);   // none on the road
        }
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f + 0.4f;
            var at = new Vector3(Mathf.Sin(a) * (34f + i * 2f), -0.3f, Mathf.Cos(a) * (34f + i * 2f));
            if (!MenuMeet.Covers(MeetAt, at)) Place(Art + "Models/Props/Pirate/rocks-sand-b.fbx", pirate, at, i * 37f, 2.2f);
        }
        var meet = MenuMeet.Build(MeetAt);

        // The kart stands on a turntable that turns with it (CharacterCarousel/GaragePanel rotate this transform). A kart
        // swivelling over bare sand read as floating; the disc gives the tyres something to stand on. Mount grounds the
        // tyres at the podium's height, so the podium sits on the disc's top face.
        var podium = new GameObject("Podium").transform;
        podium.position = new Vector3(0, 0.12f, 0);
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Turntable";
        disc.transform.SetParent(podium, false);
        disc.transform.localPosition = new Vector3(0, -0.06f, 0);
        disc.transform.localScale = new Vector3(6.8f, 0.06f, 6.8f);   // radius 3.4 m: the 4.9 m trucks stand inside the rim with margin
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        var discMat = ContentSetup.LoadOrCreate(Art + "Materials/Turntable.mat", Shader.Find("Universal Render Pipeline/Lit"));
        discMat.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.19f));   // graphite: neutral under any paint, no second accent
        discMat.SetFloat("_Smoothness", 0.38f);
        discMat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(discMat);
        disc.GetComponent<MeshRenderer>().sharedMaterial = discMat;

        // A plain dark rim (it used to glow cyan, like a neon sign under the kart): the platform is a stand, the kart is
        // the thing to look at. A warm key and a cool fill pick the kart out of the night, and the lighthouse turns
        // out past the palms.
        var rimMat = ContentSetup.LoadOrCreate(Art + "Materials/Rim_Dark.mat", Shader.Find("Universal Render Pipeline/Lit"));
        rimMat.SetColor("_BaseColor", new Color(0.07f, 0.08f, 0.09f)); rimMat.SetFloat("_Smoothness", 0.55f); rimMat.SetFloat("_Metallic", 0.6f);
        EditorUtility.SetDirty(rimMat);
        Ring(podium, "Rim", 3.42f, 3.6f, 0.115f, rimMat);
        Spot("KeyLight", new Vector3(6f, 7f, -4f), new Vector3(0, 1f, 0), new Color(1f, 0.82f, 0.58f), 48f, 55f);
        Spot("FillLight", new Vector3(-7f, 5f, 6f), new Vector3(0, 1f, 0), new Color(0.7f, 0.8f, 1f), 18f, 70f);
        Lighthouse(new Vector3(-55f, 0f, 120f));

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 30; cam.nearClipPlane = 0.2f; cam.farClipPlane = 300; cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        var drift = camGo.AddComponent<MenuBackdrop>();
        new SerializedObject(drift).Do(s =>
        {
            s.FindProperty("target").objectReferenceValue = podium;
            s.FindProperty("meet").objectReferenceValue = meet;
        });
        drift.Snap(false);     // parked on the title's pose, so the scene reads right when opened or screenshotted

        var volume = new GameObject("Post").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/Post_beach.asset")
                            ?? AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/PostProfile.asset");
        return podium;
    }

    static void Spot(string name, Vector3 at, Vector3 target, Color colour, float intensity, float angle)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.position = at; l.transform.LookAt(target);
        l.type = LightType.Spot; l.spotAngle = angle; l.innerSpotAngle = angle * 0.5f; l.range = 22f; l.intensity = intensity;
        l.color = colour; l.shadows = LightShadows.None;
    }

    /// A flat ring on the turntable, facing up.
    static void Ring(Transform parent, string name, float r0, float r1, float y, Material mat, int segs = 64)
    {
        var v = new Vector3[segs * 2]; var n = new Vector3[segs * 2]; var uv = new Vector2[segs * 2]; var t = new int[segs * 6];
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            v[i * 2] = d * r0 + Vector3.up * y; v[i * 2 + 1] = d * r1 + Vector3.up * y;
            n[i * 2] = n[i * 2 + 1] = Vector3.up; uv[i * 2] = uv[i * 2 + 1] = new Vector2(0.03f, 0.5f);
            int j = (i + 1) % segs, k = i * 6;
            t[k] = i * 2; t[k + 1] = j * 2 + 1; t[k + 2] = i * 2 + 1;
            t[k + 3] = i * 2; t[k + 4] = j * 2; t[k + 5] = j * 2 + 1;
        }
        var mesh = new Mesh { name = name, vertices = v, normals = n, uv = uv, triangles = t };
        mesh.RecalculateBounds();
        Directory.CreateDirectory(Art + "Models/Props/Menu");
        AssetDatabase.CreateAsset(mesh, Art + $"Models/Props/Menu/{name}.asset");
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    /// Palm Bay's lighthouse, as TrackNight built it (mesh, materials, beams), placed out to sea and turning.
    static void Lighthouse(Vector3 at)
    {
        const string dir = Art + "Models/Track/Race1/";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(dir + "Lighthouse.asset");
        var core = AssetDatabase.LoadAssetAtPath<Mesh>(dir + "Beam_Core.asset");
        var halo = AssetDatabase.LoadAssetAtPath<Mesh>(dir + "Beam_Halo.asset");
        if (mesh == null || core == null || halo == null) { Debug.LogWarning("[menu] no lighthouse: build the race scenes first"); return; }
        Material Mat(string m) => AssetDatabase.LoadAssetAtPath<Material>(Art + $"Materials/{m}.mat");
        var root = new GameObject("Lighthouse").transform;
        root.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(-at.x, 0, -at.z).normalized, Vector3.up));   // its door faces the beach
        var tower = new GameObject("Tower");
        tower.transform.SetParent(root, false);
        tower.AddComponent<MeshFilter>().sharedMesh = mesh;
        tower.AddComponent<MeshRenderer>().sharedMaterials = new[] { "LH_Stone", "LH_Red", "LH_White", "LH_Trim", "LH_Glass", "LH_Door" }.Select(Mat).ToArray();
        const float lampY = 22.05f;                        // TrackNight: the gallery deck + 1.55
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(lamp.GetComponent<Collider>());
        lamp.name = "Lamp"; lamp.transform.SetParent(root, false);
        lamp.transform.localPosition = new Vector3(0, lampY, 0); lamp.transform.localScale = Vector3.one * 0.84f;
        lamp.GetComponent<MeshRenderer>().sharedMaterial = Mat("LH_Lamp");
        var rotor = new GameObject("Rotor").transform;
        rotor.SetParent(root, false);
        rotor.localPosition = new Vector3(0, lampY, 0);
        rotor.gameObject.AddComponent<KartCup.Rotor>().period = 8f;
        foreach (float yaw in new[] { 0f, 180f })
            foreach (var (name, m, mat) in new[] { ("BeamCore", core, Mat("Beam_Core")), ("BeamHalo", halo, Mat("Beam_Halo")) })
            {
                var go = new GameObject(name);
                go.transform.SetParent(rotor, false);
                go.transform.localRotation = Quaternion.Euler(1.5f, yaw, 0);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
    }

    /// An alpha ramp, generated once and committed like any other sprite: left-to-right for the title plate,
    /// bottom-to-top for the track select, where the card and text have to read over a full-screen picture.
    static Sprite FadeSprite(bool vertical = false)
    {
        string path = Art + (vertical ? "Textures/UI/plate_fade_v.png" : "Textures/UI/plate_fade.png");
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        int w = vertical ? 8 : 256, h = vertical ? 256 : 8;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                float t = vertical ? y / (h - 1f) : x / (w - 1f);
                float a = 0.88f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, t)));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- screens ----------
    /// Beach Buggy's front: the lockup big over the street with everyone on it, and one row of doors with PLAY at the
    /// right. PLAY is the hub (TitleScreen: your driver and kart). Android back from the hub comes here; from here it quits.
    static GameObject FrontScreen(RectTransform parent)
    {
        var screen = Rect("Front", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var plate = Rect("Plate", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var shade = Rect("Shade", plate, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 150), new Vector2(0, 420));
        var img = shade.gameObject.AddComponent<Image>(); img.sprite = FadeSprite(vertical: true); img.color = Deep; img.raycastTarget = false;
        var lockup = UiSprite(Art + "Textures/UI/kartel_lockup.png");
        var logo = Rect("Logo", plate, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1400, 512));
        logo.pivot = new Vector2(0.5f, 1);
        var mark = logo.gameObject.AddComponent<Image>(); mark.sprite = lockup; mark.preserveAspect = true; mark.raycastTarget = false;
        var actions = Rect("Actions", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        actions.gameObject.AddComponent<PopIn>();
        var doors = new (string name, string icon, string caption, string method)[]
        {
            ("Settings", "icon_cog", "SETTINGS", nameof(ScreenFlow.ShowSettings)), ("Board", "trophy", "RANKING", nameof(ScreenFlow.ShowLeaderboard)),
            ("Awards", "star", "AWARDS", nameof(ScreenFlow.ShowAchievements)), ("Online", "globe", "ONLINE", nameof(ScreenFlow.ShowRooms)),
        };
        for (int i = 0; i < doors.Length; i++)
        {
            var door = Tile(doors[i].name, actions, doors[i].icon, doors[i].caption, new Vector2(0, 0), new Vector2(64 + i * 262, 48), new Vector2(236, 212), glyph: 96f);
            Wire(door, doors[i].method);
            if (doors[i].name == "Awards") AwardsBadge(door.transform);   // how many wait to be claimed
        }
        var play = Button("Play", actions, "PLAY", Accent, Paper, 76, new Vector2(1, 0), new Vector2(-64, 48), new Vector2(640, 212), icon: "play");
        Wire(play, nameof(ScreenFlow.ShowTitle));
        Shine(play);
        return screen;
    }

    /// First launch (10 Oct 2026): connect the Seeker wallet, or play as a guest. The street behind, a dark wash over it,
    /// the lockup, one line on why, three perks that are true today, a big CONNECT and a small guest link.
    static GameObject ConnectScreen(RectTransform parent)
    {
        var screen = Rect("Connect", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var panel = screen.AddComponent<ConnectPanel>();
        var wash = Rect("Wash", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        wash.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.78f);
        var logo = Rect("Logo", screen.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(760, 280));
        logo.pivot = new Vector2(0.5f, 1);
        var mark = logo.gameObject.AddComponent<Image>(); mark.sprite = UiSprite(Art + "Textures/UI/kartel_lockup.png"); mark.preserveAspect = true; mark.raycastTarget = false;
        Label("Title", screen.transform, display, "CONNECT YOUR SEEKER WALLET", 84, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(2000, 100));
        Label("Why", screen.transform, body, "One tap and a fingerprint: Seed Vault is built into your Seeker.", 36, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -436), new Vector2(1800, 50));
        var perks = new (string icon, string text)[]
        {
            ("star", $"+{ConnectPanel.StarterGems} STARTER GEMS"), ("user", "YOUR .SKR NAME ON THE GRID"), ("kart", "SEEKER EDITION KART FOR GENESIS HOLDERS"),
        };
        for (int i = 0; i < perks.Length; i++)
        {
            var row = Rect("Perk" + i, screen.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 640, -540), new Vector2(600, 110));
            row.pivot = new Vector2(0.5f, 1);
            Design.AsRow(row, Design.Ink700).raycastTarget = false;
            var ic = Rect("Icon", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(64, 64));
            var ii = ic.gameObject.AddComponent<Image>(); ii.sprite = IconSprite(perks[i].icon); ii.color = Accent; ii.preserveAspect = true; ii.raycastTarget = false;
            var t = Label("Text", row, strong, perks[i].text, 30, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(116, 0), new Vector2(460, 90));
            t.enableAutoSizing = true; t.fontSizeMin = 20; t.fontSizeMax = 30; t.textWrappingMode = TextWrappingModes.Normal;
        }
        var connect = Button("Connect", screen.transform, "CONNECT", Accent, Paper, 72, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(760, 170), icon: "user");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(connect.onClick, panel.Connect);
        Shine(connect);
        var guest = Label("Guest", screen.transform, strong, "Play as guest", 40, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(600, 80));
        guest.raycastTarget = true;
        var gb = guest.gameObject.AddComponent<Button>(); gb.transition = Selectable.Transition.None;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(gb.onClick, panel.Guest);
        var status = Label("Status", screen.transform, body, "", 30, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1800, 44));
        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("status").objectReferenceValue = status;
            s.FindProperty("connectLabel").objectReferenceValue = connect.GetComponentInChildren<TMP_Text>();
        });
        return screen;
    }

    static GameObject TitleScreen(RectTransform parent)
    {
        var screen = Rect("Title", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        // Beach Buggy's hub (10 Oct 2026, the user's reference): the chosen driver and kart dead centre on the street
        // (HomeStage), the counters in a bar along the top, one row of big labelled doors along the bottom with PLAY at
        // the right. No plate: nothing boxes the hero in. "Plate" stays as the full-screen container the rewards and
        // the flow look things up in, and "Actions" as the row PopIn assembles.
        var plate = Rect("Plate", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        // a soft shade under the doors and under the counters, so both read on any street (the ramp is dark at the bottom)
        foreach (var (name, top) in new[] { ("ShadeBottom", false), ("ShadeTop", true) })
        {
            var shade = Rect(name, plate, new Vector2(0, top ? 1 : 0), new Vector2(1, top ? 1 : 0), new Vector2(0, top ? -130 : 150), new Vector2(0, 400));
            if (top) shade.localScale = new Vector3(1, -1, 1);
            var img = shade.gameObject.AddComponent<Image>(); img.sprite = FadeSprite(vertical: true); img.color = Deep; img.raycastTarget = false;
        }

        // the lockup, small, where Beach Buggy keeps BACK: the brand, not a headline
        var lockup = UiSprite(Art + "Textures/UI/kartel_lockup.png");
        if (lockup == null) Debug.LogWarning("[menu] no kartel_lockup.png: run tools/brand/make_lockup.py");
        // BACK to the front door first (13 Oct 2026, the user: "there's no back button in the dashboard screen to come to
        // the welcome screen"; only Android's back did it), the lockup beside it, a little smaller
        var home = IconButton("Back", plate, "back", new Vector2(0, 1), new Vector2(40, -28), new Vector2(108, 108));
        Wire(home, nameof(ScreenFlow.ShowFront));
        var logo = Rect("Logo", plate, new Vector2(0, 1), new Vector2(0, 1), new Vector2(168, -30), new Vector2(270, 99));
        logo.pivot = new Vector2(0, 1);
        var mark = logo.gameObject.AddComponent<Image>();
        mark.sprite = lockup; mark.preserveAspect = true; mark.raycastTarget = false;

        // the doors along the bottom, in the order you reach for them, and PLAY big at the right (Beach Buggy's Choose Series)
        var actions = Rect("Actions", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        actions.gameObject.AddComponent<PopIn>();
        var doors = new (string name, string icon, string caption, string method)[]
        {
            ("Driver", "user", "DRIVER", nameof(ScreenFlow.ShowSelect)), ("Garage", "kart", "KART", nameof(ScreenFlow.ShowGarage)),
            ("Items", "rocket", "ITEMS", nameof(ScreenFlow.ShowItems)), ("Online", "globe", "ONLINE", nameof(ScreenFlow.ShowRooms)),
            ("Quick", "play", "QUICK RACE", nameof(ScreenFlow.Play)),   // one track, or the cup in flight (the label says CONTINUE CUP then)
        };
        for (int i = 0; i < doors.Length; i++)
        {
            var door = Tile(doors[i].name, actions, doors[i].icon, doors[i].caption, new Vector2(0, 0), new Vector2(64 + i * 262, 48), new Vector2(236, 212), glyph: 96f);
            Wire(door, doors[i].method);
            if (doors[i].name == "Awards") AwardsBadge(door.transform);   // how many wait to be claimed
        }
        // the DRIVER and KART doors wear what is chosen, a swap badge in the corner (Beach Buggy's Switch Driver / Switch Car)
        Image Picture(string door)
        {
            var icon = actions.Find($"{door}/{door}Face/{door}Icon")?.GetComponent<Image>();
            if (icon == null) return null;
            icon.rectTransform.sizeDelta = new Vector2(118, 118); icon.rectTransform.anchoredPosition = new Vector2(0, -14);   // clear of the label under it
            var badge = Rect("Swap", icon.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(6, -6), new Vector2(54, 54));   // the top corner: at the bottom it crowded the label
            Design.AsPill(badge, Deep).raycastTarget = false;
            var glyph = Rect("Glyph", badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            var gi = glyph.gameObject.AddComponent<Image>(); gi.sprite = IconSprite("swap"); gi.color = Accent; gi.preserveAspect = true; gi.raycastTarget = false;
            return icon;
        }
        var rosterDb = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        var driverIds = rosterDb != null ? rosterDb.characters.Where(c => c != null).Select(c => c.driverId).ToArray() : new string[0];
        var homeDoors = screen.AddComponent<HomeDoors>();
        new SerializedObject(homeDoors).Do(s =>
        {
            s.FindProperty("roster").objectReferenceValue = rosterDb;
            s.FindProperty("karts").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
            s.FindProperty("driverImage").objectReferenceValue = Picture("Driver");
            s.FindProperty("kartImage").objectReferenceValue = Picture("Garage");
            s.FindProperty("driverFallback").objectReferenceValue = IconSprite("user");
            s.FindProperty("kartFallback").objectReferenceValue = IconSprite("kart");
            var ids = s.FindProperty("portraitIds"); ids.arraySize = driverIds.Length;
            for (int i = 0; i < driverIds.Length; i++) ids.GetArrayElementAtIndex(i).stringValue = driverIds[i];
            Fill(s.FindProperty("portraits"), driverIds.Select(id => AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSetup.Dir + id + ".png")).Cast<Object>().ToArray());
            var kids = s.FindProperty("kartIds"); kids.arraySize = KartSetup.Karts.Length;
            for (int i = 0; i < KartSetup.Karts.Length; i++) kids.GetArrayElementAtIndex(i).stringValue = KartSetup.Karts[i];
            Fill(s.FindProperty("thumbs"), KartSetup.Karts.Select(id => AssetDatabase.LoadAssetAtPath<Sprite>(KartThumbs.Dir + id + ".png")).Cast<Object>().ToArray());
        });
        homeDoors.ShowInEditor();
        // Beach Buggy's Choose Series: the career is the main road. Series after series, each cleared by its stars.
        var play = Button("Play", actions, "CHOOSE SERIES", Accent, Paper, 60, new Vector2(1, 0), new Vector2(-64, 48), new Vector2(640, 212), icon: "flag");
        Wire(play, nameof(ScreenFlow.ChooseSeries));
        Shine(play);

        // Top right: who you are (sign in), the gems, the two counters (ranking, awards) and the settings.
        var corner = Rect("Corner", screen.transform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        SceneSetup.Web3Object();   // without it SIGN IN, the shop and paid rooms all hit a null Web3.Instance
        var wallet = new GameObject("WalletFlow").AddComponent<WalletFlow>();
        var chipBtn = Button("Profile", corner, "SIGN IN", new Color(Slate.r, Slate.g, Slate.b, 0.92f), Paper, 34,
                             new Vector2(1, 1), new Vector2(-196, -32), new Vector2(380, 104), icon: "user");
        var chip = chipBtn.gameObject.AddComponent<ProfileChip>();
        var toast = Label("Toast", corner, body, "", 28, Paper, TextAlignmentOptions.Right,
                          new Vector2(1, 1), new Vector2(-64, -270), new Vector2(900, 44));   // under the fuel chip
        var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false;
        new SerializedObject(chip).Do(s =>
        {
            s.FindProperty("nameLabel").objectReferenceValue = chipBtn.GetComponentInChildren<TMP_Text>();
            s.FindProperty("toast").objectReferenceValue = toast;
            s.FindProperty("toastGroup").objectReferenceValue = toastGroup;
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(chipBtn.onClick, chip.Tap);
        var cog = IconButton("Settings", corner, "icon_cog", new Vector2(1, 1), new Vector2(-64, -32), new Vector2(104, 104));
        Wire(cog, nameof(ScreenFlow.ShowSettings));
        GemChip(corner, new Vector2(-600, -32));
        FuelChip(corner, new Vector2(1, 1), new Vector2(-600, -146), new Vector2(260, 104));   // under the gems: a race takes a can
        var boardBtn = IconButton("Board", corner, "trophy", new Vector2(1, 1), new Vector2(-880, -32), new Vector2(104, 104));
        Wire(boardBtn, nameof(ScreenFlow.ShowLeaderboard));
        var awardsBtn = IconButton("Awards", corner, "star", new Vector2(1, 1), new Vector2(-1000, -32), new Vector2(104, 104));
        Wire(awardsBtn, nameof(ScreenFlow.ShowAchievements));
        AwardsBadge(awardsBtn.transform);

        // NEW VERSION, right of the hero: hidden until the download host has a newer build (AppUpdate)
        var update = Button("Update", screen.transform, "NEW VERSION", Slate, Paper, 32,
                            new Vector2(1, 0), new Vector2(-64, 360), new Vector2(600, 104), icon: "download");
        var appUpdate = screen.AddComponent<AppUpdate>();
        new SerializedObject(appUpdate).Do(s =>
        {
            s.FindProperty("banner").objectReferenceValue = update.gameObject;
            s.FindProperty("label").objectReferenceValue = update.GetComponentInChildren<TMP_Text>();
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(update.onClick, appUpdate.Open);
        update.gameObject.SetActive(false);

        TitleRewards(screen, plate);
        return screen;
    }

    static GameObject SelectScreen(RectTransform parent, Transform podium, CharacterCarousel carousel)
    {
        var screen = Rect("Select", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        // First child so every control draws (and raycasts) above it: a drag on the open vista turns/flicks the kart.
        var catcher = Rect("SwipeCatcher", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        catcher.gameObject.AddComponent<Image>().color = Color.clear;
        var swipe = catcher.gameObject.AddComponent<SwipeSelect>();
        new SerializedObject(swipe).Do(s => { s.FindProperty("carousel").objectReferenceValue = carousel; s.FindProperty("flick").boolValue = false; });

        Header(screen.transform, "CHOOSE YOUR DRIVER", nameof(ScreenFlow.ShowTitle), "STEP 1 OF 2");
        var gemCorner = Rect("GemCorner", screen.transform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        GemChip(gemCorner, new Vector2(-64, -150));   // under STEP 1 OF 2: recruiting is paid in gems

        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 64); card.offsetMax = new Vector2(744, -172);
        Design.AsCard(card, Panel);

        var nameLabel = Label("Name", card, display, "NODEY", 112, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -40), new Vector2(624, 120));
        var blurb = Label("Blurb", card, body, "", 34, Muted, TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(48, -164), new Vector2(608, 96));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        var bars = new StatBar[6];
        var labels = new[] { "SPEED", "ACCEL", "GRIP", "WEIGHT", "TURBO", "ATTACK" };
        for (int i = 0; i < 6; i++) bars[i] = StatRow(card, labels[i], -276 - i * 56);

        var next = Button("Next", card, "CHOOSE TRACK", Accent, Paper, 46,
                          new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(584, 112), icon: "next", iconAfter: true);   // 112 high: the pay row fits above it, under ATTACK
        Wire(next, nameof(ScreenFlow.ShowTrack));
        Shine(next);
        // a locked judge: PAY in SOL / PAY in ORE above the big button (which reads UNLOCK · SKR); both hidden otherwise
        var payB = Button("Secondary", card, "PAY SOL", Slate, Paper, 32, new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(584, 72));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(payB.onClick, carousel.Secondary);
        var payC = Button("Tertiary", card, "PAY ORE", Slate, Paper, 32, new Vector2(0.5f, 0), new Vector2(150, 160), new Vector2(284, 72));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(payC.onClick, carousel.Tertiary);
        payB.gameObject.SetActive(false); payC.gameObject.SetActive(false);

        // Every driver's face in a row along the bottom right, clear of the card: tap one to put them on the turntable.
        var roster = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        var strip = Rect("Roster", screen.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 48), Vector2.zero);
        var rosterStrip = strip.gameObject.AddComponent<RosterStrip>();
        int count = roster != null ? roster.characters.Length : 0;
        const float tileSize = 116f, tileGap = 12f;
        var tiles = new RectTransform[count]; var tileBacks = new Image[count]; var rings = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var def = roster.characters[i];
            var rt = Rect("Face" + i, strip, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-(count - 1 - i) * (tileSize + tileGap), 0), new Vector2(tileSize, tileSize));
            rt.pivot = new Vector2(1, 0);
            tileBacks[i] = Design.AsTile(rt, Slate, 24f, out float lip);
            var button = rt.gameObject.AddComponent<Button>();
            var feel = rt.gameObject.AddComponent<PressFeel>();
            var face = Face("Face" + i, rt, lip);
            var portrait = Rect("Portrait", face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            portrait.offsetMin = new Vector2(6, 4); portrait.offsetMax = new Vector2(-6, -4);
            var pic = portrait.gameObject.AddComponent<Image>();
            pic.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSetup.Dir + def.driverId + ".png");
            pic.preserveAspect = true; pic.raycastTarget = false;
            var ring = Rect("Ring", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ring.offsetMin = new Vector2(-6, -6); ring.offsetMax = new Vector2(6, 6);
            rings[i] = ring.gameObject.AddComponent<Image>();
            rings[i].sprite = Design.TileEdge; rings[i].type = Image.Type.Sliced; rings[i].pixelsPerUnitMultiplier = Design.TileRadius / 30f;
            rings[i].color = Paper; rings[i].raycastTarget = false; rings[i].enabled = false;
            feel.Configure(Design.TileDown, face, lip * (Design.TileLip - 3f) / Design.TileLip);
            tiles[i] = rt;
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, rosterStrip.Pick, i);
        }
        new SerializedObject(rosterStrip).Do(s =>
        {
            s.FindProperty("carousel").objectReferenceValue = carousel;
            Fill(s.FindProperty("tiles"), tiles.Cast<Object>().ToArray());
            Fill(s.FindProperty("backs"), tileBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("rings"), rings.Cast<Object>().ToArray());
            s.FindProperty("idle").colorValue = Slate;
            s.FindProperty("picked").colorValue = Design.Ink600;
        });

        // What the driver says (their own voice, Voices.Say) in a comic bubble beside their head: the tail's tip is the
        // bubble's pivot, which SpeechBubble keeps by the head as the turntable turns.
        var bubble = Rect("Bubble", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        bubble.pivot = Vector2.zero;
        var bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.alpha = 0f; bubbleGroup.blocksRaycasts = false; bubbleGroup.interactable = false;
        var tail = Rect("Tail", bubble, Vector2.zero, Vector2.zero, new Vector2(30, 34), new Vector2(62, 62));
        tail.localRotation = Quaternion.Euler(0, 0, -135f);        // the play glyph turned to point back down at the head
        var tailImg = tail.gameObject.AddComponent<Image>();
        tailImg.sprite = Design.Icon("play"); tailImg.color = Paper; tailImg.raycastTarget = false;
        var speech = Rect("Speech", bubble, Vector2.zero, Vector2.zero, new Vector2(16, 26), new Vector2(320, 96));
        speech.pivot = Vector2.zero;
        Design.AsCard(speech, Paper).raycastTarget = false;
        var words = Label("Words", speech, display, "", 42, Deep, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        words.rectTransform.anchorMin = Vector2.zero; words.rectTransform.anchorMax = Vector2.one;
        words.rectTransform.offsetMin = new Vector2(28, 14); words.rectTransform.offsetMax = new Vector2(-28, -14);
        words.textWrappingMode = TextWrappingModes.Normal;
        var speechBubble = bubble.gameObject.AddComponent<SpeechBubble>();
        new SerializedObject(speechBubble).Do(s =>
        {
            s.FindProperty("bubble").objectReferenceValue = bubble;
            s.FindProperty("body").objectReferenceValue = speech;
            s.FindProperty("words").objectReferenceValue = words;
            s.FindProperty("group").objectReferenceValue = bubbleGroup;
            s.FindProperty("podium").objectReferenceValue = podium;
        });

        var so = new SerializedObject(carousel);
        so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        so.FindProperty("podium").objectReferenceValue = podium;
        Fill(so.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).Where(k => k != null).ToArray());
        Fill(so.FindProperty("driverPrefabs"), Directory.GetFiles(Prefabs + "Drivers", "Driver_*.prefab").Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        StandingDrivers(so);   // the driver stands on the disc, Beach Buggy's driver screen (MenuSetup.Select.cs)
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("speedBar").objectReferenceValue = bars[0];
        so.FindProperty("accelBar").objectReferenceValue = bars[1];
        so.FindProperty("handlingBar").objectReferenceValue = bars[2];
        so.FindProperty("weightBar").objectReferenceValue = bars[3];
        so.FindProperty("turboBar").objectReferenceValue = bars[4];
        so.FindProperty("attackBar").objectReferenceValue = bars[5];
        so.FindProperty("nextLabel").objectReferenceValue = next.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryLabel").objectReferenceValue = payB.GetComponentInChildren<TMP_Text>();
        so.FindProperty("tertiaryLabel").objectReferenceValue = payC.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryButton").objectReferenceValue = payB;
        so.FindProperty("tertiaryButton").objectReferenceValue = payC;
        so.ApplyModifiedPropertiesWithoutUndo();
        return screen;
    }

    /// Track select: the chosen circuit's key art full-bleed with the card over it. The one panel that is a picture
    /// rather than a scrim over the live vista, because a map is judged by what it looks like.
    static GameObject TrackScreen(RectTransform parent)
    {
        var screen = Rect("Track", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var carousel = screen.AddComponent<TrackCarousel>();

        // Overhang the safe area like the Dim rects: a picture stopping at the cutout leaves a strip of live vista.
        var art = Rect("Art", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        var fade = art.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = false;
        var single = Rect("Single", art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        single.raycastTarget = false;
        // Grand Prix: every circuit at once. The cells are half-screen, so full-screen key art keeps its aspect.
        var grid = Rect("Grid", art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var gutter = grid.gameObject.AddComponent<Image>();   // shows through the hairline between the four pictures
        gutter.color = Deep; gutter.raycastTarget = false;
        var cells = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var corner = new Vector2(i % 2 * 0.5f, (1 - i / 2) * 0.5f);
            var cell = Rect("Cell" + i, grid, corner, corner + Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            cell.offsetMin = new Vector2(4, 4); cell.offsetMax = new Vector2(-4, -4);
            cells[i] = cell.gameObject.AddComponent<Image>();
            cells[i].raycastTarget = false;
        }
        // The same left-to-right ramp as the title plate: it grounds the card and BACK, and leaves the picture alone.
        // A full-screen vertical scrim greyed the whole key art, which is the one thing this screen is for.
        var scrim = Rect("Scrim", screen.transform, Vector2.zero, new Vector2(0.50f, 1f), Vector2.zero, Vector2.zero);
        scrim.offsetMin = new Vector2(-320, -320); scrim.offsetMax = new Vector2(0, 320);
        var ramp = scrim.gameObject.AddComponent<Image>();
        ramp.sprite = FadeSprite(); ramp.color = Deep; ramp.raycastTarget = false;

        // and a bottom-up ramp under the row of track cards, so their names read over any picture
        var floor = Rect("Floor", screen.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -320), new Vector2(1200, 1000));
        floor.pivot = new Vector2(0.5f, 0);
        var floorRamp = floor.gameObject.AddComponent<Image>();
        floorRamp.sprite = FadeSprite(vertical: true); floorRamp.color = Deep; floorRamp.raycastTarget = false;

        var catcher = Rect("SwipeCatcher", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        catcher.gameObject.AddComponent<Image>().color = Color.clear;
        var swipe = catcher.gameObject.AddComponent<SwipeSelect>();
        new SerializedObject(swipe).Do(s => s.FindProperty("carousel").objectReferenceValue = carousel);   // the track cards keep their flick

        Header(screen.transform, "CHOOSE A TRACK", nameof(ScreenFlow.ShowSelect), "STEP 2 OF 2");

        // The same card as select, so the eye lands in the same place on consecutive screens: how to race at the top
        // (the two tabs), then what the chosen race is, then RACE.
        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 64); card.offsetMax = new Vector2(744, -172);
        Design.AsCard(card, Panel);

        var (tabBacks, tabLabels, tabButtons) = Segmented("Mode", card, new[] { "GRAND PRIX", "SINGLE RACE" },
                                                           new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(600, 88), 34);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tabButtons[0].onClick, carousel.GrandPrix);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tabButtons[1].onClick, carousel.SingleRace);

        var kicker = Label("Kicker", card, strong, "", 28, Muted, TextAlignmentOptions.Left,
                           new Vector2(0, 1), new Vector2(48, -148), new Vector2(608, 40));
        kicker.characterSpacing = 8f;
        var nameLabel = Label("Name", card, display, "", 104, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(46, -184), new Vector2(624, 112));
        nameLabel.enableAutoSizing = true; nameLabel.fontSizeMin = 64; nameLabel.fontSizeMax = 104;   // SUNSET SPEEDWAY is wider than the card
        // left column: the facts; right: the map
        var facts = Label("Facts", card, display, "", 44, Paper, TextAlignmentOptions.Left,
                          new Vector2(0, 1), new Vector2(48, -312), new Vector2(400, 56));
        // Label and pips in one container so the Grand Prix card can drop the whole row with one SetActive.
        var difficulty = Rect("Difficulty", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -386), new Vector2(440, 48));
        difficulty.pivot = new Vector2(0, 1);
        var bar = StatRow(difficulty, "DIFFICULTY", 0, pips: 3, size: 28, barX: 236);
        var bestCaption = Label("BestLabel", card, strong, "YOUR BEST", 28, Muted, TextAlignmentOptions.Left,
                                new Vector2(0, 1), new Vector2(48, -446), new Vector2(240, 40));
        bestCaption.characterSpacing = 6f;
        var best = Label("Best", card, strong, "—", 36, Paper, TextAlignmentOptions.Left,
                         new Vector2(0, 1), new Vector2(232, -442), new Vector2(220, 44));
        best.enableAutoSizing = true; best.fontSizeMin = 26; best.fontSizeMax = 36;
        var mapBox = Rect("Map", card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -300), new Vector2(196, 196));
        mapBox.pivot = new Vector2(1, 1);
        var mapImage = mapBox.gameObject.AddComponent<Image>();
        mapImage.preserveAspect = true; mapImage.raycastTarget = false;
        // the Grand Prix shows its running order where a single track shows its map
        var order = Rect("Order", card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-44, -300), new Vector2(196, 196));
        order.pivot = new Vector2(1, 1);
        int cols = Tracks.Count > 4 ? 3 : 2; float cell0 = 196f / cols;       // two by two for four tracks, three across for five or six
        for (int i = 0; i < Tracks.Count && i < cols * cols; i++)
        {
            var cell = Rect("Map" + i, order, new Vector2(0, 1), new Vector2(0, 1), new Vector2(i % cols * cell0 + cell0 / 2f, -(i / cols) * cell0 - cell0 / 2f), new Vector2(cell0 - 8f, cell0 - 8f));
            var img = cell.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{Tracks.At(i).id}_map.png");
            img.preserveAspect = true; img.raycastTarget = false; img.color = new Color(1, 1, 1, 0.9f);
            var n = Label("N", cell, display, (i + 1).ToString(), 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 40));
            n.raycastTarget = false;
        }
        var blurb = Label("Blurb", card, body, "", 30, Muted, TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(48, -516), new Vector2(600, 132));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        var race = Button("Race", card, "RACE", Accent, Paper, 60,
                          new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(584, 136), icon: "flag");
        Wire(race, nameof(ScreenFlow.StartRace));
        Shine(race);

        // Every track as a card along the bottom right: its picture, its name, its map in the corner. Tap one to race
        // it alone; in a Grand Prix they are all lit and numbered in running order.
        var row = Rect("Tracks", screen.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 48), Vector2.zero);
        int nTracks = Tracks.Count;
        float cw = nTracks > 4 ? 290f : 326f; const float ch = 232f, gap = 18f;      // five fit between the card and the edge at 290
        var cards = new RectTransform[nTracks]; var cardArt = new Image[nTracks]; var cardRings = new Image[nTracks]; var badges = new GameObject[nTracks];
        for (int i = 0; i < nTracks; i++)
        {
            var def = Tracks.At(i);
            var rt = Rect("Track" + i, row, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-(nTracks - 1 - i) * (cw + gap), 0), new Vector2(cw, ch));
            rt.pivot = new Vector2(1, 0);
            var back = Design.AsCard(rt, Deep);
            rt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, carousel.Pick, i);
            // the key art is 2:1; the card is wider than tall but not that wide, so the picture covers it and is cropped
            var pic = Rect("Art", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(ch * 2.1f, ch * 1.05f));
            cardArt[i] = pic.gameObject.AddComponent<Image>();
            cardArt[i].sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{def.id}.png");
            cardArt[i].raycastTarget = false;
            var shade = Rect("Shade", rt, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 150));
            shade.pivot = new Vector2(0.5f, 0);
            var shadeImg = shade.gameObject.AddComponent<Image>();
            shadeImg.sprite = FadeSprite(vertical: true); shadeImg.color = Deep; shadeImg.raycastTarget = false;
            var nm = Label("Name", rt, display, def.name.ToUpperInvariant(), 40, Paper, TextAlignmentOptions.BottomLeft,
                           new Vector2(0, 0), new Vector2(20, 14), new Vector2(cw - 110, 48));
            nm.enableAutoSizing = true; nm.fontSizeMin = 22; nm.fontSizeMax = 40;
            var map = Rect("Map", rt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 10), new Vector2(76, 76));
            map.pivot = new Vector2(1, 0);
            var mapImg = map.gameObject.AddComponent<Image>();
            mapImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{def.id}_map.png");
            mapImg.preserveAspect = true; mapImg.raycastTarget = false;
            var badge = Rect("Badge", rt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -14), new Vector2(56, 56));
            badge.pivot = new Vector2(0, 1);
            Design.AsPill(badge, Paper).raycastTarget = false;                 // cream, not gold: gold is RACE's alone
            Label("N", badge, display, (i + 1).ToString(), 36, Deep, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -1), new Vector2(56, 52));
            badges[i] = badge.gameObject;
            var ring = Rect("Ring", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cardRings[i] = ring.gameObject.AddComponent<Image>();
            cardRings[i].sprite = Design.TileEdge; cardRings[i].type = Image.Type.Sliced; cardRings[i].pixelsPerUnitMultiplier = Design.TileRadius / 32f;
            cardRings[i].color = Paper; cardRings[i].raycastTarget = false;
            cards[i] = rt;
        }

        var so = new SerializedObject(carousel);
        // Key art comes from the track builder, which may not have run yet: a null slot is a blank, not a failure.
        Fill(so.FindProperty("art"), Tracks.All.Select(t => (Object)AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{t.id}.png")).ToArray());
        Fill(so.FindProperty("maps"), Tracks.All.Select(t => (Object)AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{t.id}_map.png")).ToArray());
        var lengths = so.FindProperty("lengths");
        var metres = TrackLengths();
        lengths.arraySize = Tracks.Count;
        for (int i = 0; i < Tracks.Count; i++) lengths.GetArrayElementAtIndex(i).floatValue = metres.TryGetValue(Tracks.At(i).id, out var m) ? m : 0f;
        so.FindProperty("single").objectReferenceValue = single;
        so.FindProperty("grid").objectReferenceValue = grid.gameObject;
        Fill(so.FindProperty("cells"), cells.Cast<Object>().ToArray());
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("kickerLabel").objectReferenceValue = kicker;
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("bestLabel").objectReferenceValue = best;
        so.FindProperty("factsLabel").objectReferenceValue = facts;
        so.FindProperty("difficultyRow").objectReferenceValue = difficulty.gameObject;
        so.FindProperty("difficulty").objectReferenceValue = bar;
        so.FindProperty("mapImage").objectReferenceValue = mapImage;
        Fill(so.FindProperty("gpOnly"), new Object[] { order.gameObject });
        Fill(so.FindProperty("singleOnly"), new Object[] { bestCaption.gameObject, best.gameObject });
        Fill(so.FindProperty("cards"), cards.Cast<Object>().ToArray());
        Fill(so.FindProperty("cardArt"), cardArt.Cast<Object>().ToArray());
        Fill(so.FindProperty("cardRings"), cardRings.Cast<Object>().ToArray());
        Fill(so.FindProperty("cardBadges"), badges.Cast<Object>().ToArray());
        so.FindProperty("tabGp").objectReferenceValue = tabBacks[0];
        so.FindProperty("tabSingle").objectReferenceValue = tabBacks[1];
        so.FindProperty("tabGpLabel").objectReferenceValue = tabLabels[0];
        so.FindProperty("tabSingleLabel").objectReferenceValue = tabLabels[1];
        so.FindProperty("tabOn").colorValue = Design.Ink600;
        so.FindProperty("tabOff").colorValue = new Color(0, 0, 0, 0);
        so.FindProperty("textOn").colorValue = Paper;
        so.FindProperty("textOff").colorValue = Muted;
        so.ApplyModifiedPropertiesWithoutUndo();
        carousel.ShowInEditor(0);   // Start() never runs in edit mode: the saved scene and MenuPreview show a real card
        return screen;
    }

    /// Metres a lap per track, from the contract the track builder writes for the Worker (worker/src/tracks.json).
    static System.Collections.Generic.Dictionary<string, float> TrackLengths()
    {
        var found = new System.Collections.Generic.Dictionary<string, float>();
        const string path = "worker/src/tracks.json";
        if (!File.Exists(path)) { Debug.LogWarning("[menu] no " + path + ": track lengths left blank"); return found; }
        var json = File.ReadAllText(path);
        foreach (var t in Tracks.All)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + t.id + "\"\\s*:\\s*\\{[^}]*?\"lengthM\"\\s*:\\s*([0-9.]+)");
            if (m.Success) found[t.id] = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return found;
    }

    static GameObject BoardScreen(RectTransform parent)
    {
        var screen = Rect("Board", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        // Overhang the safe area: this sits inside it, and a dim stopping at the cutout leaves a bright live strip.
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.68f);
        Header(screen.transform, "RANKING", nameof(ScreenFlow.ShowTitle));
        var feed = screen.AddComponent<LeaderboardFeed>();

        // a tab per track, then the table: ten rows, medals for the top three, your own row lit
        var names = Tracks.All.Select(t => t.name.ToUpperInvariant()).ToArray();
        var (tabBacks, tabLabels, tabButtons) = Segmented("Tracks", screen.transform, names,
                                                           new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(1480, 92), 32);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(tabButtons[i].onClick, feed.SetTrack, i);
            tabBacks[i].color = i == 0 ? Design.Ink600 : new Color(0, 0, 0, 0);         // as it opens (the feed recolours them)
            tabLabels[i].color = i == 0 ? Paper : Muted;
        }

        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1480, 770));
        card.pivot = new Vector2(0.5f, 0);
        Design.AsCard(card, Panel);
        Label("RankCap", card, strong, "#", 26, Muted, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(72, -28), new Vector2(80, 36));
        Label("NameCap", card, strong, "DRIVER", 26, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(150, -28), new Vector2(400, 36)).characterSpacing = 8f;
        Label("TimeCap", card, strong, "BEST LAP", 26, Muted, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-56, -28), new Vector2(300, 36)).characterSpacing = 8f;

        const int count = 10; const float rowH = 60f, rowGap = 6f;
        var rowObjects = new GameObject[count]; var rowBacks = new Image[count]; var medals = new Image[count];
        var ranks = new TMP_Text[count]; var drivers = new TMP_Text[count]; var times = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            var rt = Rect("Row" + i, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -76 - i * (rowH + rowGap)), new Vector2(1400, rowH));
            rt.pivot = new Vector2(0.5f, 1);
            rowBacks[i] = Design.AsRow(rt, Slate);
            rowBacks[i].raycastTarget = false;
            var medal = Rect("Medal", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(72, 0), new Vector2(50, 50));
            medals[i] = Design.AsPill(medal, Accent);
            medals[i].raycastTarget = false;
            ranks[i] = Label("Rank", rt, display, (i + 1).ToString(), 32, Paper, TextAlignmentOptions.Center, new Vector2(0, 0.5f), new Vector2(72, 0), new Vector2(70, 50));
            ranks[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            drivers[i] = Label("Driver", rt, strong, "", 34, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(900, 50));
            times[i] = Label("Time", rt, strong, "", 34, Paper, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(300, 50));
            rowObjects[i] = rt.gameObject;
            rt.gameObject.SetActive(false);
        }
        var rows = Label("Rows", card, body, "Loading the fastest laps…", 36, Muted, TextAlignmentOptions.Center,
                         new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1200, 140));
        rows.textWrappingMode = TextWrappingModes.Normal;

        new SerializedObject(feed).Do(s =>
        {
            s.FindProperty("rows").objectReferenceValue = rows;
            Fill(s.FindProperty("rowObjects"), rowObjects.Cast<Object>().ToArray());
            Fill(s.FindProperty("rowBacks"), rowBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("medals"), medals.Cast<Object>().ToArray());
            Fill(s.FindProperty("ranks"), ranks.Cast<Object>().ToArray());
            Fill(s.FindProperty("names"), drivers.Cast<Object>().ToArray());
            Fill(s.FindProperty("times"), times.Cast<Object>().ToArray());
            Fill(s.FindProperty("tabs"), tabBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("tabLabels"), tabLabels.Cast<Object>().ToArray());
            s.FindProperty("rowIdle").colorValue = Slate;
            s.FindProperty("rowMine").colorValue = Design.Ink600;
            s.FindProperty("tabOn").colorValue = Design.Ink600;
            s.FindProperty("tabOff").colorValue = new Color(0, 0, 0, 0);
            s.FindProperty("textOn").colorValue = Paper;
            s.FindProperty("textOff").colorValue = Muted;
            var medalColours = s.FindProperty("medalColours");
            medalColours.arraySize = 3;
            medalColours.GetArrayElementAtIndex(0).colorValue = new Color(0.86f, 0.66f, 0.22f);   // the trophies' gold, silver, bronze (PodiumSetup)
            medalColours.GetArrayElementAtIndex(1).colorValue = new Color(0.66f, 0.68f, 0.74f);
            medalColours.GetArrayElementAtIndex(2).colorValue = new Color(0.70f, 0.42f, 0.24f);
        });
        return screen;
    }

    /// Switches, nothing else. Screen shake is an accessibility setting; the frame counter is a dev tool that
    /// should not be on screen in a submission video by default.
    static GameObject SettingsScreen(RectTransform parent)
    {
        var screen = Rect("Settings", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.68f);
        Header(screen.transform, "SETTINGS", nameof(ScreenFlow.ShowTitle));
        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(1240, 836));
        Design.AsCard(card, Panel);

        SettingRow(card, SettingToggle.Which.Steering, "STEERING", "", -32);
        SettingRow(card, SettingToggle.Which.AutoGas, "AUTO GAS", "The kart drives itself; brake to slow down", -144);
        SettingRow(card, SettingToggle.Which.Sound, "SOUND", "Everything you hear", -256);
        SettingRow(card, SettingToggle.Which.Music, "MUSIC", "The soundtrack only", -368);
        SettingRow(card, SettingToggle.Which.Haptics, "VIBRATION", "Taps, hits and boosts", -480);
        SettingRow(card, SettingToggle.Which.Shake, "SCREEN SHAKE", "Turn off if it makes you queasy", -592);
        SettingRow(card, SettingToggle.Which.Fps, "FRAME COUNTER", "For testing", -704);
        return screen;
    }

    /// A settings row: name and a short line on what it does on the left, the switch on the right. The whole row
    /// is the switch's hit area.
    internal static void SettingRow(RectTransform card, SettingToggle.Which which, string caption, string hint, float y)
    {
        var rt = Rect(caption, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(1140, 104));
        rt.pivot = new Vector2(0.5f, 1);
        Design.AsRow(rt, Design.Ink700);
        var button = rt.gameObject.AddComponent<Button>();
        rt.gameObject.AddComponent<PressFeel>();
        var label = Label(caption + "Label", rt, display, caption, 44, Paper, TextAlignmentOptions.Left,
                          new Vector2(0, 0.5f), new Vector2(40, 14), new Vector2(700, 56));
        label.rectTransform.pivot = new Vector2(0, 0.5f);
        var sub = Label(caption + "Hint", rt, body, hint, 26, Muted, TextAlignmentOptions.Left,
                        new Vector2(0, 0.5f), new Vector2(40, -26), new Vector2(700, 36));
        sub.rectTransform.pivot = new Vector2(0, 0.5f);

        // the switch: a trough with a knob that slides to ON. STEERING cycles three ways instead: a wider lit pill
        // with the chosen way written in it, and the hint line says how that way works
        bool cycle = which == SettingToggle.Which.Steering;
        var track = Rect("Switch", rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(cycle ? 220 : 140, 72));
        track.pivot = new Vector2(1, 0.5f);
        var trackImg = Design.AsPill(track, cycle ? Design.Lit : Deep);
        trackImg.raycastTarget = false;
        RectTransform knob = null;
        if (!cycle)
        {
            knob = Rect("Knob", track, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            Design.AsPill(knob, Paper).raycastTarget = false;
        }
        var state = cycle
            ? Label(caption + "State", track, strong, "", 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 44))
            : Label(caption + "State", rt, strong, "", 26, Muted, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-200, 0), new Vector2(100, 40));

        var toggle = rt.gameObject.AddComponent<SettingToggle>();
        var so = new SerializedObject(toggle);
        so.FindProperty("setting").enumValueIndex = (int)which;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("caption").stringValue = caption;
        so.FindProperty("track").objectReferenceValue = trackImg;
        so.FindProperty("knob").objectReferenceValue = knob;
        so.FindProperty("state").objectReferenceValue = state;
        if (cycle) so.FindProperty("hint").objectReferenceValue = sub;
        so.FindProperty("onColour").colorValue = Design.Lit;
        so.FindProperty("offColour").colorValue = Deep;
        so.FindProperty("travel").floatValue = 34f;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, toggle.Toggle);
        toggle.ShowInEditor();
    }


    /// RACE ONLINE: create (tracks, stake, visibility) on the left; join by code and the open public rooms on the
    /// right, Among Us style. One accent: CREATE.
    static GameObject RoomsScreen(RectTransform parent)
    {
        var screen = Rect("Rooms", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.68f);
        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 900));
        Design.AsCard(card, Panel);
        Label("Head", card, display, "RACE ONLINE", 68, Paper, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1000, 84));

        var panel = screen.AddComponent<RoomsPanel>();

        // left: create
        var lx = -350f;
        Label("CreateCap", card, body, "CREATE A ROOM", 28, Muted, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(lx, -134), new Vector2(560, 40)).characterSpacing = 8f;
        var trackChip = Chip("TrackChip", card, "GRAND PRIX", new Vector2(lx, -184), out var trackLabel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(trackChip.onClick, panel.NextTrack);
        var stakeChip = Chip("StakeChip", card, "FREE RACE", new Vector2(lx, -284), out var stakeLabel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(stakeChip.onClick, panel.NextStake);
        var visChip = Chip("VisibilityChip", card, "PUBLIC  ·  ANYONE CAN JOIN", new Vector2(lx, -384), out var visLabel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(visChip.onClick, panel.ToggleVisibility);
        Label("StakeHint", card, body, "Tap any to change · stakes are held on-chain", 26, Muted, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(lx, -484), new Vector2(560, 40));
        var create = Button("Create", card, "CREATE", Accent, Paper, 46,
                            new Vector2(0.5f, 1), new Vector2(lx, -536), new Vector2(560, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(create.onClick, panel.Create);

        // right: join by code, then the open rooms
        var rx = 350f;
        Label("JoinCap", card, body, "JOIN WITH A CODE", 28, Muted, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(rx, -134), new Vector2(560, 40)).characterSpacing = 8f;
        var field = InputField("Code", card, "A B C D E", new Vector2(rx - 120, -184), new Vector2(320, 92));
        var join = Button("Join", card, "JOIN", Slate, Paper, 40,
                          new Vector2(0.5f, 1), new Vector2(rx + 170, -184), new Vector2(220, 92));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(join.onClick, panel.JoinTyped);

        Label("OpenCap", card, body, "OPEN ROOMS", 28, Muted, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(rx, -304), new Vector2(560, 40)).characterSpacing = 8f;
        var rows = new TMP_Text[4]; var rowButtons = new Button[4];
        for (int i = 0; i < rows.Length; i++)
        {
            var rt = Rect("Room" + i, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(rx, -350 - i * 76), new Vector2(560, 66));
            rt.pivot = new Vector2(0.5f, 1);
            Design.AsRow(rt, Design.Ink700);
            rowButtons[i] = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(rowButtons[i].onClick, panel.JoinRow, i);
            rows[i] = Label("Row" + i + "Label", rt, body, i == 0 ? "No open rooms right now — make yours public" : "", 26, Paper, TextAlignmentOptions.Left,
                            new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(520, 50));
            rows[i].rectTransform.pivot = new Vector2(0, 0.5f);
            if (i == 0) rows[i].color = Muted;
        }

        var status = Label("Status", card, body, "", 28, Paper, TextAlignmentOptions.Center,
                           new Vector2(0.5f, 0), new Vector2(0, 118), new Vector2(1300, 40));
        var back = Button("Back", card, "BACK", Slate, Paper, 32,
                          new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(280, 78));
        Wire(back, nameof(ScreenFlow.ShowTitle));

        new SerializedObject(panel).Do(so =>
        {
            so.FindProperty("codeField").objectReferenceValue = field;
            so.FindProperty("trackLabel").objectReferenceValue = trackLabel;
            so.FindProperty("stakeLabel").objectReferenceValue = stakeLabel;
            so.FindProperty("visibilityLabel").objectReferenceValue = visLabel;
            so.FindProperty("status").objectReferenceValue = status;
            so.FindProperty("createButton").objectReferenceValue = create;
            so.FindProperty("joinButton").objectReferenceValue = join;
            Fill(so.FindProperty("roomRows"), rows.Cast<Object>().ToArray());
            Fill(so.FindProperty("roomButtons"), rowButtons.Cast<Object>().ToArray());
            so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
            so.FindProperty("garage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
        });
        return screen;
    }

    /// The lobby: code, who is in and what each staked, the pot, the stake chips, and one button that is always the next step.
    static GameObject LobbyScreen(RectTransform parent)
    {
        var screen = Rect("Lobby", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.68f);
        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 880));
        Design.AsCard(card, Panel);

        var code = Label("Code", card, display, "A B C D E", 132, Paper, TextAlignmentOptions.Center,
                         new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1200, 150));
        var info = Label("Info", card, body, "", 32, Muted, TextAlignmentOptions.Center,
                         new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(1300, 44));
        info.characterSpacing = 4f;

        // Stacked, full width: pool and prizes on one line, the fee note under it, then the racers. Side columns
        // overflowed into each other at 20:9.
        var pool = Label("Pool", card, strong, "", 34, Paper, TextAlignmentOptions.Center,
                         new Vector2(0.5f, 1), new Vector2(0, -236), new Vector2(1360, 48));
        pool.characterSpacing = 2f;
        var prizes = Label("Prizes", card, body, "", 28, Muted, TextAlignmentOptions.Center,
                           new Vector2(0.5f, 1), new Vector2(0, -284), new Vector2(1360, 40));
        var players = Label("Players", card, body, "", 38, Paper, TextAlignmentOptions.TopLeft,
                            new Vector2(0, 1), new Vector2(120, -342), new Vector2(1260, 270));
        // This seat's stake, picked before it pays: the room's stakes from its minimum up, on the prizes line (the
        // panel blanks that line while the chips show). The picked one takes the accent.
        var chips = new Button[5];
        for (int i = 0; i < chips.Length; i++)
        {
            var rt = Rect("Stake" + i, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 2) * 236, -280), new Vector2(220, 60));
            rt.pivot = new Vector2(0.5f, 1);
            Design.AsRow(rt, Slate);
            chips[i] = rt.gameObject.AddComponent<Button>();
            chips[i].targetGraphic = rt.GetComponent<Image>();
            rt.gameObject.AddComponent<PressFeel>();
            Label("Stake" + i + "Label", rt, strong, "", 30, Paper, TextAlignmentOptions.Center,
                  new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 50));
            rt.gameObject.SetActive(false);
        }

        var status = Label("Status", card, body, "", 30, Muted, TextAlignmentOptions.Center,
                           new Vector2(0.5f, 0), new Vector2(0, 198), new Vector2(1300, 44));

        var panel = screen.AddComponent<LobbyPanel>();
        for (int i = 0; i < chips.Length; i++) UnityEditor.Events.UnityEventTools.AddIntPersistentListener(chips[i].onClick, panel.PickStake, i);
        var share = Button("Share", card, "SHARE INVITE", Slate, Paper, 38,
                           new Vector2(0.5f, 0), new Vector2(-440, 64), new Vector2(380, 116));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(share.onClick, panel.ShareInvite);
        var primary = Button("Primary", card, "START RACE", Accent, Paper, 46,
                             new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(480, 116));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(primary.onClick, panel.Primary);
        var leave = Button("Leave", card, "LEAVE", Slate, Paper, 38,
                           new Vector2(0.5f, 0), new Vector2(440, 64), new Vector2(380, 116));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(leave.onClick, panel.Leave);

        new SerializedObject(panel).Do(so =>
        {
            so.FindProperty("codeLabel").objectReferenceValue = code;
            so.FindProperty("infoLabel").objectReferenceValue = info;
            so.FindProperty("poolLabel").objectReferenceValue = pool;
            so.FindProperty("prizeLabel").objectReferenceValue = prizes;
            so.FindProperty("playersLabel").objectReferenceValue = players;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("primaryLabel").objectReferenceValue = primary.GetComponentInChildren<TMP_Text>();
            so.FindProperty("primaryButton").objectReferenceValue = primary;
            Fill(so.FindProperty("stakeChips"), chips.Cast<Object>().ToArray());
            so.FindProperty("chipIdle").colorValue = Slate;
            so.FindProperty("chipPicked").colorValue = Design.Ink600;
            so.FindProperty("chipText").colorValue = Muted;
            so.FindProperty("chipPickedText").colorValue = Paper;
        });
        return screen;
    }

    /// The garage: the same podium and card as character select, showing karts and paints you can drive, try or buy.
    static GameObject GarageScreen(RectTransform parent, Transform podium, CharacterCarousel characters)
    {
        var screen = Rect("Garage", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var garage = screen.AddComponent<GaragePanel>();

        var catcher = Rect("SwipeCatcher", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        catcher.gameObject.AddComponent<Image>().color = Color.clear;
        var swipe = catcher.gameObject.AddComponent<SwipeSelect>();
        new SerializedObject(swipe).Do(s => { s.FindProperty("carousel").objectReferenceValue = garage; s.FindProperty("flick").boolValue = false; });

        ShopHeader(screen.transform, "GARAGE", 0);
        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 64); card.offsetMax = new Vector2(744, -172);
        Design.AsCard(card, Panel);

        var nameLabel = Label("Name", card, display, "PHOTON", 88, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -36), new Vector2(624, 96));
        nameLabel.enableAutoSizing = true; nameLabel.fontSizeMin = 60; nameLabel.fontSizeMax = 88;
        var blurb = Label("Blurb", card, body, "", 28, Muted, TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(48, -128), new Vector2(608, 72));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        // the five powers; their total is the price (KartPricing), and a locked kart shows what it would cost
        var powerBars = new StatBar[5];
        var powerNames = new[] { "SPEED", "TURBO", "GRIP", "ARMOR", "ATTACK" };
        for (int i = 0; i < 5; i++) powerBars[i] = StatRow(card, powerNames[i], -214 - i * 42, size: 24);
        var price = Label("Price", card, strong, "", 28, Paper, TextAlignmentOptions.Left,
                          new Vector2(0, 1), new Vector2(48, -428), new Vector2(624, 36));

        // paint swatches: the driver's own colour, the four for sale, then the Tour bosses' prizes
        var swatchCap = Label("PaintCap", card, body, "PAINT", 24, Muted, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -484), new Vector2(200, 36));
        swatchCap.characterSpacing = 6f;
        var swatches = new Image[1 + Paints.All.Length];
        for (int i = 0; i < swatches.Length; i++)
        {
            var rt = Rect("Swatch" + i, card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(176 + i * 45, -480), new Vector2(42, 42));   // eleven fit the card (five bosses, the gold)
            rt.pivot = new Vector2(0, 1);
            swatches[i] = Design.AsPill(rt, Color.white);
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(b.onClick, garage.Swatch, i);
        }

        var status = Label("Status", card, body, "", 24, Muted, TextAlignmentOptions.TopLeft,
                           new Vector2(0, 1), new Vector2(48, -540), new Vector2(608, 28));   // one short line above TEST DRIVE

        var secondary = Button("Secondary", card, "TEST DRIVE", Slate, Paper, 36,
                               new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(584, 96));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(secondary.onClick, garage.Secondary);
        var tertiary = Button("Tertiary", card, "PAY ORE", Slate, Paper, 36,
                              new Vector2(0.5f, 0), new Vector2(150, 168), new Vector2(284, 96));   // right half of the SOL row; the panel narrows SOL while this shows
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tertiary.onClick, garage.Tertiary);
        tertiary.gameObject.SetActive(false);
        var primary = Button("Primary", card, "UNLOCK", Accent, Paper, 46,
                             new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(584, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(primary.onClick, garage.Primary);

        var prevArrow = IconButton("Prev", screen.transform, "back", new Vector2(1, 0), new Vector2(-336, 72), new Vector2(144, 144));
        var nextArrow = IconButton("Next", screen.transform, "next", new Vector2(1, 0), new Vector2(-128, 72), new Vector2(144, 144));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(prevArrow.onClick, garage.Previous);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(nextArrow.onClick, garage.Next);

        var so = new SerializedObject(garage);
        so.FindProperty("garage").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
        so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        so.FindProperty("podium").objectReferenceValue = podium;
        Fill(so.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).Where(k => k != null).ToArray());
        Fill(so.FindProperty("driverPrefabs"), Directory.GetFiles(Prefabs + "Drivers", "Driver_*.prefab").Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        so.FindProperty("characters").objectReferenceValue = characters;
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("priceLabel").objectReferenceValue = price;
        so.FindProperty("statusLabel").objectReferenceValue = status;
        so.FindProperty("primaryLabel").objectReferenceValue = primary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryLabel").objectReferenceValue = secondary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("primaryButton").objectReferenceValue = primary;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary;
        so.FindProperty("tertiaryLabel").objectReferenceValue = tertiary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("tertiaryButton").objectReferenceValue = tertiary;
        Fill(so.FindProperty("swatches"), swatches.Cast<Object>().ToArray());
        Fill(so.FindProperty("powerBars"), powerBars.Cast<Object>().ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
        return screen;
    }

    /// The shop's items tab: five packs of race consumables, how many of each you hold, and the one you start with.
    static GameObject ItemsScreen(RectTransform parent, Transform podium)
    {
        var screen = Rect("Items", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var panel = screen.AddComponent<ItemsPanel>();
        ShopHeader(screen.transform, "GARAGE", 1);
        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 64); card.offsetMax = new Vector2(744, -172);
        Design.AsCard(card, Panel);

        // 13 Oct 2026 (the user: "rather than clicking on the list, I should be having left and right button"): one pack
        // at a time, big, paged with the arrows the karts use; the six as a strip of icons to show where you are
        var kicker = Label("Kicker", card, strong, "ITEMS", 28, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(48, -34), new Vector2(608, 36));
        kicker.characterSpacing = 10f;
        var nameLabel = Label("Name", card, display, "ROCKETS X3", 76, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(48, -70), new Vector2(608, 88));
        nameLabel.enableAutoSizing = true; nameLabel.fontSizeMin = 48; nameLabel.fontSizeMax = 76;
        var owned = Label("Owned", card, strong, "", 30, Accent, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(48, -160), new Vector2(608, 40));
        var blurb = Label("Blurb", card, body, "", 26, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(48, -206), new Vector2(608, 70));
        blurb.textWrappingMode = TextWrappingModes.Normal; blurb.enableAutoSizing = true; blurb.fontSizeMin = 20; blurb.fontSizeMax = 26;
        // the rule, where the START WITH chip was: before each race the player picks what to take (CarryPicker)
        var loadoutLabel = Label("Rule", card, strong, "", 24, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(600, 64));
        loadoutLabel.textWrappingMode = TextWrappingModes.Normal; loadoutLabel.enableAutoSizing = true; loadoutLabel.fontSizeMin = 18; loadoutLabel.fontSizeMax = 24;

        var packs = Inventory.Packs;
        var rows = new Button[packs.Length]; var rowLabels = new TMP_Text[packs.Length]; var rowPrices = new TMP_Text[0];
        for (int i = 0; i < packs.Length; i++)
        {
            var rt = Rect("Pip" + i, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - (packs.Length - 1) / 2f) * 96f, -440), new Vector2(84, 84));
            Design.AsRow(rt, Design.Ink700);
            rows[i] = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(rows[i].onClick, panel.Focus, i);
            var glyph = Rect("Glyph", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(46, 46));
            var glyphImg = glyph.gameObject.AddComponent<Image>();
            glyphImg.sprite = Design.Icon(Design.ItemGlyph(packs[i].item) ?? "trap"); glyphImg.color = Paper; glyphImg.preserveAspect = true; glyphImg.raycastTarget = false;
            rowLabels[i] = Label("Count", rt, strong, "", 20, Paper, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-6, 2), new Vector2(60, 26));
        }
        var prevArrow = IconButton("Prev", screen.transform, "back", new Vector2(1, 0), new Vector2(-336, 72), new Vector2(144, 144));
        var nextArrow = IconButton("Next", screen.transform, "next", new Vector2(1, 0), new Vector2(-128, 72), new Vector2(144, 144));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(prevArrow.onClick, panel.Previous);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(nextArrow.onClick, panel.Next);

        var secondary = Button("Secondary", card, "PAY", Slate, Paper, 36, new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(584, 96));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(secondary.onClick, panel.Secondary);
        var tertiary = Button("Tertiary", card, "PAY ORE", Slate, Paper, 36,
                              new Vector2(0.5f, 0), new Vector2(150, 168), new Vector2(284, 96));   // right half of the SOL row; the panel narrows SOL while this shows
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tertiary.onClick, panel.Tertiary);
        tertiary.gameObject.SetActive(false);
        var primary = Button("Primary", card, "UNLOCK", Accent, Paper, 46, new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(584, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(primary.onClick, panel.Primary);

        var so = new SerializedObject(panel);
        Fill(so.FindProperty("rows"), rows.Cast<Object>().ToArray());
        Fill(so.FindProperty("rowLabels"), rowLabels.Cast<Object>().ToArray());
        Fill(so.FindProperty("rowPrices"), rowPrices.Cast<Object>().ToArray());
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("ownedLabel").objectReferenceValue = owned;
        so.FindProperty("loadoutLabel").objectReferenceValue = loadoutLabel;
        so.FindProperty("primaryLabel").objectReferenceValue = primary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryLabel").objectReferenceValue = secondary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("primaryButton").objectReferenceValue = primary;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary;
        so.FindProperty("tertiaryLabel").objectReferenceValue = tertiary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("tertiaryButton").objectReferenceValue = tertiary;
        so.FindProperty("rowIdle").colorValue = Design.Ink700;
        so.FindProperty("rowFocus").colorValue = Accent;
        so.FindProperty("podium").objectReferenceValue = podium;
        // one model per pack, in Inventory.Packs order (rocket, homing, bomb, nitro, bouncer, sun bomb)
        var modelPaths = new System.Collections.Generic.Dictionary<string, string>
        {
            ["rocket"] = "Assets/_Game/Prefabs/Items/Rocket.prefab", ["homing"] = "Assets/_Game/Prefabs/Items/Homing.prefab",
            ["bomb"] = "Assets/_Game/Prefabs/Items/Bomb.prefab", ["nitro"] = "Assets/_Game/Art/Models/Props/Survival/bottle-large.fbx",
            ["bouncer"] = "Assets/_Game/Prefabs/Items/Bouncer.prefab", ["sunbomb"] = "Assets/_Game/Prefabs/Items/SunBomb.prefab",
        };
        Fill(so.FindProperty("models"), packs.Select(p => (Object)(modelPaths.TryGetValue(p.id, out var path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null)).ToArray());
        so.FindProperty("nitroMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Survival.mat");
        so.ApplyModifiedPropertiesWithoutUndo();
        return screen;
    }

    /// A row-shaped chooser: tap cycles its value. One label per intent, no dropdown widget.
    /// The gem balance as a tappable chip (GemChip keeps the text current); top-right anchored.
    static Button GemChip(RectTransform parent, Vector2 pos)
    {
        var b = Button("Gems", parent, "20 GEMS", new Color(Slate.r, Slate.g, Slate.b, 0.92f), Accent, 32, new Vector2(1, 1), pos, new Vector2(260, 96));
        var chip = b.gameObject.AddComponent<GemChip>();
        new SerializedObject(chip).Do(s => s.FindProperty("label").objectReferenceValue = b.GetComponentInChildren<TMP_Text>());
        Wire(b, nameof(ScreenFlow.ShowGems));
        return b;
    }

    /// The gem shop: balance, three packs, the three pay buttons (ItemsScreen's shape).
    static GameObject GemsScreen(RectTransform parent)
    {
        var screen = Rect("Gems", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var panel = screen.AddComponent<GemsPanel>();
        Header(screen.transform, "GEMS", nameof(ScreenFlow.BackFromGems));
        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 64); card.offsetMax = new Vector2(744, -172);
        Design.AsCard(card, Panel);

        var balance = Label("Balance", card, display, "YOU HAVE  20  GEMS", 60, Accent, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(48, -40), new Vector2(624, 72));
        var blurb = Label("Blurb", card, body, "", 26, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(48, -124), new Vector2(608, 84));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        var packs = GemsPanel.Packs;
        var rows = new Button[packs.Length]; var rowLabels = new TMP_Text[packs.Length]; var rowPrices = new TMP_Text[packs.Length];
        for (int i = 0; i < packs.Length; i++)
        {
            var rt = Rect("Row" + i, card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -236 - i * 76), new Vector2(600, 64));
            rt.pivot = new Vector2(0, 1);
            Design.AsRow(rt, Design.Ink700);
            rows[i] = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(rows[i].onClick, panel.Focus, i);
            var glyph = Rect("Glyph", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(40, 40));
            var glyphImg = glyph.gameObject.AddComponent<Image>();
            glyphImg.sprite = Design.Icon("trophy"); glyphImg.color = Accent; glyphImg.preserveAspect = true; glyphImg.raycastTarget = false;
            rowLabels[i] = Label("Label", rt, strong, packs[i].name, 28, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(76, 0), new Vector2(380, 44));
            rowPrices[i] = Label("Price", rt, body, "", 26, Muted, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(200, 44));
        }

        var secondary = Button("Secondary", card, "PAY", Slate, Paper, 36, new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(584, 96));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(secondary.onClick, panel.Secondary);
        var tertiary = Button("Tertiary", card, "PAY ORE", Slate, Paper, 36, new Vector2(0.5f, 0), new Vector2(150, 168), new Vector2(284, 96));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tertiary.onClick, panel.Tertiary);
        tertiary.gameObject.SetActive(false);
        var primary = Button("Primary", card, "BUY", Accent, Paper, 46, new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(584, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(primary.onClick, panel.Primary);

        var so = new SerializedObject(panel);
        Fill(so.FindProperty("rows"), rows.Cast<Object>().ToArray());
        Fill(so.FindProperty("rowLabels"), rowLabels.Cast<Object>().ToArray());
        Fill(so.FindProperty("rowPrices"), rowPrices.Cast<Object>().ToArray());
        so.FindProperty("balanceLabel").objectReferenceValue = balance;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("primaryLabel").objectReferenceValue = primary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryLabel").objectReferenceValue = secondary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("tertiaryLabel").objectReferenceValue = tertiary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("primaryButton").objectReferenceValue = primary;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary;
        so.FindProperty("tertiaryButton").objectReferenceValue = tertiary;
        so.FindProperty("rowIdle").colorValue = Design.Ink700;
        so.FindProperty("rowFocus").colorValue = Design.Ink600;
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.ShowInEditor(1);
        return screen;
    }

    static Button Chip(string name, RectTransform card, string caption, Vector2 offset, out TMP_Text label)
    {
        var rt = Rect(name, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), offset, new Vector2(520, 92));
        rt.pivot = new Vector2(0.5f, 1);
        Design.AsTile(rt, Design.Ink700, 20f, out float lip);
        var button = rt.gameObject.AddComponent<Button>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var face = Face(name, rt, lip);
        label = Label(name + "Label", face, strong, caption, 36, Paper, TextAlignmentOptions.Center,
                      new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 60));
        label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = 36;
        feel.Configure(Design.TileDown, face, lip * (Design.TileLip - 3f) / Design.TileLip);
        return button;
    }

    /// A five-letter code field built from parts (TMP's default-controls helper needs a resources pack we do not ship).
    static TMP_InputField InputField(string name, RectTransform card, string placeholderText, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), offset, dims);
        rt.pivot = new Vector2(0.5f, 1);
        Design.AsRow(rt, Design.Ink700);
        var field = rt.gameObject.AddComponent<TMP_InputField>();
        var area = Rect("Text Area", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        area.offsetMin = new Vector2(24, 8); area.offsetMax = new Vector2(-24, -8);
        area.gameObject.AddComponent<RectMask2D>();
        var text = Label("Text", area, display, "", 52, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.sizeDelta = Vector2.zero;
        text.characterSpacing = 12f; text.raycastTarget = false;
        var placeholder = Label("Placeholder", area, display, placeholderText, 52, new Color(Muted.r, Muted.g, Muted.b, 0.5f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        placeholder.rectTransform.anchorMin = Vector2.zero; placeholder.rectTransform.anchorMax = Vector2.one; placeholder.rectTransform.sizeDelta = Vector2.zero;
        placeholder.characterSpacing = 12f; placeholder.raycastTarget = false;
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.fontAsset = display;
        field.characterLimit = 5;
        field.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        field.contentType = TMP_InputField.ContentType.Alphanumeric;
        field.pointSize = 52;
        field.caretColor = Accent;
        field.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        field.customCaretColor = true;
        return field;
    }

    // ---------- builders ----------
    static StatBar StatRow(RectTransform card, string label, float y, int pips = 5, float size = 32f, float barX = 300f)
    {
        var text = Label(label + "Label", card, body, label, size, Muted, TextAlignmentOptions.Left,
                         new Vector2(0, 1), new Vector2(48, y), new Vector2(240, 48));
        text.characterSpacing = 6f;
        var row = Rect(label + "Bar", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(barX, y - 6), new Vector2(380, 30));
        row.pivot = new Vector2(0, 1);
        var bar = row.gameObject.AddComponent<StatBar>();
        var images = new Image[pips];
        for (int i = 0; i < pips; i++)
        {
            var pip = Rect("Pip" + i, row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 70 + 26, 0), new Vector2(56, 22));
            images[i] = Design.AsRow(pip, Color.white);
        }
        var so = new SerializedObject(bar);
        Fill(so.FindProperty("pips"), images.Cast<Object>().ToArray());
        so.FindProperty("on").colorValue = Accent;
        so.FindProperty("off").colorValue = Design.Ink600;
        so.FindProperty("label").objectReferenceValue = text;
        so.FindProperty("title").stringValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Color colour,
                          TextAlignmentOptions align, Vector2 anchor, Vector2 offset, Vector2 dims)
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

    /// Every action in the game is a pill you can press: its face over a darker lip, the face (and what is written on
    /// it) dropping into the lip on press-in (PressFeel). Nothing else is. `icon` sits before the words, or after
    /// them for a "go on" arrow.
    static Button Button(string name, Transform parent, string caption, Color tint, Color textColour,
                         float textSize, Vector2 anchor, Vector2 offset, Vector2 dims, string icon = null, bool iconAfter = false)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        Design.AsButton(rt, tint, out float lip);
        var button = rt.gameObject.AddComponent<Button>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var colours = button.colors;
        colours.highlightedColor = Color.white;
        colours.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colours.disabledColor = new Color(0.6f, 0.6f, 0.66f, 0.75f);
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        var face = Face(name, rt, lip);
        var ink = tint == Design.Accent ? Design.OnAccent : textColour;
        var label = Label(name + "Label", face, display, caption, textSize, ink, TextAlignmentOptions.Center,
                          new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.enableAutoSizing = true;
        label.fontSizeMin = textSize * 0.65f;
        label.fontSizeMax = textSize;
        label.rectTransform.offsetMin = new Vector2(28, 0);
        label.rectTransform.offsetMax = new Vector2(-28, -2);
        if (icon != null)
        {
            float h = dims.y - lip, s = h * 0.46f;
            var glyph = Rect(name + "Icon", face, new Vector2(iconAfter ? 1 : 0, 0.5f), new Vector2(iconAfter ? 1 : 0, 0.5f),
                             new Vector2(iconAfter ? -h * 0.42f : h * 0.42f, 0), new Vector2(s, s));
            var img = glyph.gameObject.AddComponent<Image>();
            img.sprite = IconSprite(icon); img.color = ink; img.raycastTarget = false; img.preserveAspect = true;
            if (iconAfter) label.rectTransform.offsetMax = new Vector2(-h * 0.62f, -2);
            else label.rectTransform.offsetMin = new Vector2(h * 0.62f, 0);
        }
        feel.Configure(Design.PillDown, face, lip * (Design.Lip - 4f) / Design.Lip);
        return button;
    }

    /// What is written on a pressable thing: the area above its lip, which sinks with it.
    static RectTransform Face(string name, RectTransform rt, float lip)
    {
        var face = Rect(name + "Face", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        face.offsetMin = new Vector2(0, lip); face.offsetMax = Vector2.zero;
        return face;
    }

    /// A round button with a glyph: back, settings, the garage's arrows.
    static Button IconButton(string name, Transform parent, string icon, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, size);
        rt.pivot = anchor;
        Design.AsButton(rt, Slate, out float lip);
        var button = rt.gameObject.AddComponent<Button>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var face = Face(name, rt, lip);
        var glyph = Rect(name + "Icon", face, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.46f);
        var img = glyph.gameObject.AddComponent<Image>();
        img.sprite = IconSprite(icon);
        img.color = Paper;
        img.preserveAspect = true;
        img.raycastTarget = false;
        feel.Configure(Design.PillDown, face, lip * (Design.Lip - 4f) / Design.Lip);
        return button;
    }

    /// A pressable tile with a glyph over its name: the title's doors (online, garage, ranking).
    static Button Tile(string name, Transform parent, string icon, string caption, Vector2 anchor, Vector2 offset, Vector2 dims, float glyph = 72f)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        Design.AsTile(rt, Slate, 28f, out float lip);
        var button = rt.gameObject.AddComponent<Button>();
        var feel = rt.gameObject.AddComponent<PressFeel>();
        var face = Face(name, rt, lip);
        var glyphRt = Rect(name + "Icon", face, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(glyph, glyph));
        glyphRt.pivot = new Vector2(0.5f, 1);
        var img = glyphRt.gameObject.AddComponent<Image>();
        img.sprite = IconSprite(icon); img.color = Paper; img.preserveAspect = true; img.raycastTarget = false;
        var label = Label(name + "Label", face, display, caption, 34, Paper, TextAlignmentOptions.Center,
                          new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(dims.x - 20, 44));
        label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = 34;
        feel.Configure(Design.TileDown, face, lip * (Design.TileLip - 3f) / Design.TileLip);
        return button;
    }

    /// The top bar of every screen under the title: a round back button and the screen's name, over a soft scrim so
    /// it reads on any backdrop. `step` is a quiet "STEP 1 OF 2" at the far right on the screens that are a sequence.
    static RectTransform Header(Transform screen, string title, string backMethod, string step = null)
    {
        // Call it after the screen's backdrop and before its cards: the shade goes over the one and under the other.
        var shade = Rect("HeaderShade", screen, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -120), new Vector2(1200, 480));
        shade.localScale = new Vector3(1, -1, 1);                  // the bottom-up ramp, turned over about its middle: dark at the top
        var ramp = shade.gameObject.AddComponent<Image>();
        ramp.sprite = FadeSprite(vertical: true); ramp.color = Deep; ramp.raycastTarget = false;   // full: the track art can be bright day

        var bar = Rect("Header", screen, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -28), new Vector2(0, 112));
        bar.pivot = new Vector2(0.5f, 1);
        var back = IconButton("Back", bar, "back", new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(108, 108));
        Wire(back, backMethod);
        Label("Title", bar, display, title, 80, Paper, TextAlignmentOptions.Left,
              new Vector2(0, 0.5f), new Vector2(204, 2), new Vector2(1400, 104));
        if (step != null)
        {
            var s = Label("Step", bar, strong, step, 28, Muted, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-64, 0), new Vector2(400, 48));
            s.characterSpacing = 10f;
        }
        return bar;
    }

    /// The shop's top bar: back to the title, and its two tabs (karts, items), the open one lit.
    static void ShopHeader(Transform screen, string title, int open)
    {
        var bar = Header(screen, title, nameof(ScreenFlow.ShowTitle));
        var (backs, labels, buttons) = Segmented("Tabs", bar, new[] { "KARTS", "ITEMS" }, new Vector2(1, 0.5f), new Vector2(-64, 0), new Vector2(440, 96), 34);
        for (int i = 0; i < 2; i++) { backs[i].color = i == open ? Design.Ink600 : new Color(0, 0, 0, 0); labels[i].color = i == open ? Paper : Muted; }
        Wire(buttons[0], nameof(ScreenFlow.ShowGarage));
        Wire(buttons[1], nameof(ScreenFlow.ShowItems));
    }

    /// Two to four options in one trough: the chosen one is lit (the lit violet, Paper words), the rest sit back.
    /// The caller colours them at run time; this lays them out and wires nothing.
    static (Image[] backs, TMP_Text[] labels, Button[] buttons) Segmented(string name, Transform parent, string[] options,
                                                                           Vector2 anchor, Vector2 offset, Vector2 dims, float textSize = 32)
    {
        var bar = Rect(name, parent, anchor, anchor, offset, dims);
        bar.pivot = anchor;
        var trough = Design.AsPill(bar, Deep);
        trough.raycastTarget = false;
        int n = options.Length;
        float pad = 8f, w = (dims.x - pad * 2) / n;
        var backs = new Image[n]; var labels = new TMP_Text[n]; var buttons = new Button[n];
        for (int i = 0; i < n; i++)
        {
            var seg = Rect(name + i, bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(pad + w * i + w * 0.5f, 0), new Vector2(w - 4, dims.y - pad * 2));
            backs[i] = Design.AsPill(seg, Slate);
            buttons[i] = seg.gameObject.AddComponent<Button>();
            seg.gameObject.AddComponent<PressFeel>();
            labels[i] = Label(name + i + "Label", seg, display, options[i], textSize, Paper, TextAlignmentOptions.Center,
                              new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 24, dims.y - pad * 2));
            labels[i].enableAutoSizing = true; labels[i].fontSizeMin = textSize * 0.6f; labels[i].fontSizeMax = textSize;
        }
        return (backs, labels, buttons);
    }

    /// The light that crosses the primary button every few seconds (Shine), clipped to the button by a Mask.
    static void Shine(Button button)
    {
        var rt = (RectTransform)button.transform;
        rt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        float h = rt.sizeDelta.y;
        var band = Rect("Shine", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(h * 0.8f, h * 2.2f));
        band.localRotation = Quaternion.Euler(0, 0, -20f);
        band.anchoredPosition = new Vector2(-(rt.sizeDelta.x * 0.5f + h * 0.8f), 0);   // parked off the left end until it runs
        var img = band.gameObject.AddComponent<Image>();
        img.sprite = Design.Band; img.color = new Color(1f, 1f, 1f, 0.7f); img.raycastTarget = false;
        new SerializedObject(rt.gameObject.AddComponent<KartCup.Shine>()).Do(s => s.FindProperty("band").objectReferenceValue = band);
    }

    /// A kit icon (Icons/), or one of Design's drawn ones (play, back, next, globe, trophy, kart, user, flag).
    static Sprite IconSprite(string icon) =>
        System.Array.IndexOf(Design.Drawn, icon) >= 0
            ? Design.Icon(icon)
            : Sprite(icon) ?? AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Icons/{icon}.png");

    /// A picture used as UI: made a sprite on first use (uncompressed, no mips: it is a logo).
    static Sprite UiSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Kit/{name}.png");

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Vector2 dims)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.anchoredPosition = offset; rt.sizeDelta = dims;
        return rt;
    }

    static void Wire(Button button, string method)
    {
        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        if (flow == null) return;
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), flow, method);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    static GameObject Place(string fbxPath, Material mat, Vector3 pos, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) return new GameObject("missing");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one * scale;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void Fill(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Do(this SerializedObject so, System.Action<SerializedObject> edit) { edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }
}
