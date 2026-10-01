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
public static class MenuSetup
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
        var items = ItemsScreen(safe);

        var so = new SerializedObject(flow);
        so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        so.FindProperty("playLabel").objectReferenceValue = title.transform.Find("Plate/Play/PlayLabel")?.GetComponent<TMP_Text>();
        var panels = so.FindProperty("panels");
        panels.arraySize = 9;
        panels.GetArrayElementAtIndex(0).objectReferenceValue = title;
        panels.GetArrayElementAtIndex(1).objectReferenceValue = select;
        panels.GetArrayElementAtIndex(2).objectReferenceValue = board;
        panels.GetArrayElementAtIndex(3).objectReferenceValue = settings;
        panels.GetArrayElementAtIndex(4).objectReferenceValue = track;
        panels.GetArrayElementAtIndex(5).objectReferenceValue = rooms;
        panels.GetArrayElementAtIndex(6).objectReferenceValue = lobby;
        panels.GetArrayElementAtIndex(7).objectReferenceValue = garage;
        panels.GetArrayElementAtIndex(8).objectReferenceValue = items;
        so.ApplyModifiedPropertiesWithoutUndo();

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
    static GameObject TitleScreen(RectTransform parent)
    {
        var screen = Rect("Title", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        // Behind the title is the street meet (MenuMeet): the drivers by their karts, the teaser on the hoarding.
        // A scrim, not a card: the title has to stay legible over it without boxing it in.
        var plate = Rect("Plate", screen.transform, new Vector2(0, 0), new Vector2(0.47f, 1f), Vector2.zero, Vector2.zero);
        // The scrim is its own layer behind the content: overhanging the plate itself would drag every child
        // label with it, because they are anchored to the plate's corners.
        var scrim = Rect("Scrim", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        scrim.offsetMin = new Vector2(-320, -320); scrim.offsetMax = new Vector2(260, 320);   // cover the camera cutout, and reach past the wordmark
        var fade = scrim.gameObject.AddComponent<Image>();
        fade.sprite = FadeSprite();
        fade.color = Deep;
        fade.raycastTarget = false;

        var kicker = Label("Kicker", plate, body, "BEACH GRAND PRIX", 36, Accent, TextAlignmentOptions.Left,
                           new Vector2(0, 1), new Vector2(120, -168), new Vector2(1240, 60));
        kicker.characterSpacing = 14f;                       // an eyebrow reads as a label, not a sentence

        Label("Title1", plate, display, "SEEKER", 190, Paper, TextAlignmentOptions.Left,
              new Vector2(0, 1), new Vector2(116, -216), new Vector2(1100, 210));
        Label("Title2", plate, display, "KART CUP", 190, Paper, TextAlignmentOptions.Left,
              new Vector2(0, 1), new Vector2(116, -380), new Vector2(1100, 210));
        Label("Sub", plate, body, "Race the island. Put your lap on-chain.", 38, Muted,
              TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(120, -576), new Vector2(1000, 60));

        var play = Button("Play", plate, "PLAY", Accent, Paper, 62,
                          new Vector2(0, 0), new Vector2(120, 300), new Vector2(520, 140));
        Wire(play, nameof(ScreenFlow.Play));

        // Second row: the two doors that make it a game between people — race friends, and the garage they see you in.
        var online = Button("Online", plate, "RACE ONLINE", Slate, Paper, 34,
                            new Vector2(0, 0), new Vector2(120, 176), new Vector2(330, 96));
        Wire(online, nameof(ScreenFlow.ShowRooms));
        var garageBtn = Button("Garage", plate, "GARAGE", Slate, Paper, 34,
                               new Vector2(0, 0), new Vector2(470, 176), new Vector2(260, 96));
        Wire(garageBtn, nameof(ScreenFlow.ShowGarage));

        var boardBtn = Button("Board", plate, "LEADERBOARD", Slate, Paper, 34,
                              new Vector2(0, 0), new Vector2(120, 64), new Vector2(390, 96));
        Wire(boardBtn, nameof(ScreenFlow.ShowLeaderboard));

        var walletBtn = Button("Wallet", plate, "SIGN IN", Slate, Paper, 34,
                               new Vector2(0, 0), new Vector2(530, 64), new Vector2(260, 96));
        var wallet = new GameObject("WalletFlow").AddComponent<WalletFlow>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(walletBtn.onClick, wallet.Login);

        var cog = IconButton("Settings", plate, "icon_cog", new Vector2(0, 0), new Vector2(810, 64), new Vector2(96, 96));
        Wire(cog, nameof(ScreenFlow.ShowSettings));

        // The wordmark is the film's own lockup (tools/brand/make_lockup.py lifts it from the teaser's title frames):
        // the gold letters on their violet shell, the tagline in its badge. It stands in for the typeset title and the
        // eyebrow over it, and the on-chain line moves up under it.
        foreach (var typeset in new[] { "Title1", "Title2", "Kicker" })
            if (plate.Find(typeset) is Transform old) Object.DestroyImmediate(old.gameObject);
        var lockup = UiSprite(Art + "Textures/UI/kartel_lockup.png");
        if (lockup == null) Debug.LogWarning("[menu] no kartel_lockup.png: run tools/brand/make_lockup.py");
        var logo = Rect("Logo", plate, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -84), new Vector2(1120, 410));
        logo.pivot = new Vector2(0, 1);
        var mark = logo.gameObject.AddComponent<Image>();
        mark.sprite = lockup; mark.preserveAspect = true; mark.raycastTarget = false;
        plate.Find("Sub").GetComponent<RectTransform>().anchoredPosition = new Vector2(120, -512);

        return screen;
    }

    static GameObject SelectScreen(RectTransform parent, Transform podium, CharacterCarousel carousel)
    {
        var screen = Rect("Select", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        // First child so every control draws (and raycasts) above it: a drag on the open vista turns/flicks the kart.
        var catcher = Rect("SwipeCatcher", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        catcher.gameObject.AddComponent<Image>().color = Color.clear;
        var swipe = catcher.gameObject.AddComponent<SwipeSelect>();
        new SerializedObject(swipe).Do(s => s.FindProperty("carousel").objectReferenceValue = carousel);

        var card = Rect("Card", screen.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(60, 0), new Vector2(760, -160));
        card.pivot = new Vector2(0, 0.5f);
        card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(0, 1);
        card.offsetMin = new Vector2(64, 112); card.offsetMax = new Vector2(744, -152);
        Design.AsCard(card, Panel);

        var nameLabel = Label("Name", card, display, "NODEY", 112, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -56), new Vector2(624, 120));
        var blurb = Label("Blurb", card, body, "", 36, Muted, TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(48, -180), new Vector2(608, 120));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        var bars = new StatBar[6];
        var labels = new[] { "SPEED", "ACCEL", "GRIP", "WEIGHT", "TURBO", "ATTACK" };
        for (int i = 0; i < 6; i++) bars[i] = StatRow(card, labels[i], -268 - i * 56);

        var next = Button("Next", card, "NEXT", Accent, Paper, 52,
                          new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(560, 128));
        Wire(next, nameof(ScreenFlow.ShowTrack));

        var back = Button("Back", screen.transform, "BACK", Slate, Paper, 32,
                          new Vector2(0, 1), new Vector2(64, -36), new Vector2(200, 88));
        back.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        Wire(back, nameof(ScreenFlow.ShowTitle));

        // both arrows hug the bottom right, clear of the kart and of the card
        var prevArrow = IconButton("Prev", screen.transform, "arrow_basic_w", new Vector2(1, 0), new Vector2(-336, 72), new Vector2(144, 144));
        var nextArrow = IconButton("Next", screen.transform, "arrow_basic_e", new Vector2(1, 0), new Vector2(-128, 72), new Vector2(144, 144));

        var so = new SerializedObject(carousel);
        so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        so.FindProperty("podium").objectReferenceValue = podium;
        Fill(so.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).Where(k => k != null).ToArray());
        Fill(so.FindProperty("driverPrefabs"), Directory.GetFiles(Prefabs + "Drivers", "Driver_*.prefab").Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("speedBar").objectReferenceValue = bars[0];
        so.FindProperty("accelBar").objectReferenceValue = bars[1];
        so.FindProperty("handlingBar").objectReferenceValue = bars[2];
        so.FindProperty("weightBar").objectReferenceValue = bars[3];
        so.FindProperty("turboBar").objectReferenceValue = bars[4];
        so.FindProperty("attackBar").objectReferenceValue = bars[5];
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(prevArrow.onClick, carousel.Previous);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(nextArrow.onClick, carousel.Next);
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

        var catcher = Rect("SwipeCatcher", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        catcher.gameObject.AddComponent<Image>().color = Color.clear;
        var swipe = catcher.gameObject.AddComponent<SwipeSelect>();
        new SerializedObject(swipe).Do(s => s.FindProperty("carousel").objectReferenceValue = carousel);

        // same card as select, so the eye lands in the same place on consecutive screens
        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 112); card.offsetMax = new Vector2(744, -152);
        Design.AsCard(card, Panel);

        var kicker = Label("Kicker", card, body, "", 32, Muted, TextAlignmentOptions.Left,
                           new Vector2(0, 1), new Vector2(48, -48), new Vector2(608, 44));
        kicker.characterSpacing = 8f;
        var nameLabel = Label("Name", card, display, "", 112, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -92), new Vector2(624, 120));
        nameLabel.enableAutoSizing = true; nameLabel.fontSizeMin = 72; nameLabel.fontSizeMax = 112;   // SUNSET SPEEDWAY is wider than the card
        var blurb = Label("Blurb", card, body, "", 36, Muted, TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(48, -224), new Vector2(608, 160));
        blurb.textWrappingMode = TextWrappingModes.Normal;
        // Label and pips in one container so the Grand Prix card can drop the whole row with one SetActive.
        var difficulty = Rect("Difficulty", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -400), new Vector2(680, 48));
        difficulty.pivot = new Vector2(0, 1);
        var bar = StatRow(difficulty, "DIFFICULTY", 0, pips: 3);
        var bestCaption = Label("BestLabel", card, body, "BEST", 32, Muted, TextAlignmentOptions.Left,
                                new Vector2(0, 1), new Vector2(48, -472), new Vector2(240, 48));
        bestCaption.characterSpacing = 6f;
        var best = Label("Best", card, body, "—", 40, Paper, TextAlignmentOptions.Left,
                         new Vector2(0, 1), new Vector2(300, -478), new Vector2(380, 48));

        var race = Button("Race", card, "RACE", Accent, Paper, 52,
                          new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(560, 128));
        Wire(race, nameof(ScreenFlow.StartRace));

        var back = Button("Back", screen.transform, "BACK", Slate, Paper, 32,
                          new Vector2(0, 1), new Vector2(64, -36), new Vector2(200, 88));
        back.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        Wire(back, nameof(ScreenFlow.ShowSelect));

        var prevArrow = IconButton("Prev", screen.transform, "arrow_basic_w", new Vector2(1, 0), new Vector2(-336, 72), new Vector2(144, 144));
        var nextArrow = IconButton("Next", screen.transform, "arrow_basic_e", new Vector2(1, 0), new Vector2(-128, 72), new Vector2(144, 144));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(prevArrow.onClick, carousel.Previous);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(nextArrow.onClick, carousel.Next);

        var so = new SerializedObject(carousel);
        // Key art comes from the track builder, which may not have run yet: a null slot is a blank, not a failure.
        Fill(so.FindProperty("art"), Tracks.All.Select(t => (Object)AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{t.id}.png")).ToArray());
        so.FindProperty("single").objectReferenceValue = single;
        so.FindProperty("grid").objectReferenceValue = grid.gameObject;
        Fill(so.FindProperty("cells"), cells.Cast<Object>().ToArray());
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("kickerLabel").objectReferenceValue = kicker;
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("bestLabel").objectReferenceValue = best;
        so.FindProperty("difficultyRow").objectReferenceValue = difficulty.gameObject;
        so.FindProperty("difficulty").objectReferenceValue = bar;
        so.ApplyModifiedPropertiesWithoutUndo();
        carousel.ShowInEditor(0);   // Start() never runs in edit mode: the saved scene and MenuPreview show a real card
        return screen;
    }

    static GameObject BoardScreen(RectTransform parent)
    {
        var screen = Rect("Board", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        // Overhang the safe area: this sits inside it, and a dim stopping at the cutout leaves a bright live strip.
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.68f);
        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 840));
        Design.AsCard(card, Panel);
        Label("Head", card, display, "BEST TIMES", 72, Paper, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(1000, 90));
        var rows = Label("Rows", card, body, "loading…", 40, Paper, TextAlignmentOptions.TopLeft,
                         new Vector2(0.5f, 1), new Vector2(0, -184), new Vector2(1040, 520));
        var back = Button("Back", card, "BACK", Slate, Paper, 34,
                          new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(300, 100));
        Wire(back, nameof(ScreenFlow.ShowTitle));

        var feed = screen.AddComponent<LeaderboardFeed>();
        new SerializedObject(feed).Do(s => s.FindProperty("rows").objectReferenceValue = rows);
        return screen;
    }

    /// Three switches, nothing else. Screen shake is an accessibility setting; the frame counter is a dev tool that
    /// should not be on screen in a submission video by default.
    static GameObject SettingsScreen(RectTransform parent)
    {
        var screen = Rect("Settings", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.68f);
        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 760));
        Design.AsCard(card, Panel);
        Label("Head", card, display, "SETTINGS", 72, Paper, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(1000, 90));

        SettingRow(card, SettingToggle.Which.Shake, "SCREEN SHAKE", -184);
        SettingRow(card, SettingToggle.Which.Haptics, "VIBRATION", -296);
        SettingRow(card, SettingToggle.Which.Fps, "FRAME COUNTER", -408);

        var back = Button("Back", card, "BACK", Slate, Paper, 34,
                          new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(300, 100));
        Wire(back, nameof(ScreenFlow.ShowTitle));
        return screen;
    }

    static void SettingRow(RectTransform card, SettingToggle.Which which, string caption, float y)
    {
        var rt = Rect(caption, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(1040, 96));
        rt.pivot = new Vector2(0.5f, 1);
        Design.AsRow(rt, Design.Ink700);
        var button = rt.gameObject.AddComponent<Button>();
        rt.gameObject.AddComponent<PressFeel>();
        var label = Label(caption + "Label", rt, body, caption, 42, Paper, TextAlignmentOptions.Left,
                          new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(960, 60));
        label.rectTransform.pivot = new Vector2(0, 0.5f);

        var toggle = rt.gameObject.AddComponent<SettingToggle>();
        var so = new SerializedObject(toggle);
        so.FindProperty("setting").enumValueIndex = (int)which;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("caption").stringValue = caption;
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, toggle.Toggle);
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
        var feeChip = Chip("FeeChip", card, "FREE RACE", new Vector2(lx, -284), out var feeLabel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(feeChip.onClick, panel.NextFee);
        var visChip = Chip("VisibilityChip", card, "PUBLIC  ·  ANYONE CAN JOIN", new Vector2(lx, -384), out var visLabel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(visChip.onClick, panel.ToggleVisibility);
        Label("FeeHint", card, body, "Tap any to change · prizes are paid on-chain", 26, Muted, TextAlignmentOptions.Center,
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
            so.FindProperty("feeLabel").objectReferenceValue = feeLabel;
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

    /// The lobby: code, who is in and paid, the pool and the prizes, and one button that is always the next step.
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

        var status = Label("Status", card, body, "", 30, Muted, TextAlignmentOptions.Center,
                           new Vector2(0.5f, 0), new Vector2(0, 198), new Vector2(1300, 44));

        var panel = screen.AddComponent<LobbyPanel>();
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
        new SerializedObject(swipe).Do(s => s.FindProperty("carousel").objectReferenceValue = garage);

        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 112); card.offsetMax = new Vector2(744, -152);
        Design.AsCard(card, Panel);

        var nameLabel = Label("Name", card, display, "PHOTON", 96, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -44), new Vector2(624, 100));
        nameLabel.enableAutoSizing = true; nameLabel.fontSizeMin = 60; nameLabel.fontSizeMax = 96;
        var blurb = Label("Blurb", card, body, "", 30, Muted, TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(48, -136), new Vector2(608, 60));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        // the five powers; their total is the price (KartPricing), and a locked kart shows what it would cost
        var powerBars = new StatBar[5];
        var powerNames = new[] { "SPEED", "TURBO", "GRIP", "ARMOR", "ATTACK" };
        for (int i = 0; i < 5; i++) powerBars[i] = StatRow(card, powerNames[i], -200 - i * 44, size: 26);
        var price = Label("Price", card, body, "", 30, Paper, TextAlignmentOptions.Left,
                          new Vector2(0, 1), new Vector2(48, -428), new Vector2(624, 36));

        // paint swatches: the driver's own colour, then the four paints
        var swatchCap = Label("PaintCap", card, body, "PAINT", 26, Muted, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(48, -474), new Vector2(200, 36));
        swatchCap.characterSpacing = 6f;
        var swatches = new Image[1 + Paints.All.Length];
        for (int i = 0; i < swatches.Length; i++)
        {
            var rt = Rect("Swatch" + i, card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(180 + i * 76, -466), new Vector2(56, 56));
            rt.pivot = new Vector2(0, 1);
            swatches[i] = Design.AsPill(rt, Color.white);
            var b = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(b.onClick, garage.Swatch, i);
        }

        var status = Label("Status", card, body, "", 24, Muted, TextAlignmentOptions.TopLeft,
                           new Vector2(0, 1), new Vector2(48, -522), new Vector2(608, 28));   // one short line above TEST DRIVE

        var secondary = Button("Secondary", card, "TEST DRIVE", Slate, Paper, 36,
                               new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(560, 92));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(secondary.onClick, garage.Secondary);
        var primary = Button("Primary", card, "UNLOCK", Accent, Paper, 44,
                             new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(560, 108));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(primary.onClick, garage.Primary);

        // the shop's other tab
        var itemsTab = Button("ItemsTab", screen.transform, "ITEMS  >", Slate, Paper, 32,
                              new Vector2(1, 1), new Vector2(-64, -36), new Vector2(240, 88));
        itemsTab.GetComponent<RectTransform>().pivot = new Vector2(1, 1);
        Wire(itemsTab, nameof(ScreenFlow.ShowItems));

        var back = Button("Back", screen.transform, "BACK", Slate, Paper, 32,
                          new Vector2(0, 1), new Vector2(64, -36), new Vector2(200, 88));
        back.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        Wire(back, nameof(ScreenFlow.ShowTitle));

        var prevArrow = IconButton("Prev", screen.transform, "arrow_basic_w", new Vector2(1, 0), new Vector2(-336, 72), new Vector2(144, 144));
        var nextArrow = IconButton("Next", screen.transform, "arrow_basic_e", new Vector2(1, 0), new Vector2(-128, 72), new Vector2(144, 144));
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
        Fill(so.FindProperty("swatches"), swatches.Cast<Object>().ToArray());
        Fill(so.FindProperty("powerBars"), powerBars.Cast<Object>().ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
        return screen;
    }

    /// The shop's items tab: five packs of race consumables, how many of each you hold, and the one you start with.
    static GameObject ItemsScreen(RectTransform parent)
    {
        var screen = Rect("Items", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var panel = screen.AddComponent<ItemsPanel>();
        var card = Rect("Card", screen.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        card.pivot = new Vector2(0, 0.5f);
        card.offsetMin = new Vector2(64, 112); card.offsetMax = new Vector2(744, -152);
        Design.AsCard(card, Panel);

        Label("Name", card, display, "ITEMS", 96, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(48, -40), new Vector2(624, 96));
        var blurb = Label("Blurb", card, body, "", 26, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(48, -142), new Vector2(608, 56));
        blurb.textWrappingMode = TextWrappingModes.Normal;
        var loadout = Chip("Loadout", card, "START WITH:  NOTHING", new Vector2(0, -198), out var loadoutLabel);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(loadout.onClick, panel.CycleLoadout);

        var packs = Inventory.Packs;
        var rows = new Button[packs.Length]; var rowLabels = new TMP_Text[packs.Length]; var rowPrices = new TMP_Text[packs.Length];
        for (int i = 0; i < packs.Length; i++)
        {
            var rt = Rect("Row" + i, card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -290 - i * 42), new Vector2(600, 40));   // six packs clear the PAY button
            rt.pivot = new Vector2(0, 1);
            Design.AsRow(rt, Design.Ink700);
            rows[i] = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(rows[i].onClick, panel.Focus, i);
            rowLabels[i] = Label("Label", rt, strong, packs[i].name.ToUpperInvariant(), 26, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(400, 40));
            rowPrices[i] = Label("Price", rt, body, "", 24, Muted, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(180, 40));
        }

        var secondary = Button("Secondary", card, "PAY", Slate, Paper, 36, new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(560, 92));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(secondary.onClick, panel.Secondary);
        var primary = Button("Primary", card, "UNLOCK", Accent, Paper, 44, new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(560, 108));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(primary.onClick, panel.Primary);

        var back = Button("Back", screen.transform, "BACK", Slate, Paper, 32, new Vector2(0, 1), new Vector2(64, -36), new Vector2(200, 88));
        back.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        Wire(back, nameof(ScreenFlow.ShowTitle));
        var kartsTab = Button("KartsTab", screen.transform, "<  KARTS", Slate, Paper, 32, new Vector2(1, 1), new Vector2(-64, -36), new Vector2(240, 88));
        kartsTab.GetComponent<RectTransform>().pivot = new Vector2(1, 1);
        Wire(kartsTab, nameof(ScreenFlow.ShowGarage));

        var so = new SerializedObject(panel);
        Fill(so.FindProperty("rows"), rows.Cast<Object>().ToArray());
        Fill(so.FindProperty("rowLabels"), rowLabels.Cast<Object>().ToArray());
        Fill(so.FindProperty("rowPrices"), rowPrices.Cast<Object>().ToArray());
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("loadoutLabel").objectReferenceValue = loadoutLabel;
        so.FindProperty("primaryLabel").objectReferenceValue = primary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("secondaryLabel").objectReferenceValue = secondary.GetComponentInChildren<TMP_Text>();
        so.FindProperty("primaryButton").objectReferenceValue = primary;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary;
        so.FindProperty("rowIdle").colorValue = Design.Ink700;
        so.FindProperty("rowFocus").colorValue = Design.Ink600;
        so.ApplyModifiedPropertiesWithoutUndo();
        return screen;
    }

    /// A row-shaped chooser: tap cycles its value. One label per intent, no dropdown widget.
    static Button Chip(string name, RectTransform card, string caption, Vector2 offset, out TMP_Text label)
    {
        var rt = Rect(name, card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), offset, new Vector2(520, 92));
        rt.pivot = new Vector2(0.5f, 1);
        Design.AsRow(rt, Design.Ink700);
        var button = rt.gameObject.AddComponent<Button>();
        rt.gameObject.AddComponent<PressFeel>();
        label = Label(name + "Label", rt, strong, caption, 36, Paper, TextAlignmentOptions.Center,
                      new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 60));
        label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = 36;
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
    static StatBar StatRow(RectTransform card, string label, float y, int pips = 5, float size = 32f)
    {
        var text = Label(label + "Label", card, body, label, size, Muted, TextAlignmentOptions.Left,
                         new Vector2(0, 1), new Vector2(48, y), new Vector2(240, 48));
        text.characterSpacing = 6f;
        var row = Rect(label + "Bar", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, y - 6), new Vector2(380, 30));
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

    /// Every action in the game is a pill. Nothing else is.
    static Button Button(string name, Transform parent, string caption, Color tint, Color textColour,
                         float textSize, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        Design.AsPill(rt, tint);
        var button = rt.gameObject.AddComponent<Button>();
        rt.gameObject.AddComponent<PressFeel>();
        var colours = button.colors;
        colours.highlightedColor = Color.white;
        colours.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        var label = Label(name + "Label", rt, display, caption, textSize, tint == Design.Accent ? Design.OnAccent : textColour, TextAlignmentOptions.Center,
                          new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.enableAutoSizing = true;
        label.fontSizeMin = textSize * 0.65f;
        label.fontSizeMax = textSize;
        label.rectTransform.offsetMin = new Vector2(28, 4);
        label.rectTransform.offsetMax = new Vector2(-28, -8);
        return button;
    }

    static Button IconButton(string name, Transform parent, string icon, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, size);
        rt.pivot = anchor;
        Design.AsPill(rt, Slate);
        var button = rt.gameObject.AddComponent<Button>();
        rt.gameObject.AddComponent<PressFeel>();
        var glyph = Rect(name + "Icon", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.42f);
        var img = glyph.gameObject.AddComponent<Image>();
        img.sprite = Sprite(icon) ?? AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Icons/{icon}.png");
        img.color = Paper;
        img.raycastTarget = false;
        return button;
    }

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
