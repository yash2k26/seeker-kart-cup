using UnityEngine;

namespace KartCup
{
    /// Every driver's voice without words: laughs, giggles, "hey-yo!", "huh?", "woo-hoo!", "oof!" (tools/audio/vocalize.py
    /// synthesises them the way the films' laugh was made; VoiceSetup files them into Resources/DriverVoices). Kinds:
    /// hello (arriving on the turntable), poke (tapped), annoyed (tapped again and again), go (CHOOSE TRACK), win
    /// (first over the line), hurt (hit by an item). `text` is the comic-book word for the sound. Voices.Say plays them.
    public sealed class DriverVoices : ScriptableObject
    {
        [System.Serializable]
        public sealed class Line { public string driver, kind, text; public AudioClip clip; }

        public Line[] lines = System.Array.Empty<Line>();
    }
}
