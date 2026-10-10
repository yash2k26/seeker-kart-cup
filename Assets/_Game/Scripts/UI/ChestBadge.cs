using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// On the results card: the chest this race earned (a podium place) and its stars, with OPEN; a full star meter
    /// says so. Off the podium it says what a podium would have given. Hidden in the time trial and in paid rooms.
    /// Online the room's verdict decides the place after the card is already up, so it redraws when a chest lands.
    public sealed class ChestBadge : MonoBehaviour
    {
        [SerializeField] GameObject content, openButton;
        [SerializeField] Image chest;
        [SerializeField] TMP_Text label, stars;
        [SerializeField] Sprite[] closedSprites = System.Array.Empty<Sprite>();

        Chests.Tier? shown;

        void OnEnable() { shown = null; Chests.Changed += Refresh; Refresh(); }
        void OnDisable() => Chests.Changed -= Refresh;

        void Refresh()
        {
            var (tier, gained, filled) = Chests.LastEarn;
            bool show = !Weekly.Racing && !(RoomClient.Instance != null && RoomClient.Instance.Room != null && RoomClient.Instance.Room.IsPaid);
            if (content != null) content.SetActive(show);
            if (!show) return;
            if (stars != null) stars.text = (tier == null ? $"STARS  {Chests.Stars}/{Chests.StarGoal}" : $"+{gained} STARS  ·  {Chests.Stars}/{Chests.StarGoal}")
                                            + (Gems.LastPodium > 0 && !TourCard ? $"  ·  +{Gems.LastPodium} GEMS" : "");
            if (openButton != null) openButton.SetActive(tier != null && Chests.Pending > 0 && ChestReveal.Instance != null && !ChestReveal.Instance.Showing);
            if (chest != null) chest.enabled = tier != null;
            if (label != null) label.text = tier == null ? "Podium finishes earn chests" : filled ? "+ GOLDEN TREASURE CHEST!" : Chests.Names[(int)tier.Value];
            if (tier == null || tier == shown) return;
            shown = tier;
            chest.sprite = closedSprites[(int)tier.Value];
            if (!Application.isPlaying) return;
            var t = chest.transform;
            t.localScale = Vector3.zero;
            Tween.Scale(t, Vector3.one, 0.45f, Ease.OutBack, startDelay: 0.35f, useUnscaledTime: true);
        }

        /// A series race's card shows its gems in its own reward line (EventResult): not twice.
        static bool TourCard => RaceMode.InEvent && RaceMode.EventId != "daily" && !Weekly.Racing;

        public void Open() { ChestReveal.Instance?.OpenNext(); if (openButton != null) openButton.SetActive(false); }
    }
}
