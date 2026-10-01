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

    // Original loops (tools/audio/game_music.py). Each circuit has its own; a race may also draw any of the others.
    static readonly (string map, string track)[] Own = { ("beach", "coast_run"), ("alpine", "blue_hour"), ("forest", "golden_mist"), ("speedway", "floodlights") };
    static readonly string[] Pool = { "coast_run", "blue_hour", "golden_mist", "floodlights", "afterburn" };

    public static AudioDirector Build(bool menu, string map = null)
    {
        var go = new GameObject("Audio");
        var director = go.AddComponent<AudioDirector>();
        var so = new SerializedObject(director);

        // The menu has its own unhurried loop. A race lists its circuit's track first and the rest of the pool after:
        // the director starts each race on one the last race did not play.
        string own = Own.FirstOrDefault(o => o.map == map).track ?? Pool[0];
        var list = (menu ? new[] { "garage" } : new[] { own }.Concat(Pool.Where(t => t != own))).Select(t => Clip(Music + t + ".ogg")).Where(c => c != null).ToArray();
        var tracks = so.FindProperty("tracks");
        tracks.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++) tracks.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.FindProperty("musicVolume").floatValue = menu ? 0.5f : 0.42f;   // the loops are mastered at -15 LUFS: under the engines at this

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
        Set(so, "pickup", Clip(Sfx + "items/pickup.ogg"));
        Set(so, "useItem", Clip(Sfx + "items/throw.ogg"));
        Set(so, "podUp", Clip(Sfx + "items/launcher_clack.ogg"));
        Set(so, "bombUp", Clip(Sfx + "items/bomb_appear.ogg"));
        Set(so, "hit", Clip(Sfx + "items/kart_hit.ogg"));
        // Using an item, by its ItemManager name. The rockets and the sun bomb sound from the thing itself (ItemSetup
        // puts the source on their prefabs), so here they map to nothing rather than to the fallback.
        (string item, string clip)[] items = { ("Nitro", "nitro_boost"), ("Turbo", "turbo_boost"), ("Bouncer", "bouncer_launch"), ("Bomb", "bomb_drop"),
                                               ("Trap", "trap_drop"), ("Sun Bomb", "throw"), ("Rocket", null), ("Homing Item", null) };
        var names = so.FindProperty("itemNames"); var clips = so.FindProperty("itemClips");
        names.arraySize = clips.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            names.GetArrayElementAtIndex(i).stringValue = items[i].item;
            clips.GetArrayElementAtIndex(i).objectReferenceValue = items[i].clip != null ? Clip(Sfx + $"items/{items[i].clip}.ogg") : null;
        }

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
