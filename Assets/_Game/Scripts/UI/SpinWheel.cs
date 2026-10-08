using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The daily wheel on the title (DailySpin): a tile that says whether today's spin is waiting, and the wheel over
    /// the title when it is tapped. SPIN turns it a few times and eases onto the slice DailySpin picked; the prize is
    /// named and already paid. Tapping the dim closes it.
    public sealed class SpinWheel : MonoBehaviour
    {
        [SerializeField] GameObject overlay;
        [SerializeField] RectTransform wheel;
        [SerializeField] TMP_Text result, tileLine, spinLabel;
        [SerializeField] Button spinButton;
        [SerializeField] GameObject readyDot;
        bool spinning;
        float nextTile;

        void OnEnable() { if (overlay != null) overlay.SetActive(false); Tile(); }
        void Update() { if (Time.unscaledTime > nextTile) { nextTile = Time.unscaledTime + 20f; Tile(); } }

        void Tile()
        {
            bool ready = DailySpin.Ready;
            if (tileLine != null) tileLine.text = ready ? "FREE SPIN" : Daily.NextIn();
            if (readyDot != null) readyDot.SetActive(ready);
        }

        /// The title's tile.
        public void Open()
        {
            if (overlay == null) return;
            overlay.transform.SetAsLastSibling();                // over everything on the title (the toast too)
            UiMotion.Show(overlay, overlay.transform.childCount > 1 ? overlay.transform.GetChild(1) : overlay.transform);
            bool ready = DailySpin.Ready;
            if (result != null) result.text = ready ? "One free spin a day. Every slice is as likely." : $"Spun today. The next spin is in {Daily.NextIn()}.";
            if (spinButton != null) spinButton.interactable = ready;
            if (spinLabel != null) spinLabel.text = ready ? "SPIN" : "COME BACK";
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
        }

        public void Close() { if (!spinning && overlay != null) UiMotion.Hide(overlay); }

        /// Previews: the wheel open, turned to a slice, its prize named.
        public void ShowInEditor(int landOn)
        {
            if (overlay != null) { overlay.transform.SetAsLastSibling(); overlay.SetActive(true); }
            if (wheel != null) wheel.localEulerAngles = new Vector3(0, 0, landOn * 360f / DailySpin.Wheel.Length);
            if (result != null) result.text = $"<color=#FFB126>{DailySpin.Wheel[landOn].label}</color>   ·   waiting in your chests";
            if (spinLabel != null) spinLabel.text = "SPIN";
        }
        public void HideInEditor() { if (overlay != null) overlay.SetActive(false); Tile(); }

        public void Spin()
        {
            if (spinning || wheel == null) return;
            int i = DailySpin.Spin();
            if (i < 0) return;
            spinning = true;
            if (spinButton != null) spinButton.interactable = false;
            int n = DailySpin.Wheel.Length;
            // slice i sits at angle i * 360/n clockwise from the top; the wheel turns so that slice ends under the pointer
            float target = 360f * 5f + i * 360f / n + Random.Range(-360f / n * 0.3f, 360f / n * 0.3f);
            float from = wheel.localEulerAngles.z % 360f;
            Tween.Custom(from, from + target, 3.6f, z => wheel.localEulerAngles = new Vector3(0, 0, z), Ease.OutQuart, useUnscaledTime: true)
                 .OnComplete(() => Landed(i));
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Confirm();
        }

        void Landed(int i)
        {
            spinning = false;
            var p = DailySpin.Wheel[i];
            if (result != null)
            {
                result.text = p.kind == DailySpin.Kind.Chest ? $"<color=#FFB126>{p.label}</color>   ·   waiting in your chests"
                            : p.kind == DailySpin.Kind.Stars ? $"<color=#FFB126>{p.label}</color>   ·   into the star meter"
                            : $"<color=#FFB126>{p.label}</color>   ·   in your items for the next race";
                UiMotion.Punch(result.transform, 1.15f, 0.3f);
            }
            if (spinLabel != null) spinLabel.text = "COME BACK";
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm();
            Tile();
        }
    }
}
