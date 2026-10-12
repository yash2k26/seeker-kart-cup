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
        public double Sol => KartPricing.SolFor(Total);
        public string Tier => KartPricing.TierFor(Total);

        /// Two pips is the free karts' middle; every pip either way is a few percent, so a Hero kart is quicker but
        /// a good driver in a free one still races it.
        public void ApplyTo(Kart kart)
        {
            int s = speed - 2, t = turbo - 2, g = grip - 2, a = armor - 2, k = attack - 2;
            // A pip is 6% of top speed (9 Oct 2026; was 3.5%): a Hero kart's SPEED 5 is +18%, so a bought kart visibly
            // walks away from a free one, Beach Buggy's way. referee.ts V_SUSTAINED and referee-test.ts carry the same numbers.
            kart.maxSpeed *= 1f + s * 0.06f;
            kart.acceleration *= 1f + s * 0.06f;   // 13 Oct 2026: 4% a pip was not felt off the line
            kart.boostSpeedAdd *= 1f + t * 0.15f;
            kart.driftBoostAdd *= 1f + t * 0.12f;
            kart.boostBurnRate *= 1f - t * 0.10f;
            kart.steerRate *= 1f + g * 0.06f;
            kart.maxSteer *= 1f + g * 0.04f;
            kart.sidewaysFriction *= 1f + g * 0.045f;
            kart.spinDecel *= Mathf.Max(0.4f, 1f - a * 0.1f);   // ARMOR: a hit costs less speed (spinDecel slows a spinning kart)
            if (kart.rb != null) kart.rb.mass *= 1f + a * 0.04f;   // heavier to bump; at 8% a pip ARMOR made a kart slower (measured, 13 Oct 2026)
            var powers = KartPowers.On(kart.gameObject);
            powers.attack *= 1f + k * 0.15f;
            if (attack >= 4) powers.bonusAmmo += 1;
        }
    }
}
