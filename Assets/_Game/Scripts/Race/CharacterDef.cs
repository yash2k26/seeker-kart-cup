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
        [Range(-2, 2)] public int speed, accel, handling, weight, turbo, attack;   // six pips that sum to zero: a shape, not an edge
        public string blurb = "";

        /// Paint the kart in this racer's colour. Six runtime material instances per race is nothing, and the SRP
        /// Batcher still batches them because it keys on the shader, not the material.
        public void ApplyLivery(GameObject kartRoot) { if (tint != Color.white) Paint(kartRoot, tint); }

        /// Any colour onto the body: the character's tint, or a paint job the player bought.
        public static void Paint(GameObject kartRoot, Color colour, bool gold = false)
        {
            foreach (var r in kartRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.name != "Body") continue;                  // wheels, driver and shadow keep their own colours
                var mats = r.materials;                          // instances, so one racer's paint is their own
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i].SetColor("_BaseColor", colour);
                    if (gold) { mats[i].SetFloat("_Metallic", 0.92f); mats[i].SetFloat("_Smoothness", 0.82f); }   // 24k: a metal, it catches the lamps
                }
                r.materials = mats;
            }
        }

        public void ApplyTo(Kart kart)
        {
            kart.maxSpeed *= 1f + speed * 0.05f;
            kart.acceleration *= 1f + accel * 0.09f;
            kart.steerRate *= 1f + handling * 0.08f;
            kart.maxSteer *= 1f + handling * 0.05f;
            kart.boostSpeedAdd *= 1f + turbo * 0.08f;
            kart.driftBoostAdd *= 1f + turbo * 0.06f;
            KartPowers.On(kart.gameObject).attack *= 1f + attack * 0.10f;
            kart.sidewaysFriction *= 1f + handling * 0.05f - weight * 0.03f;
            if (kart.rb != null) kart.rb.mass *= 1f + weight * 0.10f;
        }
    }
}
