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
        void OnEnable() { if (characters != null && characters.Length > 0) count = characters.Length; }

        /// Always a driver on the roster: a phone that picked Maroo (10) or Andrew (11) before Runa and Vee left (6 Oct)
        /// had an index past the end, and its kart was never put on the grid.
        public static int SelectedIndex
        {
            get { int i = Mathf.Max(0, PlayerPrefs.GetInt(Key, 0)); return count > 0 && i >= count ? count - 1 : i; }
            set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }

        public CharacterDef Selected => characters.Length == 0 ? null : characters[Mathf.Clamp(SelectedIndex, 0, characters.Length - 1)];
        public CharacterDef At(int i) => characters.Length == 0 ? null : characters[((i % characters.Length) + characters.Length) % characters.Length];
    }
}
