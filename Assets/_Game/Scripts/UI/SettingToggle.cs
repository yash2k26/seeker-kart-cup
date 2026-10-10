using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// One row of the settings screen: what it is on the left, a switch on the right. The whole row is the switch's
    /// hit area. The knob slides and the track lights (the shell's lit violet, not the action gold) when it is on.
    public sealed class SettingToggle : MonoBehaviour
    {
        public enum Which { Shake, Haptics, Fps, Sound, Music, AutoGas, Steering }

        [SerializeField] Which setting;
        [SerializeField] TMP_Text label;
        [SerializeField] string caption = "SETTING";
        [SerializeField] Image track;
        [SerializeField] RectTransform knob;
        [SerializeField] TMP_Text state;
        [SerializeField] TMP_Text hint;              // STEERING only: the row cycles three ways, and says how the chosen one works
        [SerializeField] Color onColour = new(0.455f, 0.275f, 1f), offColour = new(0.05f, 0.012f, 0.15f);
        [SerializeField] float travel = 34f;

        void OnEnable() => Refresh(false);

        public void Toggle()
        {
            switch (setting)
            {
                case Which.Shake: Settings.Shake = !Settings.Shake; break;
                case Which.Haptics: Settings.Haptics = !Settings.Haptics; break;
                case Which.Fps: Settings.ShowFps = !Settings.ShowFps; break;
                case Which.Sound: Settings.Sound = !Settings.Sound; break;
                case Which.Music: Settings.Music = !Settings.Music; break;
                case Which.AutoGas: Settings.AutoGas = !Settings.AutoGas; break;
                case Which.Steering: Settings.Steering = (Settings.Steer)(((int)Settings.Steering + 1) % 3); break;
            }
            Refresh(true);
            if (setting == Which.Haptics) Haptics.Play(Haptics.Kind.Tick);   // felt only when it was just turned on, which is the point
        }

        bool Value => setting switch
        {
            Which.Shake => Settings.Shake, Which.Haptics => Settings.Haptics, Which.Sound => Settings.Sound,
            Which.Music => Settings.Music, Which.AutoGas => Settings.AutoGas, _ => Settings.ShowFps,
        };

        /// Edit-mode previews: the row as the player will first see it.
        public void ShowInEditor() => Refresh(false);

        void Refresh(bool animate)
        {
            bool on = Value;
            if (label != null) label.text = caption;
            if (setting == Which.Steering)
            {
                var mode = Settings.Steering;
                if (state != null) state.text = mode.ToString().ToUpperInvariant();
                if (hint != null) hint.text = mode switch
                {
                    Settings.Steer.Slide => "Slide your left thumb sideways, anywhere",
                    Settings.Steer.Tilt => "Tilt the phone like a wheel",
                    _ => "Two arrows under your left thumb",
                };
                return;
            }
            if (state != null) state.text = on ? "ON" : "OFF";
            if (track != null) track.color = on ? onColour : offColour;
            if (knob == null) return;
            float x = on ? travel : -travel;
            if (Application.isPlaying) Tween.StopAll(knob);
            if (animate && Application.isPlaying) Tween.UIAnchoredPositionX(knob, x, 0.16f, Ease.OutBack, useUnscaledTime: true);
            else knob.anchoredPosition = new Vector2(x, knob.anchoredPosition.y);
        }
    }
}
