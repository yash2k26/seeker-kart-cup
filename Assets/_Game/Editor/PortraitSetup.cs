using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Renders each driver's face to a small transparent PNG for the minimap marker (and anything else that wants a
// portrait). Front view, framed on the head, no body. Run: ./tools/unity.sh PortraitSetup.Build
public static class PortraitSetup
{
    public const string Dir = "Assets/_Game/Art/Textures/UI/Portraits/";

    [MenuItem("Build/Render Portraits")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.85f, 0.87f, 0.9f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(35, 160, 0); sun.shadows = LightShadows.None; sun.intensity = 1.1f;

        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographic = true; cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
        Preview.Batcher(false);

        int count = 0;
        foreach (var path in Directory.GetFiles("Assets/_Game/Prefabs/Drivers", "Driver_*.prefab").OrderBy(p => p))
        {
            var name = Path.GetFileNameWithoutExtension(path).Replace("Driver_", "");
            var d = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var head = d.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.gameObject.name == "Head");
            if (head == null) { Debug.LogWarning("[portrait] no head on " + name); Object.DestroyImmediate(d); continue; }
            // only the head and what hangs off it (cap, beard): hide the body so the frame is a face
            foreach (var r in d.GetComponentsInChildren<Renderer>(true))
                r.enabled = r.transform.IsChildOf(head.transform.parent) && !r.name.StartsWith("lid_");   // eyes open
            var b = new Bounds(head.bounds.center, Vector3.zero);
            foreach (var r in d.GetComponentsInChildren<Renderer>(true)) if (r.enabled) b.Encapsulate(r.bounds);
            // the driver faces +Z: camera ahead, looking back, framed a little loose
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.25f;
            cam.transform.position = b.center + Vector3.forward * 4f + Vector3.up * (b.extents.y * 0.05f);
            cam.transform.LookAt(b.center);

            int s = 128;
            var rt = new RenderTexture(s, s, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, s, s), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            var file = Dir + name + ".png";
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(d);
            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            count++;
        }
        Preview.Batcher(true);
        Debug.Log($"[portrait] rendered {count} faces to {Dir}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
