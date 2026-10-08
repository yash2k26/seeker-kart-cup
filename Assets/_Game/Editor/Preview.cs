using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KartCup;

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
        foreach (var c in Object.FindObjectsByType<ChestButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.ShowInEditor();
        foreach (var t in Object.FindObjectsByType<TitleToast>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.ShowInEditor("mert.skr beat your Palm Bay lap by 0.42 s", "RACE");

        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        var panels = new SerializedObject(flow).FindProperty("panels");
        for (int i = 0; i < panels.arraySize; i++)
        {
            for (int j = 0; j < panels.arraySize; j++)
                ((GameObject)panels.GetArrayElementAtIndex(j).objectReferenceValue).SetActive(i == j);
            if (backdrop != null) backdrop.Snap(i == 1 || i == 7 || i == 8, close: i == 1);     // LateUpdate never runs in edit mode either; the driver screen frames close
            // OnEnable does not run in edit mode: hand the sample/preview content to the panels that need it.
            if (i == 6) foreach (var lobby in Object.FindObjectsByType<LobbyPanel>(FindObjectsSortMode.None)) lobby.RefreshSample();
            if (i == 7) foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(KartSetup.Karts.Length - 1);
            if (i == 7 && backdrop != null) backdrop.Snap(true);   // the framing GaragePanel just fitted to its kart
            if (i == 8) foreach (var shop in Object.FindObjectsByType<ItemsPanel>(FindObjectsSortMode.None)) shop.ShowInEditor(0);
            if (i == 9) foreach (var weekly in Object.FindObjectsByType<WeeklyPanel>(FindObjectsSortMode.None)) weekly.ShowInEditor();
            if (i == 10) foreach (var tour in Object.FindObjectsByType<TourPanel>(FindObjectsSortMode.None)) tour.ShowInEditor(1);
            if (i == 11) foreach (var a in Object.FindObjectsByType<AchievementsPanel>(FindObjectsSortMode.None)) a.ShowInEditor();
            if (i != 7 && carousel != null) carousel.SetVisible(true);
            foreach (var img in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
                if (img.name == "Single" || img.name == "Cell0")
                    Debug.Log($"[preview] panel {i} {img.name}: active={img.isActiveAndEnabled} sprite={(img.sprite ? img.sprite.name : "null")} " +
                              $"tex={(img.sprite && img.sprite.texture ? img.sprite.texture.width + "x" + img.sprite.texture.height : "none")} rect={(img.sprite ? img.sprite.rect.ToString() : "-")} " +
                              $"alpha={string.Join("/", img.GetComponentsInParent<CanvasGroup>(true).Select(g => g.alpha))} colour={img.color} size={img.rectTransform.rect.size}");
            Canvas.ForceUpdateCanvases();
            Shot(cam, $"menu{i}");
            if (i == 7)   // and the gold, earned for the free kart: Beach Buggy's 24k skin
            {
                Unlocks.Earn("gold." + KartSetup.Karts[0]);
                foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(0, Paints.IndexOf("gold") + 1);
                if (backdrop != null) backdrop.Snap(true);
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu7-gold");
                foreach (var garage in Object.FindObjectsByType<GaragePanel>(FindObjectsSortMode.None)) garage.ShowInEditor(2, Paints.IndexOf("gold") + 1);
                Canvas.ForceUpdateCanvases(); Shot(cam, "menu7-gold-locked");
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
        Debug.Log("[preview] wrote Builds/menu*.png, Builds/meet*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Shot(Camera cam, string name)
    {
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
