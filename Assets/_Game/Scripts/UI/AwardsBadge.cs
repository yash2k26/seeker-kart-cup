using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The count of achievements waiting to be claimed, on an Awards button (hidden at none).
    public sealed class AwardsBadge : MonoBehaviour
    {
        [SerializeField] GameObject pill;
        [SerializeField] TMP_Text count;

        void OnEnable() { Achievements.Changed += Show; Show(); }
        void OnDisable() => Achievements.Changed -= Show;

        public void Show()
        {
            int n = Achievements.ClaimableCount;
            if (pill != null) pill.SetActive(n > 0);
            if (count != null) count.text = n > 9 ? "9+" : n.ToString();
        }
    }
}
