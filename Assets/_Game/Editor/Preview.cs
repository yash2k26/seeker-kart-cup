using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders frames of a scene to PNGs without a device (batchmode WITH graphics: no -nographics).
// Run: Unity -batchmode -projectPath . -executeMethod Preview.Capture -quit   -> Builds/preview-*.png
public static class Preview
{
    [MenuItem("Build/Capture Preview")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race.unity", OpenSceneMode.Single);
        var cam = Camera.main;
        var grid = GameObject.Find("StartGrid");
        var slot = grid != null ? grid.transform.GetChild(0) : null;

        if (slot != null)
        {
            Shot(cam, slot.position - slot.forward * 9f + Vector3.up * 4f, slot.position + Vector3.up * 1f, "grid");
            Shot(cam, slot.position + slot.forward * 60f + Vector3.up * 26f, slot.position + slot.forward * 120f, "ahead");
        }
        Shot(cam, new Vector3(0, 430, -30), Vector3.zero, "map");
        Debug.Log("[preview] wrote Builds/preview-*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
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

        var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Karts/Kart_oodi.prefab"));
        var driver = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Drivers/Driver_NodeRunner.prefab");
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
        Debug.Log("[preview] wrote Builds/kart-*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
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
