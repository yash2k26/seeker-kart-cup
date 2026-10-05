using System.IO;
using System.Linq;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// What a race sounds like on paper: every AudioSource in a race scene and its prefabs' kart audio, one line each,
// so the mix can be read without ears.   tools/unity.sh AudioAudit.Run [-track N]  ->  [audio] lines + Builds/audio-audit.txt
public static class AudioAudit
{
    public static void Run()
    {
        int track = 1;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-track") int.TryParse(args[i + 1], out track);
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        var lines = new System.Collections.Generic.List<string>();
        foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(Path))
            lines.Add($"source {Path(s)}: clip={(s.clip ? s.clip.name : "-")} vol={s.volume:0.00} blend={s.spatialBlend:0.00} loop={s.loop} awake={s.playOnAwake} " +
                      $"min={s.minDistance:0} max={s.maxDistance:0} rolloff={s.rolloffMode} doppler={s.dopplerLevel:0.0} prio={s.priority} mixer={(s.outputAudioMixerGroup ? s.outputAudioMixerGroup.name : "-")}");
        foreach (var k in Object.FindObjectsByType<KartAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(Path))
            lines.Add($"kartaudio {Path(k)}: engine={(k.engineSnd && k.engineSnd.clip ? k.engineSnd.clip.name : "-")} pitch {k.engineMinPitch}-{k.engineMaxPitch} vol {k.engineMinVolume}-{k.engineMaxVolume} " +
                      $"boost={(k.boostStartSnd ? k.boostStartSnd.name : "-")} fail={(k.boostFailSnd ? k.boostFailSnd.name : "-")} tire={(k.defaultTireSnd ? k.defaultTireSnd.name : "-")} " +
                      $"collisions={k.collisionSnds?.Length ?? 0} jump={(k.jumpSnd ? k.jumpSnd.name : "-")} land={(k.landSnd ? k.landSnd.name : "-")} use={(k.itemUseSnd ? k.itemUseSnd.name : "-")} hit={(k.itemHitSnd ? k.itemHitSnd.name : "-")}");
        // the prefabs the race spawns at run time (items, effects) carry their own sources
        foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs", "Assets/_Game/Resources" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var s in go.GetComponentsInChildren<AudioSource>(true))
                lines.Add($"prefab {Path(s).Replace(go.name, Path(path))}: clip={(s.clip ? s.clip.name : "-")} vol={s.volume:0.00} blend={s.spatialBlend:0.00} loop={s.loop} awake={s.playOnAwake} min={s.minDistance:0} max={s.maxDistance:0}");
        }
        foreach (var l in lines) Debug.Log("[audio] " + l);
        Directory.CreateDirectory("Builds");
        File.WriteAllLines("Builds/audio-audit.txt", lines);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string Path(Component c) { var t = c.transform; var p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
    static string Path(string asset) => System.IO.Path.GetFileNameWithoutExtension(asset);
}
