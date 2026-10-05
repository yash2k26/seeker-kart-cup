using UnityEngine;

namespace KartCup
{
    /// Every driver's lines in their own voice (tools/audio/voices.py renders them; VoiceSetup files them into
    /// Resources/DriverVoices). Kinds: hello (arriving on the turntable), poke (tapped), annoyed (tapped again and
    /// again), go (CHOOSE TRACK), win (first over the line), hurt (hit by an item). Voices.Say plays them.
    public sealed class DriverVoices : ScriptableObject
    {
        [System.Serializable]
        public sealed class Line { public string driver, kind, text; public AudioClip clip; }

        public Line[] lines = System.Array.Empty<Line>();
    }
}
