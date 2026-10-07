using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// The garage: every kart, which one the player picked, and which ones they own. Ownership comes from the
    /// Worker's profile (unlocks) and is cached on the phone so the garage reads right offline too.
    [CreateAssetMenu(menuName = "KartCup/Kart Database", fileName = "Karts")]
    public sealed class KartDatabase : ScriptableObject
    {
        public KartDef[] karts = System.Array.Empty<KartDef>();

        const string Key = "kc.kart";
        const string TrialArmed = "kc.trial.armed";

        /// "" = drive the character's own kart.
        public static string SelectedId
        {
            get => PlayerPrefs.GetString(Key, "");
            set { PlayerPrefs.SetString(Key, value ?? ""); PlayerPrefs.Save(); }
        }

        public KartDef Find(string id) => karts.FirstOrDefault(k => k != null && k.id == id);
        public KartDef At(int i) => karts.Length == 0 ? null : karts[((i % karts.Length) + karts.Length) % karts.Length];
        public int IndexOf(string id) { for (int i = 0; i < karts.Length; i++) if (karts[i] != null && karts[i].id == id) return i; return 0; }

        public static bool Owned(KartDef k) => k != null && (k.IsFree || Unlocks.Has(k.sku));

        // ---- one free trial race per shop kart: drive it once, then decide ----
        public static bool TrialUsed(string id) => PlayerPrefs.GetInt("kc.trial.used." + id, 0) == 1;
        public static void ArmTrial(string id) { PlayerPrefs.SetString(TrialArmed, id); PlayerPrefs.Save(); }
        public static string ArmedTrial => PlayerPrefs.GetString(TrialArmed, "");

        /// A chest's test drive: the kart's free trial handed back (the garage's TEST DRIVE arms it again).
        public static void GrantTrial(string id) { PlayerPrefs.DeleteKey("kc.trial.used." + id); PlayerPrefs.Save(); }

        /// Called by the spawner when a trial kart actually rolls onto the grid: the trial is spent.
        public static void SpendTrial(string id)
        {
            PlayerPrefs.SetInt("kc.trial.used." + id, 1);
            PlayerPrefs.DeleteKey(TrialArmed);
            PlayerPrefs.Save();
        }

        /// The kart the player will race for character `c`: their pick if they own it or a trial is armed for it,
        /// otherwise the character's default. Never a kart they have not paid for.
        public string EffectiveKartFor(CharacterDef c, bool allowTrial)
        {
            var picked = Find(SelectedId);
            if (picked == null) return c != null ? c.kartId : "oodi";
            if (Owned(picked)) return picked.id;
            if (allowTrial && ArmedTrial == picked.id && !TrialUsed(picked.id)) return picked.id;
            return c != null ? c.kartId : "oodi";
        }
    }

    /// The skus this wallet owns, as the Worker last reported them. A comma list in PlayerPrefs: it is read on the
    /// select screen and the grid, both of which must work with no network.
    public static class Unlocks
    {
        const string Key = "kc.unlocks";
        static string[] cache;

        public static void Set(System.Collections.Generic.IEnumerable<string> skus)
        {
            cache = skus?.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray() ?? System.Array.Empty<string>();
            PlayerPrefs.SetString(Key, string.Join(",", cache)); PlayerPrefs.Save();
        }

        public static void Clear() => Set(System.Array.Empty<string>());

        public static bool Has(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return true;
            cache ??= PlayerPrefs.GetString(Key, "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            return System.Array.IndexOf(cache, sku) >= 0 || System.Array.IndexOf(Earned, sku) >= 0;
        }

        /// What chests gave (paints), kept apart from the Worker's list, which Set overwrites. On this phone only.
        const string EarnedKey = "kc.earned";
        static string[] Earned => PlayerPrefs.GetString(EarnedKey, "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);
        public static void Earn(string sku)
        {
            if (string.IsNullOrEmpty(sku) || System.Array.IndexOf(Earned, sku) >= 0) return;
            PlayerPrefs.SetString(EarnedKey, string.Join(",", Earned.Append(sku))); PlayerPrefs.Save();
        }
    }
}
