using System.Collections.Generic;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// A monkey on the whole game (12 Oct 2026, the user: "run through every minor thing ... stress it as much as you
/// can"): the real game in play mode, from the title, for `-seconds` (default 360), pressing a random live button every
/// few frames and the Android back key now and then, into races (finished for it after a while, so the podium and the
/// card get pressed too) and out again. Every exception and error is counted with where it came from; the report says
/// how many buttons were pressed, how many different ones, which screens and scenes it reached. Shop and wallet
/// buttons are pressed too: in the editor there is no wallet, and what matters is that nothing throws.
/// It talks to a local Worker (cd worker && npx wrangler dev --port 8787), never the live one.
///   ./tools/unity.sh MenuMonkey.Run [-seconds 600]
public static class MenuMonkey
{
    static double start, nextPress, raceSince = -1, until = 360;
    static int presses, backs, seed = 12;
    static System.Random rng;
    static readonly Dictionary<string, int> pressed = new(), errors = new();
    static readonly HashSet<string> scenes = new(), panels = new();
    static Keyboard keys; static bool escDown;
    static readonly string[] Saved = { "kc.gems", "kc.fuel", "kc.fuel.at" };
    static readonly Dictionary<string, string> savedState = new();

    public static void Run()
    {
        foreach (var a in System.Environment.GetCommandLineArgs().Select((v, i) => (v, i)))
        {
            if (a.v == "-seconds" && double.TryParse(System.Environment.GetCommandLineArgs()[a.i + 1], out var s)) until = s;
            if (a.v == "-seed" && int.TryParse(System.Environment.GetCommandLineArgs()[a.i + 1], out var sd)) seed = sd;
        }
        EditorUtility.audioMasterMute = true;
        if (!EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.DisableDomainReload)
        { EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload; AssetDatabase.SaveAssets(); }
        foreach (var k in Saved) savedState[k] = PlayerPrefs.HasKey(k) ? (k == "kc.fuel.at" ? PlayerPrefs.GetString(k) : PlayerPrefs.GetInt(k).ToString()) : null;
        PlayerPrefs.SetInt("kc.gems", 5000); PlayerPrefs.SetInt("kc.fuel", Fuel.Max); PlayerPrefs.Save();   // never stuck on an empty tank
        KartApi.BaseUrl = "http://127.0.0.1:8787";
        rng = new System.Random(seed);
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Log(string msg, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        if (msg.StartsWith("[monkey]")) return;
        var where = (stack ?? "").Split('\n').FirstOrDefault(l => l.Contains("KartCup") || l.Contains("Assets/")) ?? "";
        var key = $"{type}: {msg.Split('\n')[0].Trim()}  @ {where.Trim()}";
        errors[key] = errors.GetValueOrDefault(key) + 1;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        AudioListener.volume = 0f;
        double t = EditorApplication.timeSinceStartup - start;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        scenes.Add(scene);
        if (keys == null) keys = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
        if (escDown) { InputSystem.QueueStateEvent(keys, new KeyboardState()); escDown = false; }

        // a race: let it run a while, then finish it for the monkey, so the podium, the card and its buttons get pressed
        var race = Object.FindFirstObjectByType<RaceManager>();
        if (race != null && race.Player != null)
        {
            if (raceSince < 0) raceSince = t;
            if (t - raceSince > 25 && !race.Player.finished && race.state == RaceManager.State.Racing) race.Finish(race.Player);
        }
        else raceSince = -1;
        var flow = Object.FindFirstObjectByType<ScreenFlow>();
        if (flow != null)   // the screen ScreenFlow has up (its private `current`, an index into its panels)
        {
            var f = typeof(ScreenFlow);
            int cur = (int)(f.GetField("current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(flow) ?? -1);
            var list = f.GetField("panels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(flow) as GameObject[];
            if (list != null && cur >= 0 && cur < list.Length && list[cur] != null) panels.Add(list[cur].name);
        }

        if (t >= until) { Finish(); return; }
        if (t < 4 || EditorApplication.timeSinceStartup < nextPress) return;
        nextPress = EditorApplication.timeSinceStartup + 0.12 + rng.NextDouble() * 0.3;

        if (rng.Next(14) == 0 && keys != null)        // the Android back key
        {
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Escape)); escDown = true; backs++;
            return;
        }
        // any live button a finger could reach: active, interactable, not under a hidden or blocking group
        var live = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Where(b => b.isActiveAndEnabled && b.interactable && b.GetComponentsInParent<CanvasGroup>().All(g => g.interactable && g.blocksRaycasts && g.alpha > 0.05f))
            .ToList();
        if (live.Count == 0) return;
        var pick = live[rng.Next(live.Count)];
        var name = Path(pick.transform);
        if (name.Contains("Quit")) return;            // leaving the app ends the run, not a finding
        pressed[name] = pressed.GetValueOrDefault(name) + 1;
        presses++;
        var ev = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(pick.gameObject, ev, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(pick.gameObject, ev, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(pick.gameObject, ev, ExecuteEvents.pointerClickHandler);
    }

    static string Path(Transform t) { var parts = new List<string>(); for (int i = 0; t != null && i < 4; i++, t = t.parent) parts.Add(t.name); parts.Reverse(); return string.Join("/", parts); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        Debug.Log($"[monkey] {presses} presses on {pressed.Count} different buttons, {backs} back presses, over {until:0} s");
        Debug.Log($"[monkey] scenes: {string.Join(", ", scenes)}");
        Debug.Log($"[monkey] screens: {string.Join(", ", panels.OrderBy(p => p))}");
        foreach (var kv in pressed.OrderByDescending(kv => kv.Value).Take(15)) Debug.Log($"[monkey]   {kv.Value,4}x {kv.Key}");
        if (errors.Count == 0) Debug.Log("[monkey] PASS — no exception or error");
        else
        {
            Debug.Log($"[monkey] {errors.Count} DIFFERENT ERROR(S), {errors.Values.Sum()} in all:");
            foreach (var kv in errors.OrderByDescending(kv => kv.Value)) Debug.Log($"[monkey]   {kv.Value,4}x {kv.Key}");
        }
        foreach (var k in Saved) { PlayerPrefs.DeleteKey(k); if (savedState[k] == null) continue; if (k == "kc.fuel.at") PlayerPrefs.SetString(k, savedState[k]); else PlayerPrefs.SetInt(k, int.Parse(savedState[k])); }
        PlayerPrefs.Save();
        EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
    }
}
