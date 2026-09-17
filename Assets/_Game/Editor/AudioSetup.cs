using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

// Builds the scene's AudioDirector and fills its clip slots from the project's audio library.
// Called by TrackSetup and MenuSetup so both scenes stay in sync with one list.
public static class AudioSetup
{
    const string Sfx = "Assets/_Game/Audio/SFX/";
    const string Music = "Assets/_Game/Audio/Music/";

    public static AudioDirector Build(bool menu)
    {
        var go = new GameObject("Audio");
        var director = go.AddComponent<AudioDirector>();
        var so = new SerializedObject(director);

        // Menu keeps the calmer theme; the race gets the driving one.
        Set(so, "track", Clip(Music + (menu ? "beach_sports_theme_loop.ogg" : "beach_stage_bpm128.ogg")));
        so.FindProperty("musicVolume").floatValue = menu ? 0.42f : 0.30f;   // race music sits under the engines

        var clicks = so.FindProperty("clicks");
        var clickClips = new[] { "ui/click_001.ogg", "ui/click_003.ogg", "ui/select_001.ogg" }.Select(c => Clip(Sfx + c)).Where(c => c != null).ToArray();
        clicks.arraySize = clickClips.Length;
        for (int i = 0; i < clickClips.Length; i++) clicks.GetArrayElementAtIndex(i).objectReferenceValue = clickClips[i];

        Set(so, "back", Clip(Sfx + "ui/back_001.ogg"));
        Set(so, "confirm", Clip(Sfx + "ui/confirmation_001.ogg"));

        // The voiceover pack carries the whole race structure.
        Set(so, "three", Clip(Sfx + "voice/3.ogg"));
        Set(so, "two", Clip(Sfx + "voice/2.ogg"));
        Set(so, "one", Clip(Sfx + "voice/1.ogg"));
        Set(so, "go", Clip(Sfx + "voice/go.ogg"));
        Set(so, "finalLap", Clip(Sfx + "voice/final_round.ogg"));
        Set(so, "win", Clip(Sfx + "voice/you_win.ogg"));
        Set(so, "lose", Clip(Sfx + "voice/congratulations.ogg"));
        Set(so, "lapDone", Clip(Sfx + "jingles/jingles_STEEL00.ogg"));
        Set(so, "pickup", Clip(Sfx + "digital/powerUp3.ogg"));
        Set(so, "useItem", Clip(Sfx + "digital/phaserDown1.ogg"));

        so.ApplyModifiedPropertiesWithoutUndo();
        return director;
    }

    static void Set(SerializedObject so, string field, AudioClip clip)
    {
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("[audio] no field " + field); return; }
        if (clip == null) Debug.LogWarning("[audio] missing clip for " + field);
        p.objectReferenceValue = clip;
    }

    static AudioClip Clip(string path)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning("[audio] not found: " + path);
        return c;
    }
}
