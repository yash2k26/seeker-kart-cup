using UnityEngine;

namespace KartCup
{
    /// Marks a race scene set after dark (the coast): karts spawn with their lamps lit, and the player's throw real light.
    public sealed class NightScene : MonoBehaviour
    {
        public static NightScene Instance { get; private set; }

        public Material lens, tail;                          // lamp glass on the bodywork: warm white ahead, red behind
        public Color headlight = new(1f, 0.93f, 0.8f);
        public float intensity = 160f, range = 30f, angle = 62f;    // strong: the beams hit dark asphalt at a grazing angle
        public float smoke = 0.3f;                           // exhaust and drift smoke are unlit white: this dims them after dark

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
