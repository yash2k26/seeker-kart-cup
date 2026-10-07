using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The title's chest: the next one waiting (with how many), or the golden chest the stars are filling toward, and
    /// the star meter under it. Tap: open what is waiting (ChestReveal), or see how chests are earned.
    public sealed class ChestButton : MonoBehaviour
    {
        [SerializeField] Image chest, meterFill;
        [SerializeField] TMP_Text count, meter;
        [SerializeField] GameObject countBadge;
        [SerializeField] Sprite[] closedSprites = System.Array.Empty<Sprite>();

        Tween bob;

        void OnEnable() { Chests.Changed += Show; Show(); }
        void OnDisable() { Chests.Changed -= Show; bob.Stop(); }

        public void Tap() => ChestReveal.Instance?.OpenNext();

        void Show()
        {
            var next = Chests.Next;
            int tier = (int)(next ?? Chests.Tier.Treasure);
            if (chest != null && tier < closedSprites.Length) { chest.sprite = closedSprites[tier]; chest.color = next == null ? new Color(1, 1, 1, 0.55f) : Color.white; }
            if (countBadge != null) countBadge.SetActive(Chests.Pending > 0);
            if (count != null) count.text = Chests.Pending.ToString();
            if (meter != null) meter.text = $"{Chests.Stars}/{Chests.StarGoal}";
            if (meterFill != null) meterFill.fillAmount = Chests.StarGoal > 0 ? (float)Chests.Stars / Chests.StarGoal : 0f;
            // something waiting: it bobs, so the title says "open me" without a word
            bob.Stop();
            if (chest != null) chest.transform.localPosition = Vector3.zero;
            if (next != null && chest != null && Application.isPlaying)
                bob = Tween.LocalPositionY(chest.transform, 10f, 0.7f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
        }

        /// Editor-side preview (OnEnable's tween never runs in edit mode).
        public void ShowInEditor() => Show();
    }
}
