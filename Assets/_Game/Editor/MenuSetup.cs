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

// Builds Menu.unity: title, character select and leaderboard, over a live 3D vista with the kart you will race.
// Run: Unity -batchmode -projectPath . -executeMethod MenuSetup.Build -quit   (menu Build > Build Menu Scene)
public static class MenuSetup
{
    const string ScenePath = "Assets/_Game/Scenes/Menu.unity";
    const string Art = "Assets/_Game/Art/";
    const string Prefabs = "Assets/_Game/Prefabs/";
    const string FontDir = "Assets/_Game/UI/Fonts/";

    // One palette, used by every screen.
    static readonly Color Deep = new(0.043f, 0.141f, 0.188f);
    static readonly Color Panel = new(0.071f, 0.227f, 0.282f, 0.92f);
    static readonly Color Sand = new(0.957f, 0.773f, 0.404f);
    static readonly Color Coral = new(0.937f, 0.416f, 0.294f);
    static readonly Color Paper = new(0.965f, 0.945f, 0.894f);

    public static TMP_FontAsset display, body;

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

        var so = new SerializedObject(flow);
        var panels = so.FindProperty("panels");
        panels.arraySize = 3;
        panels.GetArrayElementAtIndex(0).objectReferenceValue = title;
        panels.GetArrayElementAtIndex(1).objectReferenceValue = select;
        panels.GetArrayElementAtIndex(2).objectReferenceValue = board;
        so.ApplyModifiedPropertiesWithoutUndo();

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var boot = new GameObject("Boot");
        boot.AddComponent<GameBoot>();
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
        display = MakeFont("Kenney Future", 90);
        body = MakeFont("Kenney Future Narrow", 76);
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
    static Transform Vista()
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sky.mat");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.30f, 0.37f, 0.46f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.26f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.17f, 0.13f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60; RenderSettings.fogEndDistance = 220;
        RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.92f);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(44, -40, 0);
        sun.shadows = LightShadows.Soft; sun.intensity = 1.05f;
        sun.color = new Color(1f, 0.96f, 0.88f); sun.shadowStrength = 0.7f;

        var sand = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sand.name = "Sand";
        sand.transform.localScale = new Vector3(14, 1, 14);
        sand.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sand.mat");

        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Water";
        water.transform.position = new Vector3(0, -0.6f, 0);
        water.transform.localScale = new Vector3(120, 1, 120);
        water.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water.mat");
        Object.DestroyImmediate(water.GetComponent<Collider>());

        var pirate = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Pirate.mat");
        var rnd = new System.Random(3);
        var palms = new[] { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
            float r = 26f + (float)rnd.NextDouble() * 40f;
            Place(Art + $"Models/Props/Pirate/{palms[rnd.Next(palms.Length)]}.fbx", pirate,
                  new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), (float)rnd.NextDouble() * 360f, 1.6f + (float)rnd.NextDouble() * 1.1f);
        }
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f + 0.4f;
            Place(Art + "Models/Props/Pirate/rocks-sand-b.fbx", pirate,
                  new Vector3(Mathf.Sin(a) * (34f + i * 2f), -0.3f, Mathf.Cos(a) * (34f + i * 2f)), i * 37f, 2.2f);
        }

        var podium = new GameObject("Podium").transform;
        podium.position = Vector3.zero;

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        // Park it on the orbit so the scene reads correctly when opened or screenshotted; MenuBackdrop takes over at runtime.
        camGo.transform.position = new Vector3(7.9f, 3.6f, -6.9f);
        camGo.transform.LookAt(new Vector3(0, 1.3f, 0));
        camGo.transform.rotation *= Quaternion.Euler(0, -13f, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 30; cam.nearClipPlane = 0.2f; cam.farClipPlane = 300; cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        var drift = camGo.AddComponent<MenuBackdrop>();
        new SerializedObject(drift).Do(s =>
        {
            s.FindProperty("target").objectReferenceValue = podium;
            s.FindProperty("radius").floatValue = 9.6f;
            s.FindProperty("height").floatValue = 3.9f;
            s.FindProperty("speed").floatValue = 5f;
            s.FindProperty("look").floatValue = 1.7f;
        });

        var volume = new GameObject("Post").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/PostProfile.asset");
        return podium;
    }

    // ---------- screens ----------
    static GameObject TitleScreen(RectTransform parent)
    {
        var screen = Rect("Title", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        var plate = Rect("Plate", screen.transform, new Vector2(0, 0), new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero);
        // three stacked slices fake a gradient: the kit has no gradient sprite and a hard edge looks pasted on
        foreach (var (width, alpha) in new[] { (0.62f, 0.80f), (0.82f, 0.34f), (1f, 0.14f) })
        {
            var slice = Rect($"Fade{alpha}", plate, Vector2.zero, new Vector2(width, 1f), Vector2.zero, Vector2.zero);
            var img = slice.gameObject.AddComponent<Image>();
            img.color = new Color(Deep.r, Deep.g, Deep.b, alpha);
            img.raycastTarget = false;
        }

        Label("Kicker", plate, body, "BEACH GRAND PRIX", 44, Sand, TextAlignmentOptions.Left,
              new Vector2(0, 1), new Vector2(120, -190), new Vector2(1240, 70));
        Label("Title1", plate, display, "SEEKER", 190, Paper, TextAlignmentOptions.Left,
              new Vector2(0, 1), new Vector2(120, -240), new Vector2(1100, 200));
        Label("Title2", plate, display, "KART CUP", 190, Paper, TextAlignmentOptions.Left,
              new Vector2(0, 1), new Vector2(120, -410), new Vector2(1100, 200));
        Label("Sub", plate, body, "race the island. put your lap on-chain.", 40, new Color(1, 1, 1, 0.62f),
              TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(124, -610), new Vector2(1000, 60));

        var play = Button("Play", plate, "PLAY", "red_button_rectangle_depth_gradient", Coral, Paper, 64,
                          new Vector2(0, 0), new Vector2(120, 250), new Vector2(520, 150));
        Wire(play, nameof(ScreenFlow.ShowSelect));

        var boardBtn = Button("Board", plate, "LEADERBOARD", "blue_button_rectangle_depth_flat", new Color(0.29f, 0.52f, 0.62f), Paper, 38,
                              new Vector2(0, 0), new Vector2(120, 140), new Vector2(330, 90));
        Wire(boardBtn, nameof(ScreenFlow.ShowLeaderboard));

        var walletBtn = Button("Wallet", plate, "SIGN IN", "grey_button_rectangle_depth_flat", new Color(0.24f, 0.31f, 0.35f), Paper, 38,
                               new Vector2(0, 0), new Vector2(470, 140), new Vector2(240, 90));
        var wallet = new GameObject("WalletFlow").AddComponent<WalletFlow>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(walletBtn.onClick, wallet.Login);

        return screen;
    }

    static GameObject SelectScreen(RectTransform parent, Transform podium, CharacterCarousel carousel)
    {
        var screen = Rect("Select", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        var card = Rect("Card", screen.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(60, 0), new Vector2(760, -160));
        card.pivot = new Vector2(0, 0.5f);
        card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(0, 1);
        card.offsetMin = new Vector2(60, 110); card.offsetMax = new Vector2(760, -110);
        Sliced(card, "blue_button_rectangle_flat", new Color(Panel.r, Panel.g, Panel.b, 0.97f));

        var nameLabel = Label("Name", card, display, "NODEY", 96, Paper, TextAlignmentOptions.Left,
                              new Vector2(0, 1), new Vector2(56, -70), new Vector2(660, 110));
        var blurb = Label("Blurb", card, body, "", 36, new Color(1, 1, 1, 0.66f), TextAlignmentOptions.TopLeft,
                          new Vector2(0, 1), new Vector2(56, -180), new Vector2(640, 120));
        blurb.textWrappingMode = TextWrappingModes.Normal;

        var bars = new StatBar[4];
        var labels = new[] { "SPEED", "ACCEL", "GRIP", "WEIGHT" };
        for (int i = 0; i < 4; i++) bars[i] = StatRow(card, labels[i], -300 - i * 84);

        var race = Button("Race", card, "RACE", "red_button_rectangle_depth_gradient", Coral, Paper, 54,
                          new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(540, 120));
        Wire(race, nameof(ScreenFlow.StartRace));

        var back = Button("Back", screen.transform, "BACK", "grey_button_rectangle_depth_flat", new Color(0.24f, 0.31f, 0.35f), Paper, 34,
                          new Vector2(0, 1), new Vector2(60, -60), new Vector2(200, 84));
        back.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        Wire(back, nameof(ScreenFlow.ShowTitle));

        // both arrows hug the bottom right, clear of the kart and of the card
        var prev = IconButton("Prev", screen.transform, "arrow_basic_w", new Vector2(1, 0), new Vector2(-330, 70));
        var next = IconButton("Next", screen.transform, "arrow_basic_e", new Vector2(1, 0), new Vector2(-120, 70));

        var so = new SerializedObject(carousel);
        so.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        so.FindProperty("podium").objectReferenceValue = podium;
        Fill(so.FindProperty("kartPrefabs"), KartSetup.Karts.Select(k => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{k}.prefab")).ToArray());
        Fill(so.FindProperty("driverPrefabs"), Directory.GetFiles(Prefabs + "Drivers", "Driver_*.prefab").Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray());
        so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        so.FindProperty("blurbLabel").objectReferenceValue = blurb;
        so.FindProperty("speedBar").objectReferenceValue = bars[0];
        so.FindProperty("accelBar").objectReferenceValue = bars[1];
        so.FindProperty("handlingBar").objectReferenceValue = bars[2];
        so.FindProperty("weightBar").objectReferenceValue = bars[3];
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.Events.UnityEventTools.AddPersistentListener(prev.onClick, carousel.Previous);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(next.onClick, carousel.Next);
        return screen;
    }

    static GameObject BoardScreen(RectTransform parent)
    {
        var screen = Rect("Board", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var card = Rect("Card", screen.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 860));
        Sliced(card, "blue_button_rectangle_flat", Panel);
        Label("Head", card, display, "FASTEST LAPS", 72, Sand, TextAlignmentOptions.Center,
              new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1000, 90));
        var rows = Label("Rows", card, body, "loading…", 40, Paper, TextAlignmentOptions.TopLeft,
                         new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(1000, 560));
        var back = Button("Back", card, "BACK", "grey_button_rectangle_depth_flat", new Color(0.24f, 0.31f, 0.35f), Paper, 36,
                          new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(300, 100));
        Wire(back, nameof(ScreenFlow.ShowTitle));

        var feed = screen.AddComponent<LeaderboardFeed>();
        new SerializedObject(feed).Do(s => s.FindProperty("rows").objectReferenceValue = rows);
        return screen;
    }

    // ---------- builders ----------
    static StatBar StatRow(RectTransform card, string label, float y)
    {
        Label(label + "Label", card, body, label, 34, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Left,
              new Vector2(0, 1), new Vector2(56, y), new Vector2(240, 50));
        var row = Rect(label + "Bar", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, y - 8), new Vector2(380, 34));
        row.pivot = new Vector2(0, 1);
        var bar = row.gameObject.AddComponent<StatBar>();
        var pips = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var pip = Rect("Pip" + i, row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * 74 + 28, 0), new Vector2(60, 26));
            var img = pip.gameObject.AddComponent<Image>();
            img.sprite = Sprite("grey_button_rectangle_flat");
            img.type = Image.Type.Sliced;
            pips[i] = img;
        }
        var so = new SerializedObject(bar);
        Fill(so.FindProperty("pips"), pips.Cast<Object>().ToArray());
        so.FindProperty("on").colorValue = Sand;
        so.FindProperty("off").colorValue = new Color(1, 1, 1, 0.16f);
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

    static Button Button(string name, Transform parent, string caption, string sprite, Color tint, Color textColour,
                         float textSize, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        Sliced(rt, sprite, tint);
        var button = rt.gameObject.AddComponent<Button>();
        var colours = button.colors;
        colours.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colours.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        var label = Label(name + "Label", rt, display, caption, textSize, textColour, TextAlignmentOptions.Center,
                          new Vector2(0.5f, 0.5f), Vector2.zero, dims);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(0, 6);
        label.rectTransform.offsetMax = new Vector2(0, -2);
        return button;
    }

    static Button IconButton(string name, Transform parent, string icon, Vector2 anchor, Vector2 offset)
    {
        var rt = Rect(name, parent, anchor, anchor, offset, new Vector2(150, 150));
        rt.pivot = anchor;
        Sliced(rt, "yellow_button_square_depth_flat", Sand);
        var button = rt.gameObject.AddComponent<Button>();
        var glyph = Rect(name + "Icon", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(64, 64));
        var img = glyph.gameObject.AddComponent<Image>();
        img.sprite = Sprite(icon);
        img.color = Deep;
        img.raycastTarget = false;
        return button;
    }

    static Image Sliced(RectTransform rt, string sprite, Color tint)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = Sprite(sprite);
        img.type = Image.Type.Sliced;
        img.color = tint;
        img.pixelsPerUnitMultiplier = 0.55f;      // Kenney's corners are chunky: let them breathe at 2400-wide
        return img;
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
