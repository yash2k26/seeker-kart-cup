using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// One kart in the garage. Free karts are the roster's defaults; shop karts carry a sku the Worker sells.
    /// Stats are pips from -2..+2 that sum to zero, the same budget as every character: a bought kart is a different
    /// SHAPE of car, never a faster one, so online rooms stay fair and the shop still has something to sell.
    [CreateAssetMenu(menuName = "KartCup/Kart", fileName = "Kart")]
    public sealed class KartDef : ScriptableObject
    {
        public string id = "oodi";                  // Kart_<id>.prefab
        public string displayName = "Kart";
        public string blurb = "";
        public string sku = "";                     // "" = free; "kart.<id>" = for sale (catalog.ts is the price list)
        [Range(-2, 2)] public int speed, accel, handling, weight;

        public bool IsFree => string.IsNullOrEmpty(sku);

        /// Half the weight of a character pip: the driver is the personality, the kart is the flavour.
        public void ApplyTo(Kart kart)
        {
            kart.maxSpeed *= 1f + speed * 0.025f;
            kart.acceleration *= 1f + accel * 0.045f;
            kart.steerRate *= 1f + handling * 0.04f;
            kart.maxSteer *= 1f + handling * 0.025f;
            kart.sidewaysFriction *= 1f + handling * 0.025f - weight * 0.015f;
            if (kart.rb != null) kart.rb.mass *= 1f + weight * 0.05f;
        }
    }
}
