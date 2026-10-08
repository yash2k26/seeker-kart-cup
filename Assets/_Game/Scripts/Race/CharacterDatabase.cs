using UnityEngine;

namespace KartCup
{
    /// The roster, in select-screen order. One asset, referenced by the select screen and the spawner.
    [CreateAssetMenu(menuName = "KartCup/Character Database", fileName = "Characters")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        public CharacterDef[] characters = System.Array.Empty<CharacterDef>();

        const string Key = "kc.character";
        static int count;   // the roster's size, from the database once loaded
        public static CharacterDatabase Instance { get; private set; }
        void OnEnable() { if (characters != null && characters.Length > 0) { count = characters.Length; Instance = this; } }

        /// Free, bought, or earned by beating them as a Tour boss (Unlocks).
        public static bool Owned(CharacterDef d) => d != null && (d.IsFree || Unlocks.Has(d.sku));
        /// The first driver this phone may race as: a fallback for a saved pick that is locked (a fresh install's 0 is Toly).
        public int FirstOwned() { for (int i = 0; i < characters.Length; i++) if (Owned(characters[i])) return i; return 0; }

        /// Always a driver on the roster: a phone that picked Maroo (10) or Andrew (11) before Runa and Vee left (6 Oct)
        /// had an index past the end, and its kart was never put on the grid.
        public static int SelectedIndex
        {
            get
            {
                int i = Mathf.Max(0, PlayerPrefs.GetInt(Key, 0)); if (count > 0 && i >= count) i = count - 1;
                // a locked driver is browsed, never raced: the index every race start reads falls back to one that is owned
                if (Instance != null && i < Instance.characters.Length && !Owned(Instance.characters[i])) i = Instance.FirstOwned();
                return i;
            }
            set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }

        public CharacterDef Selected => characters.Length == 0 ? null : characters[Mathf.Clamp(SelectedIndex, 0, characters.Length - 1)];
        public CharacterDef At(int i) => characters.Length == 0 ? null : characters[((i % characters.Length) + characters.Length) % characters.Length];
    }
}
