using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders a frame of the Gate scene to a PNG without a device (batchmode WITH graphics: no -nographics).
// Run: Unity -batchmode -projectPath . -executeMethod Preview.Capture -quit   → Builds/preview-gate.png
public static class Preview
{
    [MenuItem("Build/Capture Gate Preview")]
    public static void Capture()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Gate.unity", OpenSceneMode.Single);
        var cam = Camera.main;
        var kart = GameObject.Find("PlayerKart");
        if (kart) { cam.transform.position = kart.transform.position - kart.transform.forward * 6.5f + Vector3.up * 2.6f; cam.transform.LookAt(kart.transform.position + Vector3.up * 0.8f); }
        int w = 2670, h = 1200;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes("Builds/preview-gate.png", tex.EncodeToPNG());
        Debug.Log("[preview] wrote Builds/preview-gate.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
