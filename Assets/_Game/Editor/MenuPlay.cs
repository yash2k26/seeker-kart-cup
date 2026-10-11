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
            (14.2, () => flow.ShowTitle()), (15.4, () => Shot("hub", ui: true)),                                         // the front door, then the hub: your pair on the stage
            (15.5, () => flow.ChooseSeries()), (16.0, () => Shot("drive-a", ui: true)), (16.5, () => Shot("drive-b", ui: true)), (17.0, () => Shot("drive-c", ui: true)),
            (17.9, () => Shot("series", ui: true)), (18.0, () => flow.ShowTitle()), (18.4, () => flow.ShowSelect()), (19.4, () => Shot("select", ui: true)),
            (19.45, () => Object.FindFirstObjectByType<CharacterCarousel>().Poke()), (19.65, () => Shot("poke", ui: true)),
            (19.8, () => Object.FindFirstObjectByType<CharacterCarousel>().Pick(9)), (20.5, () => Shot("pick", ui: true)),
            (20.6, () => flow.ShowTitle()), (21.4, () => Shot("back", ui: true)),
            (21.6, () => flow.Play()), (22.7, () => flow.ShowTrack()), (23.9, () => Shot("track", ui: true)),
            // the series, its first event's card, PLAY: the card's fuel chip goes 6 -> 5 (a "-1" pops off it) before the race loads
            (23.95, () => flow.ShowTour()), (24.6, () => Object.FindFirstObjectByType<TourPanel>()?.Pick(0)), (25.2, () => Object.FindFirstObjectByType<TourPanel>()?.Play(0)),
            (26.0, () => Object.FindFirstObjectByType<TourPanel>()?.Race()), (26.3, () => Shot("fuel-spent", ui: true)), (26.9, () => Shot("curtain", ui: true)),
            (34.7, () => { Debug.Log($"[menuplay] race scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, curtain alpha {LoadingCurtain.Instance?.GetComponent<CanvasGroup>()?.alpha}"); Shot("race", ui: true); }),
            // a win: the podium, then the card with the chest it earned, OPEN, three taps, what was inside
            (50.7, () => { var race = Object.FindFirstObjectByType<RaceManager>(); if (race != null && race.Player != null) race.Finish(race.Player); }),
            (61.7, () => { Debug.Log($"[menuplay] chest earned: {Chests.LastEarn.tier} (+{Chests.LastEarn.stars} stars), waiting {Chests.Pending}"); Shot("results", ui: true); }),
            (61.9, () => Object.FindFirstObjectByType<ChestBadge>()?.Open()), (63.1, () => Shot("chest-drop", ui: true)),
            (63.3, () => ChestReveal.Instance?.Tap()), (63.7, () => ChestReveal.Instance?.Tap()), (64.1, () => ChestReveal.Instance?.Tap()),
            (64.32, () => Shot("chest-burst", ui: true)), (64.85, () => Shot("chest-fly", ui: true)), (65.3, () => Shot("chest-fly2", ui: true)),
            (67.9, () => Shot("chest-cards", ui: true)),
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
