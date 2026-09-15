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
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race.unity", OpenSceneMode.Single);
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
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.8f, 0.84f, 0.88f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50, -35, 0); sun.shadows = LightShadows.None;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 4;
        ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Road.mat");

        Preview.Batcher(false);
        var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Karts/Kart_oodi.prefab"));
        var driver = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Drivers/Driver_Nodey.prefab");
        var seat = kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
        if (driver != null && seat != null)
        {
            var d = (GameObject)PrefabUtility.InstantiatePrefab(driver, seat);
            d.transform.localPosition = Vector3.zero; d.transform.localRotation = Quaternion.identity;
        }

        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.72f, 0.86f, 0.92f);
        cam.fieldOfView = 34;
        Shot(cam, new Vector3(9.5f, 1.9f, 0), new Vector3(0, 1.0f, 0), "kart-side");
        Shot(cam, new Vector3(6.5f, 3.4f, -6.5f), new Vector3(0, 0.9f, 0), "kart-3q");
        Preview.Batcher(true);
        Debug.Log("[preview] wrote Builds/kart-*.png");
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
        var canvas = Object.FindFirstObjectByType<Canvas>();
        var cam = Camera.main;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;       // Overlay never lands in a RenderTexture
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;

        var carousel = Object.FindFirstObjectByType<CharacterCarousel>();
        if (carousel != null) carousel.ShowInEditor(0);      // Start() never runs in edit mode

        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        var panels = new SerializedObject(flow).FindProperty("panels");
        for (int i = 0; i < panels.arraySize; i++)
        {
            for (int j = 0; j < panels.arraySize; j++)
                ((GameObject)panels.GetArrayElementAtIndex(j).objectReferenceValue).SetActive(i == j);
            Canvas.ForceUpdateCanvases();
            Shot(cam, $"menu{i}");
        }
        Preview.Batcher(true);
        Debug.Log("[preview] wrote Builds/menu*.png");
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
