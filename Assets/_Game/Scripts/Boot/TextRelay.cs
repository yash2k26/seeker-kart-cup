using UnityEngine;

namespace KartCup
{
    /// Gate-scene helper: lets a UnityEvent<string> drive a UI Text.
    public sealed class TextRelay : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Text label;
        public void Set(string s) { if (label) label.text = s; }
    }
}
