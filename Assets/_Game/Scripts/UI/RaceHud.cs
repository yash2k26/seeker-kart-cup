using PowerslideKartPhysics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Lap, position, clock, countdown, drift-boost meter and the big callouts, in the same type and palette as the menus.
    public sealed class RaceHud : MonoBehaviour
    {
        [SerializeField] TMP_Text lapLabel, positionLabel, timeLabel, bigLabel, itemLabel;
        [SerializeField] Image boostFill;
        [SerializeField] Image itemButton;          // lights up only while an item is armed: a state, not chrome
        [SerializeField] float bigLabelSeconds = 0.9f;
        // Three drift tiers, three colours, the Mario Kart read: sand, coral, paper.
        [SerializeField] Color[] boostTiers = { new(1f, 0.353f, 0.212f), new(1f, 0.690f, 0.227f), new(1f, 1f, 1f) };
        // Design.Accent, OnAccent and Ink700: the armed button turns gold, and its word violet with it.
        [SerializeField] Color accent = new(1f, 0.694f, 0.149f), onAccent = new(0.09f, 0.02f, 0.251f);
        [SerializeField] Color idle = new(0.18f, 0.043f, 0.439f);
        // What the slot shows while an item is held: its glyph (Design's), by ItemManager name.
        [SerializeField] Image itemIcon;
        [SerializeField] string[] glyphNames = System.Array.Empty<string>();
        [SerializeField] Sprite[] glyphs = System.Array.Empty<Sprite>();
        TMP_Text itemWord; Color itemWordIdle;
        string heldName;

        RaceManager race;
        Kart playerKart;
        ItemCaster playerItems;
        float bigUntil, wrongSince = -1f;
        int lastPosition, lastLap, lastTier;
        bool itemArmed;

        void Start()
        {
            race = RaceManager.Instance;
            if (race == null) return;
            race.onCountdown.AddListener(n => Big(n > 0 ? n.ToString() : "GO!"));
            race.onFinish.AddListener(p => { if (p.isPlayer) Big("FINISH"); });
        }

        void Big(string s, float seconds = -1f)
        {
            if (bigLabel == null) return;
            if (!bigLabel.enabled || bigLabel.text != s)
            {
                bigLabel.text = s;
                bigLabel.enabled = true;
                UiMotion.Punch(bigLabel.transform, 1.4f, 0.35f);
            }
            // Scaled time, like the countdown that drives it: on unscaled time a pause during 3-2-1 ate the digit.
            bigUntil = Time.time + (seconds < 0 ? bigLabelSeconds : seconds);
        }

        void Update()
        {
            if (race == null) return;
            if (race.Player == null) { race.Rescan(); return; }
            var p = race.Player;
            if (playerKart == null) { playerKart = p.GetComponent<Kart>(); playerItems = p.GetComponent<ItemCaster>(); }

            int lap = Mathf.Clamp(p.lap, 1, race.totalLaps);
            if (lapLabel != null) lapLabel.text = $"LAP {lap}/{race.totalLaps}";
            if (lap != lastLap)
            {
                if (lastLap > 0 && lap == race.totalLaps && race.totalLaps > 1) Big("FINAL LAP", 1.4f);
                lastLap = lap;
            }

            int position = race.PositionOf(p);
            if (positionLabel != null) positionLabel.text = Ordinal(position);
            if (position != lastPosition)
            {
                if (lastPosition > 0 && race.state == RaceManager.State.Racing)
                {
                    UiMotion.Punch(positionLabel.transform);
                    if (position < lastPosition) { Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.PositionUp(); }
                }
                lastPosition = position;
            }

            if (timeLabel != null) timeLabel.text = Clock(race.raceTime);
            bool armed = playerItems != null && playerItems.item != null && playerItems.ammo > 0;   // a fired item is no longer held
            string held = armed ? playerItems.item.itemName : null;
            if (itemLabel != null) itemLabel.text = armed ? (held == "Homing Item" ? "HOMING" : held.ToUpperInvariant()) : "";
            if (itemButton != null && (armed != itemArmed || held != heldName))
            {
                var c = armed ? accent : idle;
                itemButton.color = new Color(c.r, c.g, c.b, itemButton.color.a);
                if (itemWord == null) { itemWord = itemButton.GetComponentInChildren<TMP_Text>(); if (itemWord != null) itemWordIdle = itemWord.color; }
                var glyph = armed ? Glyph(held) : null;
                if (itemIcon != null) { itemIcon.sprite = glyph; itemIcon.enabled = glyph != null; }
                if (itemWord != null) { itemWord.color = armed ? onAccent : itemWordIdle; itemWord.enabled = glyph == null; }   // the glyph replaces the word
                if (armed) UiMotion.Punch(itemButton.transform, 1.12f, 0.24f);
                itemArmed = armed; heldName = held;
            }

            if (boostFill != null && playerKart != null)
            {
                // Charging while drifting (tier + progress to the next), spending after release.
                // boostReserve is seconds of boost left, not a fraction — it reaches maxBoosts, so normalise it or the
                // bar sits pinned at full for the whole boost.
                float charge = playerKart.drifting
                    ? (playerKart.boostCount + playerKart.GetBoostValue()) / Mathf.Max(1, playerKart.maxBoosts)
                    : playerKart.boostReserve / Mathf.Max(0.01f, playerKart.maxBoosts * playerKart.boostPower);
                boostFill.fillAmount = Mathf.Clamp01(charge);
                if (boostTiers.Length > 0) boostFill.color = boostTiers[Mathf.Clamp(playerKart.boostCount, 0, boostTiers.Length - 1)];
                // Compare the UNCLAMPED count: maxBoosts is 3 and there are 3 colours, so clamping hid the top tier-up.
                if (playerKart.drifting && playerKart.boostCount > lastTier) { Haptics.Play(Haptics.Kind.TierUp); AudioDirector.Instance?.TierUp(playerKart.boostCount); }
                lastTier = playerKart.drifting ? playerKart.boostCount : 0;
            }

            WrongWay(p);
            if (bigLabel != null && bigLabel.enabled && Time.time >= bigUntil) bigLabel.enabled = false;
        }

        /// Only after a full second of driving away from the next gate, so a spin or a wide line never flickers it.
        void WrongWay(KartProgress p)
        {
            // Paused, the dwell test stays true and would re-arm the callout every frame, holding it lit through the pause.
            if (Time.timeScale == 0f) return;
            if (race.state != RaceManager.State.Racing || race.checkpoints.Length == 0 || playerKart == null || playerKart.rb == null) { wrongSince = -1f; return; }
            // Compare against the TRACK's heading where the kart actually is, blended between the gate just passed
            // and the one ahead. Each gate stores the centreline direction at its own position, so this follows the
            // corner instead of pointing across it.
            int n = race.checkpoints.Length;
            var from = race.checkpoints[Mathf.Clamp(p.checkpointIndex, 0, n - 1)].transform;
            var to = race.checkpoints[(p.checkpointIndex + 1 + n) % n].transform;
            float span = Vector3.Distance(from.position, to.position);
            float along = span > 1f ? Mathf.Clamp01(1f - Vector3.Distance(p.transform.position, to.position) / span) : 0f;
            var heading = Vector3.Slerp(from.forward, to.forward, along);
            heading.y = 0f;

            var vel = playerKart.rb.linearVelocity; vel.y = 0f;
            bool wrong = vel.magnitude > 6f && heading.sqrMagnitude > 0.01f
                         && Vector3.Dot(vel.normalized, heading.normalized) < -0.45f;
            if (!wrong) { wrongSince = -1f; return; }
            if (wrongSince < 0f) wrongSince = Time.time;
            if (Time.time - wrongSince > 1.2f) { Big("WRONG WAY", 0.2f); AudioDirector.Instance?.Warn(); }
        }

        Sprite Glyph(string item)
        {
            int i = System.Array.IndexOf(glyphNames, item);
            return i >= 0 && i < glyphs.Length ? glyphs[i] : null;
        }

        /// Tabular figures. Kenney Future is proportional, so a running clock jitters sideways as the digits change
        /// and the times in a results column never line up. mspace is TMP's monospacing, applied everywhere a number counts.
        public static string Clock(float t) => $"<mspace=0.56em>{Mathf.FloorToInt(t / 60f)}:{t % 60f:00.00}</mspace>";
        public static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => n + "th" };
    }
}
