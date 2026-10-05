using System.Collections.Generic;
using System.IO;
using KartCup;
using UnityEditor;
using UnityEngine;

// Files the drivers' voices (tools/audio/vocalize.py -> Assets/_Game/Audio/Voice/voices.json + <Driver>/*.ogg) into
// Resources/DriverVoices.asset, which Voices.Say reads at run time. Mono, decompressed on load: they are short and
// must start the instant a kart is tapped.   Run: tools/unity.sh VoiceSetup.Build  (MenuSetup.Build calls it too)
public static class VoiceSetup
{
    const string Dir = "Assets/_Game/Audio/Voice/";
    const string Asset = "Assets/_Game/Resources/DriverVoices.asset";

    [System.Serializable] class Entry { public string driver, kind, file, text; public float seconds; }
    [System.Serializable] class Manifest { public Entry[] lines; }

    public static void Build()
    {
        var json = Dir + "voices.json";
        if (!File.Exists(json)) { Debug.LogWarning("[voices] no " + json + ": run tools/audio/vocalize.py"); return; }
        var lines = new List<DriverVoices.Line>();
        foreach (var e in JsonUtility.FromJson<Manifest>(File.ReadAllText(json)).lines)
        {
            var path = Dir + e.file;
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var s = importer.defaultSampleSettings;
                if (!importer.forceToMono || s.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    importer.forceToMono = true;
                    s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f;
                    importer.defaultSampleSettings = s;
                    importer.SaveAndReimport();
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { Debug.LogWarning("[voices] missing " + path); continue; }
            lines.Add(new DriverVoices.Line { driver = e.driver, kind = e.kind, text = e.text, clip = clip });
        }
        var bank = AssetDatabase.LoadAssetAtPath<DriverVoices>(Asset);
        if (bank == null) { bank = ScriptableObject.CreateInstance<DriverVoices>(); Directory.CreateDirectory(Path.GetDirectoryName(Asset)); AssetDatabase.CreateAsset(bank, Asset); }
        bank.lines = lines.ToArray();
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log($"[voices] {lines.Count} lines in {Asset}");
    }
}
