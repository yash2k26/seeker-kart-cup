using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// One racer on the select screen. Stats are pips from -2..+2 that multiply Powerslide's own kart fields,
    /// so every character drives the same tuned rig with a different flavour.
    [CreateAssetMenu(menuName = "KartCup/Character", fileName = "Character")]
    public sealed class CharacterDef : ScriptableObject
    {
        public string displayName = "Racer";
        public string kartId = "oodi";              // Kenney kart model: Kart_<kartId>.prefab
        public string driverId = "Generic";         // Driver_<driverId>.prefab
        public Color tint = Color.white;
        [Range(-2, 2)] public int speed, accel, handling, weight;
        public string blurb = "";

        public void ApplyTo(Kart kart)
        {
            kart.maxSpeed *= 1f + speed * 0.05f;
            kart.acceleration *= 1f + accel * 0.09f;
            kart.steerRate *= 1f + handling * 0.08f;
            kart.maxSteer *= 1f + handling * 0.05f;
            kart.sidewaysFriction *= 1f + handling * 0.05f - weight * 0.03f;
            if (kart.rb != null) kart.rb.mass *= 1f + weight * 0.10f;
        }
    }
}
