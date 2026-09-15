using UnityEngine;

namespace KartCup
{
    /// The roster, in select-screen order. One asset, referenced by the select screen and the spawner.
    [CreateAssetMenu(menuName = "KartCup/Character Database", fileName = "Characters")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        public CharacterDef[] characters = System.Array.Empty<CharacterDef>();

        const string Key = "kc.character";
        public static int SelectedIndex
        {
            get => PlayerPrefs.GetInt(Key, 0);
            set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }

        public CharacterDef Selected => characters.Length == 0 ? null : characters[Mathf.Clamp(SelectedIndex, 0, characters.Length - 1)];
        public CharacterDef At(int i) => characters.Length == 0 ? null : characters[((i % characters.Length) + characters.Length) % characters.Length];
    }
}
