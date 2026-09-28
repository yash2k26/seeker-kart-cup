using System.Reflection;
using PowerslideKartPhysics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Four ways to watch your own kart: the chase view, a close one, a high one that shows more road, and a front
    /// view that looks back at the driver's face. The HUD pill names the current one; tapping it opens a picker with
    /// all four, so you choose an angle rather than cycling blind. The choice is remembered.
    public sealed class CameraAngles : MonoBehaviour
    {
        public struct Preset { public string name; public float dist, height, lookAt, velDist; public bool front; }

        public static readonly Preset[] Presets =
        {
            new() { name = "CHASE", dist = 6.3f, height = 3.05f, lookAt = 1.05f, velDist = 1.0f },
            new() { name = "CLOSE", dist = 4.4f, height = 2.2f, lookAt = 1.0f, velDist = 0.5f },
            new() { name = "HIGH",  dist = 9.5f, height = 6.2f, lookAt = 0.6f, velDist = 1.2f },
            new() { name = "FRONT", dist = 5.2f, height = 2.1f, lookAt = 0f, velDist = 0.4f, front = true },
        };

        const string Key = "kc.cam";
        [SerializeField] KartCamera kartCamera;
        [SerializeField] TMP_Text pillLabel;                                  // "CAM · CHASE"
        [SerializeField] GameObject picker;                                   // the four option pills
        [SerializeField] Image[] optionPills = System.Array.Empty<Image>();  // tinted: accent = current
        [SerializeField] Color accent = new(1f, 0.353f, 0.212f), idle = new(0.106f, 0.212f, 0.263f, 0.85f);

        static readonly FieldInfo LookBack = typeof(KartCamera).GetField("lookBack", BindingFlags.NonPublic | BindingFlags.Instance);

        public int Index { get; private set; }
        public bool PickerOpen => picker != null && picker.activeSelf;

        void Start()
        {
            if (kartCamera == null) kartCamera = GetComponent<KartCamera>();
            Index = Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Presets.Length - 1);
            if (picker != null) picker.SetActive(false);
            Apply();
        }

        /// The HUD pill: open or close the picker.
        public void Toggle()
        {
            if (picker == null) return;
            if (PickerOpen) UiMotion.Hide(picker); else UiMotion.Show(picker, picker.transform);
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
        }

        /// One of the picker's options.
        public void Set(int index)
        {
            Index = Mathf.Clamp(index, 0, Presets.Length - 1);
            PlayerPrefs.SetInt(Key, Index); PlayerPrefs.Save();
            Apply();
            if (picker != null && PickerOpen) UiMotion.Hide(picker);
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Confirm();
            KartApi.Event("camera", new { preset = Presets[Index].name });
        }

        void Apply()
        {
            var p = Presets[Index];
            if (kartCamera != null)
            {
                kartCamera.initialDist = p.dist;
                kartCamera.initialHeight = p.height;
                kartCamera.lookAtHeight = p.lookAt;
                kartCamera.maxVelDist = p.velDist;
                // The kit's look-back puts the camera ahead of the kart facing it: exactly the "see my driver" view. The
                // field is private and only ever written from legacy input, which this project does not use.
                LookBack?.SetValue(kartCamera, p.front);
            }
            if (pillLabel != null) pillLabel.text = "CAM · " + p.name;
            for (int i = 0; i < optionPills.Length; i++)
                if (optionPills[i] != null) optionPills[i].color = i == Index ? accent : idle;
        }
    }
}
