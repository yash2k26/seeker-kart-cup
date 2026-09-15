using PowerslideKartPhysics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Lap, position, clock, countdown and the boost meter, in the same type and palette as the menus.
    public sealed class RaceHud : MonoBehaviour
    {
        [SerializeField] TMP_Text lapLabel, positionLabel, timeLabel, bigLabel, itemLabel;
        [SerializeField] Image boostFill;
        [SerializeField] float bigLabelSeconds = 0.9f;

        RaceManager race;
        Kart playerKart;
        ItemCaster playerItems;
        float bigUntil;

        void Start()
        {
            race = RaceManager.Instance;
            if (race == null) return;
            race.onCountdown.AddListener(n => Big(n > 0 ? n.ToString() : "GO!"));
            race.onFinish.AddListener(p => { if (p.isPlayer) Big("FINISH"); });
        }

        void Big(string s)
        {
            if (bigLabel == null) return;
            bigLabel.text = s;
            bigUntil = Time.time + bigLabelSeconds;
            bigLabel.transform.localScale = Vector3.one * 1.4f;
        }

        void Update()
        {
            if (race == null) return;
            if (race.Player == null) { race.Rescan(); return; }
            var p = race.Player;
            if (playerKart == null) { playerKart = p.GetComponent<Kart>(); playerItems = p.GetComponent<ItemCaster>(); }

            if (lapLabel != null) lapLabel.text = $"LAP {Mathf.Clamp(p.lap, 1, race.totalLaps)}/{race.totalLaps}";
            if (positionLabel != null) positionLabel.text = Ordinal(race.PositionOf(p));
            if (timeLabel != null) timeLabel.text = Clock(race.raceTime);
            if (itemLabel != null) itemLabel.text = playerItems != null && playerItems.item != null ? playerItems.item.itemName.ToUpperInvariant() : "";
            if (boostFill != null && playerKart != null)
                boostFill.fillAmount = playerKart.maxBoosts > 0 ? Mathf.Clamp01((float)playerKart.boostCount / playerKart.maxBoosts) : 0f;

            if (bigLabel != null)
            {
                bool show = Time.time < bigUntil;
                bigLabel.enabled = show;
                if (show) bigLabel.transform.localScale = Vector3.Lerp(bigLabel.transform.localScale, Vector3.one, Time.deltaTime * 12f);
            }
        }

        public static string Clock(float t) => $"{Mathf.FloorToInt(t / 60f)}:{t % 60f:00.00}";
        public static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => n + "th" };
    }
}
