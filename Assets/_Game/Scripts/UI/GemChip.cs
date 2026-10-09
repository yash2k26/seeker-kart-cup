using TMPro;
using UnityEngine;

namespace KartCup
{
    /// A button that reads the gem balance (the title and the driver screen); tapping it opens the shop (wired in MenuSetup).
    public sealed class GemChip : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        void OnEnable() { Gems.Changed += Refresh; Refresh(); }
        void OnDisable() { Gems.Changed -= Refresh; }
        void Refresh() { if (label != null) label.text = $"{Gems.Balance} GEMS"; }
        public void ShowInEditor() => Refresh();
    }
}
