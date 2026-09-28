using UnityEngine;

namespace KartCup
{
    /// The three things a player is allowed to turn off. Screen shake is an accessibility setting, not a preference:
    /// Vlambeer had to add one to Nuclear Throne because it made people ill. Stored in PlayerPrefs, applied at boot.
    public static class Settings
    {
        public static bool Shake { get => Get("kc.shake"); set => Set("kc.shake", value); }
        public static bool Haptics { get => Get("kc.haptics"); set { Set("kc.haptics", value); KartCup.Haptics.Enabled = value; } }
        public static bool ShowFps { get => Get("kc.fps", false); set => Set("kc.fps", value); }

        /// Called once at boot so a setting stored last session is live before anything reads it.
        public static void Apply() => KartCup.Haptics.Enabled = Haptics;

        static bool Get(string key, bool fallback = true) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
        static void Set(string key, bool on) { PlayerPrefs.SetInt(key, on ? 1 : 0); PlayerPrefs.Save(); }
    }
}
