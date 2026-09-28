using TMPro;
using UnityEngine;

namespace KartCup
{
    /// One row of the settings screen. The label carries its own state ("SCREEN SHAKE   ON") rather than a custom
    /// switch widget: one label per intent, no new art, and it reads correctly at arm's length.
    public sealed class SettingToggle : MonoBehaviour
    {
        public enum Which { Shake, Haptics, Fps }

        [SerializeField] Which setting;
        [SerializeField] TMP_Text label;
        [SerializeField] string caption = "SETTING";

        void OnEnable() => Refresh();

        public void Toggle()
        {
            switch (setting)
            {
                case Which.Shake: Settings.Shake = !Settings.Shake; break;
                case Which.Haptics: Settings.Haptics = !Settings.Haptics; break;
                case Which.Fps: Settings.ShowFps = !Settings.ShowFps; break;
            }
            Refresh();
            Haptics.Play(Haptics.Kind.Tick);     // silent when the player just turned haptics off, which is the point
            AudioDirector.Instance?.Click();
        }

        bool Value => setting switch { Which.Shake => Settings.Shake, Which.Haptics => Settings.Haptics, _ => Settings.ShowFps };

        void Refresh()
        {
            if (label != null) label.text = $"{caption}<pos=78%>{(Value ? "ON" : "OFF")}";
        }
    }
}
