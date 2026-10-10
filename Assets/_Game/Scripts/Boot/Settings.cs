using UnityEngine;

namespace KartCup
{
    /// The things a player is allowed to turn off. Screen shake is an accessibility setting, not a preference:
    /// Vlambeer had to add one to Nuclear Throne because it made people ill. Sound is everything you hear; music is
    /// only the soundtrack. Stored in PlayerPrefs, applied at boot.
    public static class Settings
    {
        public static bool Shake { get => Get("kc.shake"); set => Set("kc.shake", value); }
        public static bool Haptics { get => Get("kc.haptics"); set { Set("kc.haptics", value); KartCup.Haptics.Enabled = value; } }
        public static bool ShowFps { get => Get("kc.fps", false); set => Set("kc.fps", value); }
        public static bool Sound { get => Get("kc.sound"); set { Set("kc.sound", value); AudioListener.volume = value ? 1f : 0f; } }
        public static bool Music { get => Get("kc.music"); set { Set("kc.music", value); AudioDirector.Instance?.ApplyVolume(); } }
        /// The kart accelerates by itself (Beach Buggy's way); off, GAS must be held. ControlLayout moves the buttons to match.
        public static bool AutoGas { get => Get("kc.autogas"); set { Set("kc.autogas", value); ControlLayout.ApplyAll(); } }
        /// How the kart is steered: the two arrows, a drag anywhere on the left half (wherever the thumb lands is
        /// centre), or tilting the phone. Not Beach Buggy's hold-a-screen-half: our DRIFT sits on the right half.
        public enum Steer { Arrows, Slide, Tilt }
        public static Steer Steering
        {
            get => (Steer)Mathf.Clamp(PlayerPrefs.GetInt("kc.steer", 0), 0, 2);
            set { PlayerPrefs.SetInt("kc.steer", (int)value); PlayerPrefs.Save(); ControlLayout.ApplyAll(); }
        }

        /// Called once at boot so a setting stored last session is live before anything reads it.
        public static void Apply()
        {
            KartCup.Haptics.Enabled = Haptics;
            AudioListener.volume = Sound ? 1f : 0f;
        }

        static bool Get(string key, bool fallback = true) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;
        static void Set(string key, bool on) { PlayerPrefs.SetInt(key, on ? 1 : 0); PlayerPrefs.Save(); }
    }
}
