using TMPro;
using UnityEngine;

namespace KartCup
{
    /// Out of fuel (or the chip tapped): how full the tank is, when the next can comes, that a win hands its can back,
    /// and FILL UP for gems. A race that could not start opens it (ScreenFlow), and a PLAY AGAIN with an empty tank
    /// opens it on the way back to the menu (OpenOnMenu).
    public sealed class FuelPanel : MonoBehaviour
    {
        public static FuelPanel Instance { get; private set; }
        /// Set by the race scene: show the panel as the menu comes up.
        public static bool OpenOnMenu;

        [SerializeField] GameObject content;
        [SerializeField] TMP_Text title, body, fillLabel;
        float next;

        void Awake() { Instance = this; if (content != null) content.SetActive(false); }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start() { if (OpenOnMenu) { OpenOnMenu = false; Show(); } }

        public static void Open() => Instance?.Show();

        public void Show()
        {
            if (content == null) return;
            Refresh();
            UiMotion.Show(content, content.transform.childCount > 1 ? content.transform.GetChild(1) : null);
            Haptics.Play(Haptics.Kind.Tick);
        }

        public void Close() { if (content != null && content.activeSelf) UiMotion.Hide(content); }

        /// FILL UP: a full tank for gems, or the gem shop when there are not enough.
        public void Fill()
        {
            if (Fuel.Full) { Close(); return; }
            if (Fuel.Fill()) { AudioDirector.Instance?.Confirm(); Close(); return; }
            AudioDirector.Instance?.Error();
            Close();
            ScreenFlow.Instance?.ShowGems();
        }

        void Update()
        {
            if (content == null || !content.activeSelf || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            Refresh();
        }

        void Refresh()
        {
            int c = Fuel.Count;
            if (title != null) title.text = c == 0 ? "OUT OF FUEL" : "FUEL";
            if (body != null)
                body.text = (c >= Fuel.Max ? "The tank is full." : $"{c} of {Fuel.Max} cans.  The next one comes in {Fuel.NextIn()}.")
                            + $"\nEvery race takes a can; one comes back every {Fuel.RefillSeconds / 60} minutes, and a win hands its can back.";
            if (fillLabel != null) fillLabel.text = c >= Fuel.Max ? "OK" : $"FILL UP  ·  {Fuel.FillPrice} GEMS";
        }

        public void ShowInEditor() { if (content != null) { Refresh(); content.SetActive(true); } }
        public void HideInEditor() { if (content != null) content.SetActive(false); }
    }
}
