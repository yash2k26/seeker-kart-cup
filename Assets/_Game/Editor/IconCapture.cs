using System.IO;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

// The app icon's hero: one driver in one kart, low front three-quarter, square, rendered on black AND on white so
// tools/brand/make_icon.py can pull a clean matte (alpha = 1 - (white - black)) without trusting the pipeline's alpha.
//     tools/unity.sh IconCapture.Capture [-driver Toly] [-kart oodi] [-paint FF5A2A] [-size 2048] [-cam x,y,z] [-look x,y,z] [-tag -name]
//     -> Builds/icon-<shot>-black.png / -white.png for shots "a" (front-left) and "b" (front-right)
public static class IconCapture
{
    [MenuItem("Build/Capture Icon Hero")]
    public static void Capture()
    {
        string driver = Arg("-driver", "Toly"), kartId = Arg("-kart", "oodi"), paint = Arg("-paint", "FF5A2A"), tag = Arg("-tag", "");
        int size = int.Parse(Arg("-size", "2048"));

        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Sky_beach.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Sky.mat");
        if (sky != null) { RenderSettings.skybox = sky; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox; RenderSettings.defaultReflectionResolution = 128; DynamicGI.UpdateEnvironment(); }
        RenderSettings.ambientIntensity = 1.5f;
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(42, -30, 0); sun.shadows = LightShadows.Soft; sun.intensity = 1.8f; sun.color = new Color(1f, 0.95f, 0.86f);
        var fill = new GameObject("Fill").AddComponent<Light>();    // from the camera's side, so the kart's flanks are not a black mass
        fill.type = LightType.Directional; fill.transform.rotation = Quaternion.Euler(18, 200, 0); fill.intensity = 0.9f; fill.color = new Color(0.85f, 0.9f, 1f); fill.shadows = LightShadows.None;
        var rim = new GameObject("Rim").AddComponent<Light>();      // warm edge from behind-right: the sunburst's light on the hero
        rim.type = LightType.Directional; rim.transform.rotation = Quaternion.Euler(20, 150, 0); rim.intensity = 1.1f; rim.color = new Color(1f, 0.72f, 0.3f); rim.shadows = LightShadows.None;

        Preview.Batcher(false);
        var kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Karts/Kart_{kartId}.prefab"));
        if (ColorUtility.TryParseHtmlString("#" + paint, out var colour)) CharacterDef.Paint(kart, colour);
        var seat = kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
        var d = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Drivers/Driver_{driver}.prefab"), seat);
        d.transform.localPosition = Vector3.zero; d.transform.localRotation = Quaternion.identity;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Game/Prefabs/Drivers/{driver}_drive.anim");
        if (clip != null) clip.SampleAnimation(d, 0f);
        foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
        // the EyeLook windows idle at a blank stare in edit mode; aim them at the camera side like the films' hero frames
        foreach (var eye in d.GetComponentsInChildren<EyeLook>()) eye.enabled = false;

        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.fieldOfView = 44; cam.nearClipPlane = 0.1f; cam.allowHDR = true;
        // the kart faces +Z: close in on the head from the front-left, a touch below it, so the kart foreshortens under
        // the face the way Beach Buggy's buggy does; "b" is the mirror
        var from = Vec(Arg("-cam", "-0.6,1.95,4.05")); var at = Vec(Arg("-look", "0.12,1.38,0.1"));   // the shipped icon's angle   // tall karts seat the head higher: -cam/-look move it
        Shot(cam, size, from, at, "a" + tag);
        Shot(cam, size, new Vector3(-from.x, from.y, from.z), at, "b" + tag);
        Preview.Batcher(true);
        Debug.Log($"[icon] wrote Builds/icon-a|b-black|white.png ({driver} in {kartId}, paint #{paint}, {size}px)");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Shot(Camera cam, int size, Vector3 from, Vector3 lookAt, string name)
    {
        cam.transform.position = from; cam.transform.LookAt(lookAt);
        foreach (var (bg, tag) in new[] { (Color.black, "black"), (Color.white, "white") })
        {
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            Directory.CreateDirectory("Builds");
            File.WriteAllBytes($"Builds/icon-{name}-{tag}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
        }
    }

    static Vector3 Vec(string csv) { var p = csv.Split(','); return new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])); }

    static string Arg(string key, string dflt)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return dflt;
    }
}
