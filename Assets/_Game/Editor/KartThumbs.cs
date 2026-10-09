using System.IO;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// A picture of every kart in the garage (Textures/UI/Karts/<id>.png): the title's KART door wears the chosen one
/// (Beach Buggy's Switch Car). A display kart alone, from the front-left and a little above, cut out by a black/white
/// matte so the alpha is exact whatever the pipeline writes.     tools/unity.sh KartThumbs.Build
public static class KartThumbs
{
    public const string Dir = "Assets/_Game/Art/Textures/UI/Karts/";

    [MenuItem("Build/Render Kart Thumbnails")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.72f, 0.76f, 0.86f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f); sun.intensity = 1.7f; sun.color = new Color(1f, 0.95f, 0.88f); sun.shadows = LightShadows.None;
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.fieldOfView = 26f; cam.allowHDR = false;
        Preview.Batcher(false);
        const int S = 512;
        foreach (var id in KartSetup.Karts)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Karts/Kart_{id}.prefab");
            if (prefab == null) continue;
            var slot = new GameObject("Slot").transform;
            var kart = CharacterCarousel.Mount(prefab, null, slot, Color.white, false);
            kart.transform.localRotation = Quaternion.Euler(0f, 212f, 0f);   // nose to the front-left, as on the home stage
            var b = new Bounds(); bool any = false;
            foreach (var r in kart.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.name != "BlobShadow"))
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            cam.transform.position = b.center + new Vector3(-0.95f, 0.7f, -1.3f).normalized * (b.extents.magnitude * 2.75f);
            cam.transform.LookAt(b.center);
            var black = Shot(cam, S, Color.black); var white = Shot(cam, S, Color.white);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Clamp01(1f - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f);
                px[i] = a > 0.004f ? new Color(Mathf.Clamp01(black[i].r / a), Mathf.Clamp01(black[i].g / a), Mathf.Clamp01(black[i].b / a), a) : Color.clear;
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false); tex.SetPixels(px); tex.Apply();
            string file = Dir + id + ".png";
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); Object.DestroyImmediate(slot.gameObject);
            AssetDatabase.ImportAsset(file);
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        Preview.Batcher(true);
        Debug.Log($"[karts] wrote {KartSetup.Karts.Length} thumbnails to {Dir}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Color[] Shot(Camera cam, int size, Color bg)
    {
        cam.backgroundColor = bg;
        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        var px = tex.GetPixels();
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
        return px;
    }
}
