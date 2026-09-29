using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// One kart in the garage, with five powers of 0..5 pips: SPEED (top speed), TURBO (how hard and how long a boost
    /// hits), GRIP (steering and cornering bite), ARMOR (mass, and how fast a spin-out ends) and ATTACK (fired items
    /// fly faster; a big one adds a shot to every pickup). The free five total ten; what you buy totals more, and the
    /// price follows the total (KartPricing). A locked kart is one whose sku this wallet has not bought.
    [CreateAssetMenu(menuName = "KartCup/Kart", fileName = "Kart")]
    public sealed class KartDef : ScriptableObject
    {
        public string id = "oodi";                  // Kart_<id>.prefab
        public string displayName = "Kart";
        public string blurb = "";
        public string sku = "";                     // "" = free; "kart.<id>" = for sale (catalog.ts quotes it)
        [Range(0, 5)] public int speed = 2, turbo = 2, grip = 2, armor = 2, attack = 2;

        public bool IsFree => string.IsNullOrEmpty(sku);
        public int Total => speed + turbo + grip + armor + attack;
        public double Usd => KartPricing.UsdFor(Total);
        public string Tier => KartPricing.TierFor(Total);

        /// Two pips is the free karts' middle; every pip either way is a few percent, so a Hero kart is quicker but
        /// a good driver in a free one still races it.
        public void ApplyTo(Kart kart)
        {
            int s = speed - 2, t = turbo - 2, g = grip - 2, a = armor - 2, k = attack - 2;
            kart.maxSpeed *= 1f + s * 0.035f;
            kart.acceleration *= 1f + s * 0.02f;
            kart.boostSpeedAdd *= 1f + t * 0.12f;
            kart.driftBoostAdd *= 1f + t * 0.10f;
            kart.boostBurnRate *= 1f - t * 0.08f;
            kart.steerRate *= 1f + g * 0.04f;
            kart.maxSteer *= 1f + g * 0.025f;
            kart.sidewaysFriction *= 1f + g * 0.03f;
            kart.spinDecel *= 1f + a * 0.12f;
            if (kart.rb != null) kart.rb.mass *= 1f + a * 0.08f;
            var powers = KartPowers.On(kart.gameObject);
            powers.attack *= 1f + k * 0.15f;
            if (attack >= 4) powers.bonusAmmo += 1;
        }
    }
}
