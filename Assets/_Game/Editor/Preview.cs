using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KartCup;
using TMPro;

// Renders frames of a scene to PNGs without a device (batchmode WITH graphics: no -nographics).
// Run: Unity -batchmode -projectPath . -executeMethod Preview.Capture -quit   -> Builds/preview-*.png
public static class Preview
{
    [MenuItem("Build/Capture Preview")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race1.unity", OpenSceneMode.Single);
        Batcher(false);   // a manual cam.Render() in edit mode reuses the batcher's constants: every object comes out one colour
        var cam = Camera.main;
        if (System.Environment.GetCommandLineArgs().Contains("-nopost"))
            cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
        var grid = GameObject.Find("StartGrid");
        var slot = grid != null ? grid.transform.GetChild(0) : null;

        if (slot != null)
        {
            Shot(cam, slot.position - slot.forward * 9f + Vector3.up * 4f, slot.position + Vector3.up * 1f, "grid");
            Shot(cam, slot.position + Vector3.up * 30f - slot.forward * 30f, slot.position + slot.forward * 90f, "ahead");
        }
        Shot(cam, new Vector3(0, 430, -30), Vector3.zero, "map");

        // No per-circuit map shots here: re-opening scenes in edit mode and calling Camera.Render by hand rendered
        // the same geometry three times even though the scene data differed. Circuits are compared by driving them
        // (ContractTest) and by their checkpoint radii, never by an edit-mode render.
        Batcher(true);
        Debug.Log("[preview] wrote Builds/preview-*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// Toggles the SRP Batcher on the active pipeline asset. Only the preview path needs this — the player is fine.
    public static void Batcher(bool on)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (rp == null) return;
        var so = new SerializedObject(rp);
        var prop = so.FindProperty("m_UseSRPBatcher");
        if (prop == null) return;
        prop.boolValue = on;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Shot(Camera cam, Vector3 from, Vector3 lookAt, string name)
    {
        cam.transform.position = from;
        cam.transform.LookAt(lookAt);
        int w = 1780, h = 800;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/preview-{name}.png", tex.EncodeToPNG());
    }
}

// Side and three-quarter shots of a built kart, so the rig/art fit can be judged without a device.
public static class KartPreview
{
    [MenuItem("Build/Capture Kart")]
    public static void Capture()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        // The game's own sky, so glossy paint and metal reflect what they reflect on the phone, not a flat grey box.
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Sky_beach.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Sky.mat");
        if (sky != null) { RenderSettings.skybox = sky; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox; RenderSettings.defaultReflectionResolution = 128; DynamicGI.UpdateEnvironment(); }
        else { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.8f, 0.84f, 0.88f); }
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50, -35, 0); sun.shadows = LightShadows.Soft; sun.intensity = 1.1f; sun.color = new Color(1f, 0.96f, 0.9f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 4;
        ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Road.mat");

        Preview.Batcher(false);
        var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Karts/Kart_oodi.prefab"));
        var seat = kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");

        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.86f, 0.92f);
        cam.fieldOfView = 34;

        bool first = true;
        foreach (var path in System.IO.Directory.GetFiles("Assets/_Game/Prefabs/Drivers", "Driver_*.prefab").OrderBy(p => p))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path).Replace("Driver_", "");
            var d = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), seat);
            d.transform.localPosition = Vector3.zero; d.transform.localRotation = Quaternion.identity;
            // Edit mode does not run the Animation component, so without this every preview shows the standing bind
            // pose and hides exactly the seated-pose bugs these shots exist to catch.
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Game/Prefabs/Drivers/{name}_drive.anim");
            if (clip != null) clip.SampleAnimation(d, 0f);
            // SampleAnimation moves the bones but not the renderer's bounds, so the posed skin got culled (the
            // "limbs vanish" shots). Recomputing bounds per render keeps it in frame.
            foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

            Shot(cam, new Vector3(6.5f, 3.4f, -6.5f), new Vector3(0, 0.9f, 0), $"driver-{name}");
            Shot(cam, new Vector3(-2.5f, 3.2f, 8.5f), new Vector3(0, 0.9f, 0), $"driver-{name}-front");   // the kart faces +Z
            if (first) { Shot(cam, new Vector3(9.5f, 1.9f, 0), new Vector3(0, 1.0f, 0), "kart-side"); first = false; }
            Object.DestroyImmediate(d);
        }
        Object.DestroyImmediate(kart);

        // Every kart in the garage with one driver aboard: the seat height and driver scale of each vehicle are judged here.
        var driverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Drivers/Driver_Toly.prefab");
        foreach (var id in KartSetup.Karts)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Karts/Kart_{id}.prefab");
            if (prefab == null) continue;
            var k = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var s = k.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
            if (s != null && driverPrefab != null)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(driverPrefab, s);
                d.transform.localPosition = Vector3.zero; d.transform.localRotation = Quaternion.identity;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Game/Prefabs/Drivers/Toly_drive.anim");
                if (clip != null) clip.SampleAnimation(d, 0f);
                foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
            }
            Shot(cam, new Vector3(6.5f, 3.4f, -6.5f), new Vector3(0, 1.0f, 0), $"kart-{id}");
            Shot(cam, new Vector3(-6.5f, 3.4f, 6.5f), new Vector3(0, 1.0f, 0), $"kart-{id}-front");   // the kart faces +Z
            Object.DestroyImmediate(k);
        }
        Preview.Batcher(true);
        Debug.Log("[preview] wrote Builds/driver-*.png and Builds/kart-*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// Toggles the SRP Batcher on the active pipeline asset. Only the preview path needs this — the player is fine.
    public static void Batcher(bool on)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (rp == null) return;
        var so = new SerializedObject(rp);
        var prop = so.FindProperty("m_UseSRPBatcher");
        if (prop == null) return;
        prop.boolValue = on;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Shot(Camera cam, Vector3 from, Vector3 lookAt, string name)
    {
        cam.transform.position = from; cam.transform.LookAt(lookAt);
        int w = 1200, h = 700;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/{name}.png", tex.EncodeToPNG());
    }
}

// Screenshots of the menu scene's UI, so the front end can be judged without a device.
public static class MenuPreview
{
    /// The shop's buttons need a quote, and in edit mode there is no Worker: a sample config stands in. The prices are
    /// not the real ones, only shaped like them (SKR 6 decimals, SOL 9, ORE 11), so the capture shows every rail.
    static void SampleShopConfig()
    {
        var cfg = new KartApi.ConfigDto { treasuryOwner = "sample", treasuryAta = "sample", skrMint = "sample", oreMint = "sample", treasuryOreAta = "sample" };
        var karts = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
        var roster = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        var skus = (karts != null ? karts.karts.Where(k => k != null && !string.IsNullOrEmpty(k.sku)).Select(k => k.sku) : Enumerable.Empty<string>())
                   .Concat(Paints.All.Where(p => !string.IsNullOrEmpty(p.sku)).Select(p => p.sku)).Concat(Inventory.Packs.Select(p => p.sku))
                   .Concat(roster != null ? roster.characters.Where(c => c != null && !c.IsFree).Select(c => c.sku) : Enumerable.Empty<string>())
                   .Concat(GemsPanel.Packs.Select(p => p.sku));
        foreach (var sku in skus.Distinct())
        {
            bool skrOnly = sku == "paint.rally";
            cfg.catalog.Add(new KartApi.ShopItem { sku = sku, kind = sku.Split('.')[0], name = sku, skrOnly = skrOnly,
                price = new Dictionary<string, string> { ["SKR"] = "555000000", ["SOL"] = skrOnly ? "" : "50000000", ["ORE"] = skrOnly ? "" : "3100000000" } });
        }
        typeof(KartApi).GetProperty("Cfg").SetValue(null, cfg);
    }

    [MenuItem("Build/Capture Menu")]
    public static void Capture()
    {
        Preview.Batcher(false);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "UI");   // not the loading curtain's
        var cam = Camera.main;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;       // Overlay never lands in a RenderTexture
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;

        SampleShopConfig();
        var carousel = Object.FindFirstObjectByType<CharacterCarousel>();
        int character = 0;                                   // -char N: the select screen on that character
        var cargs = System.Environment.GetCommandLineArgs();
        for (int k = 0; k < cargs.Length - 1; k++) if (cargs[k] == "-char") int.TryParse(cargs[k + 1], out character);
        if (carousel != null) carousel.ShowInEditor(character);      // Start() never runs in edit mode
        foreach (var strip in Object.FindObjectsByType<RosterStrip>(FindObjectsSortMode.None)) strip.ShowInEditor(character);
        int trackCard = 0;                                   // -track N: the track select on that card (-1 the Grand Prix)
        for (int k = 0; k < cargs.Length - 1; k++) if (cargs[k] == "-track") int.TryParse(cargs[k + 1], out trackCard);
        foreach (var tracks in Object.FindObjectsByType<TrackCarousel>(FindObjectsSortMode.None)) tracks.ShowInEditor(trackCard);
        // nor does the drivers' seated clip: pose them as the phone will, and keep the posed skin from being culled
        foreach (var anim in Object.FindObjectsByType<Animation>(FindObjectsSortMode.None).Where(a => a.clip != null))
        {
            anim.clip.SampleAnimation(anim.gameObject, 0f);
            foreach (var smr in anim.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        }
        var backdrop = Object.FindFirstObjectByType<MenuBackdrop>();
        if (backdrop != null && backdrop.Meet != null) backdrop.Home = backdrop.Meet.Find("HomeStage");   // ScreenFlow.Awake does this at runtime: the title's camera in front of the home stage
        // nor does the hoarding's film: a still of it (Builds/teaser-frame.png, any frame of the teaser) on its render texture
        var film = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/_Game/Video/TeaserRT.renderTexture");
        if (film != null && File.Exists("Builds/teaser-frame.png"))
        {
            var still = new Texture2D(2, 2); still.LoadImage(File.ReadAllBytes("Builds/teaser-frame.png"));
            Graphics.Blit(still, film);
        }

        // Kartel Weekly and the chests from known samples: no Worker in edit mode, and the chests are this machine's
        var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Weekly.Preview(new Weekly.Board
        {
            week = 2961, track = "beach", nextTrack = "alpine", startsAt = now - 2 * 86400, endsAt = now + 4 * 86400 + 13 * 3600, now = now,
            pot = 2000, split = new List<int> { 50, 30, 20 }, minSeekers = 3, seekers = 5, entrants = 9,
            entries = new List<Weekly.Row>
            {
                new() { rank = 1, name = "mert.skr", wallet = "a", isSeeker = true, bestMs = 41230, prizePlace = 1 },
                new() { rank = 2, name = "toly.skr", wallet = "b", isSeeker = true, bestMs = 41870, prizePlace = 2 },
                new() { rank = 3, name = "7xKX…9fQa", wallet = "c", bestMs = 42010 },
                new() { rank = 4, name = "akshay.skr", wallet = "d", isSeeker = true, bestMs = 42550, prizePlace = 3 },
                new() { rank = 5, name = "you.skr", wallet = "me", isSeeker = true, bestMs = 43170 },
                new() { rank = 6, name = "chase.skr", wallet = "f", isSeeker = true, bestMs = 43800 },
                new() { rank = 7, name = "beeman.skr", wallet = "g", isSeeker = true, bestMs = 44920 },
            },
            me = new Weekly.Row { rank = 5, name = "you.skr", wallet = "me", isSeeker = true, bestMs = 43170, gapMs = 620, entrants = 9 },
        });
        Chests.Preview(12, Chests.Tier.Gold, Chests.Tier.Silver);
        Tour.Preview(("c1e1", 3), ("c1e2", 2), ("c1e3", 3), ("c1e4", 1), ("c1b", 3), ("c2e1", 2));
        Achievements.Preview();
        foreach (var d in Object.FindObjectsByType<DailyCard>(FindObjectsInactive.Include, FindObjectsSortMode.None)) d.ShowInEditor();
        foreach (var w in Object.FindObjectsByType<WeeklyCard>(FindObjectsInactive.Include, FindObjectsSortMode.None)) w.ShowInEditor();
        foreach (var l in Object.FindObjectsByType<LeaderboardFeed>(FindObjectsInactive.Include, FindObjectsSortMode.None)) l.ShowInEditor();
        foreach (var c in Object.FindObjectsByType<ChestButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.ShowInEditor();
        foreach (var t in Object.FindObjectsByType<TitleToast>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.ShowInEditor("mert.skr beat your Palm Bay lap by 0.42 s", "RACE");

        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        var panels = new SerializedObject(flow).FindProperty("panels");
        for (int i = 0; i < panels.arraySize; i++)
        {
            for (int j = 0; j < panels.arraySize; j++)
                ((GameObject)panels.GetArrayElementAtIndex(j).objectReferenceValue).SetActive(i == j);
            if (backdrop != null) backdrop.Home = i == 0 && backdrop.Meet != null ? backdrop.Meet.Find("HomeStage") : null;   // the hub looks at the stage; the front door runs the street
            if (backdrop != null) backdrop.Snap(i == 1 || i == 7 || i == 8, close: i == 1);     // LateUpdate never runs in edit mode either; the driver screen frames close
            // OnEnable does not run in edit mode: hand the sample/preview content to the panels that need it.
            if (i == 0) foreach (var h in Object.FindObjectsByType<HomeStage>(FindObjectsInactive.Include, FindObjectsSortMode.None)) h.ShowInEditor();
            if (i == 0) foreach (var d in Object.FindObjectsByType<HomeDoors>(FindObjectsInactive.Include, FindObjectsSortMode.None)) d.ShowInEditor();
            if (i == 1) foreach (var h in Object.FindObjectsByType<HomeStage>(FindObjectsInactive.Include, FindObjectsSortMode.None)) h.LeaveInEditor();   // the bays back for the select
            if (i == 6) foreach (var lobby in Object.FindObjectsByType<LobbyPanel>(FindObjectsSortMode.None)) lobby.RefreshSample();
            if (i == 7) foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(KartSetup.Karts.Length - 1);
            if (i == 7 && backdrop != null) backdrop.Snap(true);   // the framing GaragePanel just fitted to its kart
            if (i == 8) foreach (var shop in Object.FindObjectsByType<ItemsPanel>(FindObjectsSortMode.None)) shop.ShowInEditor(0);
            if (i == 9) foreach (var weekly in Object.FindObjectsByType<WeeklyPanel>(FindObjectsSortMode.None)) weekly.ShowInEditor();
            if (i == 10) foreach (var tour in Object.FindObjectsByType<TourPanel>(FindObjectsSortMode.None)) tour.ShowInEditor(1);
            if (i == 11) foreach (var a in Object.FindObjectsByType<AchievementsPanel>(FindObjectsSortMode.None)) a.ShowInEditor();
            if (i == 12) foreach (var g in Object.FindObjectsByType<GemsPanel>(FindObjectsSortMode.None)) g.ShowInEditor(1);
            foreach (var g in Object.FindObjectsByType<GemChip>(FindObjectsInactive.Include, FindObjectsSortMode.None)) g.ShowInEditor();
            if (i != 7 && carousel != null) carousel.SetVisible(true);
            foreach (var img in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
                if (img.name == "Single" || img.name == "Cell0")
                    Debug.Log($"[preview] panel {i} {img.name}: active={img.isActiveAndEnabled} sprite={(img.sprite ? img.sprite.name : "null")} " +
                              $"tex={(img.sprite && img.sprite.texture ? img.sprite.texture.width + "x" + img.sprite.texture.height : "none")} rect={(img.sprite ? img.sprite.rect.ToString() : "-")} " +
                              $"alpha={string.Join("/", img.GetComponentsInParent<CanvasGroup>(true).Select(g => g.alpha))} colour={img.color} size={img.rectTransform.rect.size}");
            Canvas.ForceUpdateCanvases();
            Shot(cam, $"menu{i}");
            if (i == 10)   // and the series cards the screen opens on
            {
                foreach (var tour in Object.FindObjectsByType<TourPanel>(FindObjectsSortMode.None)) tour.PickInEditor();
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu10-pick");
            }
            if (i == 7)   // and the gold, earned for the free kart: Beach Buggy's 24k skin
            {
                Unlocks.Earn("gold." + KartSetup.Karts[0]);
                foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(0, Paints.IndexOf("gold") + 1);
                if (backdrop != null) backdrop.Snap(true);
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu7-gold");
                foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(2, Paints.IndexOf("gold") + 1);
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu7-gold-locked");
                foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(1, 0);   // the PAINT view as it opens
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu7-paint");
                foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(1, Paints.IndexOf("bossmert") + 1);   // a Tour prize tried on
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu7-paint-prize");
            }
            if (i == 0)   // the fuel panel, a can down
            {
                Fuel.Preview(0, 130);
                foreach (var f in Object.FindObjectsByType<FuelChip>(FindObjectsSortMode.None)) f.ShowInEditor();
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu0-fuelchip");
                foreach (var fp in Object.FindObjectsByType<FuelPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) fp.ShowInEditor();
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu0-fuel");
                foreach (var fp in Object.FindObjectsByType<FuelPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) fp.HideInEditor();
                Fuel.Preview(Fuel.Max);
            }
            if (i == 0)   // the daily wheel, open on the gold chest
            {
                foreach (var w in Object.FindObjectsByType<SpinWheel>(FindObjectsSortMode.None)) w.ShowInEditor(7);
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu0-spin");
                foreach (var w in Object.FindObjectsByType<SpinWheel>(FindObjectsSortMode.None)) w.HideInEditor();
            }
            if (i == 10)   // the event card before a Tour race: a boss duel
            {
                foreach (var tour in Object.FindObjectsByType<TourPanel>(FindObjectsSortMode.None)) tour.CardInEditor(0, 4);
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu10-card");
                foreach (var tour in Object.FindObjectsByType<TourPanel>(FindObjectsSortMode.None)) tour.CardInEditor(1, 1);   // a race run the other way round
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu10-card-reverse");
                foreach (var tour in Object.FindObjectsByType<TourPanel>(FindObjectsSortMode.None)) tour.ShowInEditor(1);
            }
        }
        // The chest overlay over the title: a gold chest open, then the page that explains chests and its drop rates
        var reveal = Object.FindObjectsByType<ChestReveal>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (reveal != null)
        {
            for (int j = 0; j < panels.arraySize; j++) ((GameObject)panels.GetArrayElementAtIndex(j).objectReferenceValue).SetActive(j == 0);
            var rc = reveal.GetComponent<Canvas>();
            rc.renderMode = RenderMode.ScreenSpaceCamera; rc.worldCamera = cam; rc.planeDistance = 0.5f;
            Chests.Karts = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
            reveal.PreviewInEditor(Chests.Tier.Gold, Chests.PreviewRoll(Chests.Tier.Gold));
            Canvas.ForceUpdateCanvases(); Shot(cam, "chest-open");
            reveal.PreviewInEditor(Chests.Tier.Treasure, Chests.PreviewRoll(Chests.Tier.Treasure));
            Canvas.ForceUpdateCanvases(); Shot(cam, "chest-golden");
            reveal.PreviewInEditor(Chests.Tier.Wood, null);
            Canvas.ForceUpdateCanvases(); Shot(cam, "chest-info");
            reveal.ToggleRates();
            Canvas.ForceUpdateCanvases(); Shot(cam, "chest-rates");
            reveal.GetComponent<CanvasGroup>().alpha = 0f;
        }

        // The pre-race picker (a Resources prefab the race scenes load), rockets x3 and bombs x2 picked
        var pickerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Resources/CarryPicker.prefab");
        if (pickerPrefab != null)
        {
            var pk = (GameObject)PrefabUtility.InstantiatePrefab(pickerPrefab);
            var pc = pk.GetComponent<Canvas>(); pc.renderMode = RenderMode.ScreenSpaceCamera; pc.worldCamera = cam; pc.planeDistance = 0.45f;
            pk.GetComponent<CarryPicker>().PreviewInEditor((0, 3), (2, 2));
            Canvas.ForceUpdateCanvases(); Shot(cam, "carry-picker");
            Object.DestroyImmediate(pk);
        }

        // The purchase sheet over the garage: a kart on its way through the wallet, stamped, refused, still confirming;
        // a gem pack and a recruited judge stamped
        var sheet = Object.FindObjectsByType<PurchaseSheet>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (sheet != null)
        {
            for (int j = 0; j < panels.arraySize; j++) ((GameObject)panels.GetArrayElementAtIndex(j).objectReferenceValue).SetActive(j == 7);
            var sc = sheet.GetComponent<Canvas>();
            sc.renderMode = RenderMode.ScreenSpaceCamera; sc.worldCamera = cam; sc.planeDistance = 0.4f;
            var kart = KartSetup.Karts[^1];
            var judge = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset").characters.First(c => c != null && !string.IsNullOrEmpty(c.sku));
            void Buy(string name, string what, string key, string price, int state, string why = "", string done = "Yours to race, on every phone you sign in to.")
            { sheet.PreviewInEditor(what, key, price, state, why, done); Canvas.ForceUpdateCanvases(); Shot(cam, name); }
            Buy("buy-working", "Hatchback Sports", "kart:" + kart, "0.05 SOL", 0);
            Buy("buy-done", "Hatchback Sports", "kart:" + kart, "0.05 SOL", 1);
            Buy("buy-failed", "Hatchback Sports", "kart:" + kart, "0.05 SOL", 2, "Not enough SOL for this");
            Buy("buy-pending", "Hatchback Sports", "kart:" + kart, "0.05 SOL", 3);
            Buy("buy-gems", "BAG of 550 gems", "gems", "120 SKR", 1, "", "550 gems added to your balance.");
            Buy("buy-driver", judge.displayName, "driver:" + judge.driverId, "300 GEMS", 1, "", "Joins your roster, on every phone you sign in to.");
            sheet.GetComponent<CanvasGroup>().alpha = 0f;
        }

        // The title's street without the UI over it: the whole set, then each driver close, heads turned to the lens
        // as MeetActor would have them (it never runs in edit mode).
        canvas.gameObject.SetActive(false);
        if (backdrop != null) backdrop.Snap(false);
        var actors = Object.FindObjectsByType<MeetActor>(FindObjectsSortMode.None).OrderBy(a => a.name).ToArray();
        foreach (var a in actors) a.FaceInEditor(cam.transform);
        Shot(cam, "meet");
        var home = cam.transform.position;
        cam.transform.SetPositionAndRotation(home + new Vector3(0, 46f, 22f), Quaternion.Euler(90f, 0f, 0f));
        Shot(cam, "meet-top");
        for (int i = 0; i < actors.Length; i++)
        {
            var at = actors[i].transform.position + Vector3.up * 1.4f;
            cam.transform.position = at + (home - at).normalized * 10f + Vector3.up * 0.6f;
            cam.transform.LookAt(at);
            foreach (var a in actors) a.FaceInEditor(cam.transform);
            Shot(cam, $"meet-{i}");
        }
        Preview.Batcher(true);
        File.WriteAllLines("Builds/overflow.txt", overflow.Select(kv => kv.Value + "  " + kv.Key));
        Debug.Log($"[preview] wrote Builds/menu*.png, Builds/meet*.png; {overflow.Count} text problems in Builds/overflow.txt");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    internal static readonly SortedDictionary<string, string> overflow = new();

    /// Every visible text whose words leave its box, or run under an icon beside it, in this shot (12 Oct 2026, the
    /// user: "there are multiple places where text is overflowing"). Collected across shots into Builds/overflow.txt.
    internal static void Overflows(string shot)
    {
        var corners = new Vector3[4];
        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text) || t.color.a < 0.05f || t.canvas == null || !t.canvas.isActiveAndEnabled) continue;
            if (t.GetComponentsInParent<CanvasGroup>().Any(g => g.alpha < 0.05f)) continue;
            t.ForceMeshUpdate();
            var r = t.rectTransform.rect; var tb = t.textBounds;
            float dx = tb.size.x - r.width, dy = tb.size.y - r.height;
            string path = string.Join("/", t.GetComponentsInParent<Transform>().Reverse().Skip(1).Select(x => x.name));
            string what = t.text.Length > 48 ? t.text[..48] + "…" : t.text;
            if (dx > 4f || dy > 4f)
                overflow.TryAdd($"{path} | {what.Replace("\n", " / ")}", $"{shot}: over its box by {Mathf.Max(0, dx):0} x {Mathf.Max(0, dy):0} px");
            // an icon beside the text, under the same parent, that the words run into (a background holds the whole text: not counted)
            var tMin = t.rectTransform.TransformPoint(tb.min); var tMax = t.rectTransform.TransformPoint(tb.max);
            var textRect = Rect.MinMaxRect(Mathf.Min(tMin.x, tMax.x), Mathf.Min(tMin.y, tMax.y), Mathf.Max(tMin.x, tMax.x), Mathf.Max(tMin.y, tMax.y));
            if (t.transform.parent == null) continue;
            foreach (Transform sib in t.transform.parent)
            {
                if (sib == t.transform || !sib.gameObject.activeInHierarchy) continue;
                var img = sib.GetComponent<Image>();
                if (img == null || img.sprite == null || img.color.a < 0.1f || !img.enabled) continue;
                var sn = img.sprite.name.ToLowerInvariant();
                if (sn.Contains("glow") || sn.Contains("edge") || sn.Contains("rays") || sn.Contains("shine")) continue;   // light and outlines, not icons
                ((RectTransform)sib).GetWorldCorners(corners);
                var ir = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                if (img.preserveAspect)   // only where the picture draws inside its box
                {
                    float sa = img.sprite.rect.width / img.sprite.rect.height, ra = ir.width / ir.height;
                    ir = sa > ra ? new Rect(ir.x, ir.center.y - ir.width / sa / 2f, ir.width, ir.width / sa) : new Rect(ir.center.x - ir.height * sa / 2f, ir.y, ir.height * sa, ir.height);
                }
                if (ir.Contains(textRect.min) && ir.Contains(textRect.max)) continue;                 // a background
                float ox = Mathf.Min(ir.xMax, textRect.xMax) - Mathf.Max(ir.xMin, textRect.xMin);
                float oy = Mathf.Min(ir.yMax, textRect.yMax) - Mathf.Max(ir.yMin, textRect.yMin);
                float px = t.canvas.transform.lossyScale.x;                                            // world units to canvas pixels
                if (ox > 3f * px && oy > 3f * px)
                    overflow.TryAdd($"{path} | {what.Replace("\n", " / ")} | under {sib.name}", $"{shot}: runs into {sib.name} by {ox / px:0} px");
            }
        }
    }

    static void Shot(Camera cam, string name)
    {
        Overflows(name);
        int w = 2400, h = 1080;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/{name}.png", tex.EncodeToPNG());
    }
}
