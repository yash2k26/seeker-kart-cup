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

    // Just turn the game on: the menu in play mode, sound on, nothing scripted.
    //     Unity -projectPath . -executeMethod MenuPlay.Play      (GUI editor, no -batchmode)
    [MenuItem("Build/Play Menu")]
    public static void Play()
    {
        EditorUtility.audioMasterMute = false;   // MenuPlay.Run leaves the editor muted; this is for a person
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity", OpenSceneMode.Single);
        Debug.Log("[play] entering play mode on Menu");
        EditorApplication.EnterPlaymode();
    }

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
        Voices.Spoke -= Said; Voices.Spoke += Said;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        AudioListener.volume = 0f;
        double t = EditorApplication.timeSinceStartup - start;
        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        // the title at three moments (the dolly has moved, the drivers have fidgeted), then the move to the turntable,
        // then on through the track select to a race: the loading curtain over the switch, and the race behind it
        (double at, System.Action act)[] steps =
        {
            (5, () => Report("title-a")), (9, () => Report("title-b")), (14, () => Report("title-c")),
            (14.2, () => flow.Play()), (14.55, () => Shot("fly")), (15.3, () => Shot("hello", ui: true)), (16.5, () => Shot("select", ui: true)),
            (16.55, () => Object.FindFirstObjectByType<CharacterCarousel>().Poke()), (16.75, () => Shot("poke", ui: true)),
            (16.9, () => Object.FindFirstObjectByType<CharacterCarousel>().Pick(9)), (17.6, () => Shot("pick", ui: true)),
            (17.7, () => flow.ShowTitle()), (18.5, () => Shot("back", ui: true)),
            (18.7, () => flow.Play()), (19.8, () => flow.ShowTrack()), (21.0, () => Shot("track", ui: true)),
            (21.2, () => flow.StartRace()), (21.32, () => Shot("curtain", ui: true)),
            (29.0, () => { Debug.Log($"[menuplay] race scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, curtain alpha {LoadingCurtain.Instance?.GetComponent<CanvasGroup>()?.alpha}"); Shot("race", ui: true); }),
            // a win: the podium, then the card with the chest it earned, OPEN, three taps, what was inside
            (45.0, () => { var race = Object.FindFirstObjectByType<RaceManager>(); if (race != null && race.Player != null) race.Finish(race.Player); }),
            (56.0, () => { Debug.Log($"[menuplay] chest earned: {Chests.LastEarn.tier} (+{Chests.LastEarn.stars} stars), waiting {Chests.Pending}"); Shot("results", ui: true); }),
            (56.2, () => Object.FindFirstObjectByType<ChestBadge>()?.Open()), (57.4, () => Shot("chest-drop", ui: true)),
            (57.6, () => ChestReveal.Instance?.Tap()), (58.0, () => ChestReveal.Instance?.Tap()), (58.4, () => ChestReveal.Instance?.Tap()),
            (58.62, () => Shot("chest-burst", ui: true)), (59.15, () => Shot("chest-fly", ui: true)), (59.6, () => Shot("chest-fly2", ui: true)),
            (62.2, () => Shot("chest-cards", ui: true)),
        };
        if (stage < steps.Length) { if (t >= steps[stage].at) steps[stage++].act(); return; }
        EditorApplication.update -= Tick;
        Debug.Log("[menuplay] done");
        EditorApplication.Exit(0);
    }

    static void Said(string driver, string line, float seconds) => Debug.Log($"[menuplay] {driver} says \"{line}\" ({seconds:0.00} s)");

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

    /// `ui`: the overlay canvases (menu, curtain) borrow the camera for the shot, as they would draw on the phone.
    static void Shot(string name, bool ui = false)
    {
        // the podium borrows the race camera off its rig (and its MainCamera tag with it): any enabled camera will do
        var cam = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled && c.targetTexture == null).OrderByDescending(c => c.depth).FirstOrDefault();
        if (cam == null) { Debug.LogWarning("[menuplay] no camera for " + name); return; }
        int w = 2400, h = 1080;
        var moved = ui ? Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray() : new Canvas[0];
        foreach (var c in moved) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = c.sortingOrder > 0 ? 0.5f : 1f; }
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = prevTarget;
        foreach (var c in moved) c.renderMode = RenderMode.ScreenSpaceOverlay;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/menuplay-{name}.png", tex.EncodeToPNG());
        Object.Destroy(tex); rt.Release();
    }
}
