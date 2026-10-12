using System;
using UnityEngine;

namespace KartCup
{
    /// Gems, the premium currency (9 Oct 2026; Beach Buggy's): earned from chests, podiums, the daily and a first boss
    /// win, bought in packs (catalog.ts gems.*), spent to recruit the five judges. Kept on the phone like chests and
    /// items. ponytail: a PlayerPrefs balance; a Worker-side ledger when gems can be traded or when cheating costs us.
    public static class Gems
    {
        const string Key = "kc.gems";
        static int? cache;
        /// CloudSave: the balance was replaced underneath; read it again.
        internal static void Reload() { cache = null; Changed?.Invoke(); }
        public static event Action Changed;

        public static int Balance { get { cache ??= PlayerPrefs.GetInt(Key, 20); return cache.Value; } }   // a new phone starts with a few
        /// What the last podium paid, for the results card (ChestBadge); cleared with the chest's LastEarn.
        public static int LastPodium { get; private set; }

        public static void Earn(int n, string why)
        {
            if (n <= 0) return;
            Set((int)System.Math.Min((long)Balance + n, int.MaxValue));   // never wraps negative
            KartApi.Event("gems", new { n, why, balance = Balance });
        }
        public static bool Spend(int n, string on)
        {
            if (n < 0 || n > Balance) return false;      // a negative price would be a gift
            Set(Balance - n);
            KartApi.Event("gems_spent", new { n, on, balance = Balance });
            return true;
        }
        /// A podium place pays gems (solo and free rooms; never a paid room, like chests).
        public static void Podium(int place)
        {
            LastPodium = place switch { 1 => 5, 2 => 3, 3 => 2, _ => 0 };
            Earn(LastPodium, "podium");
        }
        public static void ClearLast() => LastPodium = 0;

        /// What a locked driver costs: the two Tour openers 100, Chase 200, Toly 300. "" (free) is 0.
        public static int PriceOf(CharacterDef d) => d == null || d.IsFree ? 0 : d.displayName switch { "Beeman" => 400, "Toly" => 300, "Chase" => 200, _ => 100 };

        static void Set(int v) { cache = v; PlayerPrefs.SetInt(Key, v); PlayerPrefs.Save(); Changed?.Invoke(); }
    }
}
