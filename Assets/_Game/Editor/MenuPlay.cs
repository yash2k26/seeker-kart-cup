using System.IO;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The menu in play mode, for the things an edit-mode capture cannot show (MenuPreview): the drivers on the title's
// street animating and following the camera, the film running on the hoarding, the camera's move to the turntable.
//     tools/unity.sh MenuPlay.Run        -> Builds/menuplay-*.png and a [menuplay] line per check
// The UI is a screen-space overlay and is not in these frames; MenuPreview shows it.
public static class MenuPlay
{
    static double start;
    static int stage;

    [MenuItem("Build/Menu Play Test")]
    public static void Run()
    {
        EditorUtility.audioMasterMute = true;   // a headless test run must not play the game's sound through the laptop
        if (!EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.DisableDomainReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
        }
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup; stage = 0;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        AudioListener.volume = 0f;
        double t = EditorApplication.timeSinceStartup - start;
        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        // the title at three moments (the dolly has moved, the drivers have fidgeted), then the move to the turntable
        (double at, System.Action act)[] steps =
        {
            (5, () => Report("title-a")), (9, () => Report("title-b")), (14, () => Report("title-c")),
            (14.2, () => flow.Play()), (14.55, () => Shot("fly")), (16.5, () => Shot("select")),
            (16.7, () => flow.ShowTitle()), (18.5, () => Shot("back")),
        };
        if (stage < steps.Length) { if (t >= steps[stage].at) steps[stage++].act(); return; }
        EditorApplication.update -= Tick;
        Debug.Log("[menuplay] done");
        EditorApplication.Exit(0);
    }

    static void Report(string name)
    {
        var video = Object.FindFirstObjectByType<UnityEngine.Video.VideoPlayer>();
        Debug.Log($"[menuplay] {name}: film playing={video != null && video.isPlaying} frame={(video != null ? video.frame : -1)}");
        foreach (var a in Object.FindObjectsByType<MeetActor>(FindObjectsSortMode.None).OrderBy(a => a.name))
        {
            var anim = a.GetComponent<Animation>();
            var playing = anim.Cast<AnimationState>().Where(s => anim.IsPlaying(s.name)).Select(s => $"{s.name}@{s.time:F2}");
            var head = a.GetComponentsInChildren<Transform>().First(x => x.name == "head");
            float off = Vector3.Angle(a.transform.forward, head.rotation * Quaternion.Inverse(a.transform.rotation) * a.transform.forward);
            Debug.Log($"[menuplay]   {a.name}: {string.Join(" + ", playing)}, head turned {off:F0} deg, eyes {(a.GetComponentInChildren<EyeLook>() is { enabled: true } ? "live" : "off")}");
        }
        Shot(name);
    }

    static void Shot(string name)
    {
        var cam = Camera.main;
        int w = 2400, h = 1080;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = prevTarget;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/menuplay-{name}.png", tex.EncodeToPNG());
        Object.Destroy(tex); rt.Release();
    }
}
