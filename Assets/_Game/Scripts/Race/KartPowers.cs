using UnityEngine;

namespace KartCup
{
    /// What a kart and its driver add up to for the item system: ATTACK scales the projectiles it fires, and a big
    /// one adds a shot to every pickup. Written by KartDef/CharacterDef.ApplyTo on the grid, read by KartHooks.
    public sealed class KartPowers : MonoBehaviour
    {
        public float attack = 1f;      // projectile speed multiplier
        public int bonusAmmo;          // extra shots per pickup

        public static KartPowers On(GameObject go) => go.GetComponent<KartPowers>() ?? go.AddComponent<KartPowers>();
    }
}
