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
        [SerializeField] TMP_Text ammoLabel;       // "×3": a box gives three shots, and the slot never said so
        [SerializeField] GameObject awardPlate;   // an achievement just done: a banner under the race bar
        // the driver's special (Ability): its button, its name, the charge filling under it
        [SerializeField] Image abilityButton, abilityFill;
        [SerializeField] TMP_Text abilityLabel;
        Ability playerAbility; bool abilityWasReady;
        [SerializeField] TMP_Text awardLabel;
        float awardUntil;
        [SerializeField] string[] glyphNames = System.Array.Empty<string>();
        [SerializeField] Sprite[] glyphs = System.Array.Empty<Sprite>();
        TMP_Text itemWord; Color itemWordIdle;
        string heldName;

        RaceManager race;
        Kart playerKart;
        ItemCaster playerItems;
        float bigUntil, wrongSince = -1f;
        // The roulette (Beach Buggy's): a pickup spins through the glyphs before it lands. Tapping ITEM stops it early.
        const float RollSeconds = 0.75f;
        float rollUntil, nextFlip; bool rolling, wasArmed; int flips;
        public bool Rolling => rolling;
        public void StopRoulette() => rollUntil = 0f;
        int lastPosition, lastLap, lastTier;
        bool itemArmed;

        public static RaceHud Instance { get; private set; }
        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; Achievements.Unlocked -= Award; Ability.Used -= OnAbility; }

        /// The ability button.
        public void FireAbility()
        {
            if (playerAbility == null) return;
            if (!playerAbility.Fire()) Haptics.Play(Haptics.Kind.Tick);   // not charged yet (OnAbility says the rest)
        }

        /// Anyone's special: yours in big gold letters; a rival's near you as a line. Both say what it DOES (the def's
        /// line), so a player who has never read the driver screen still knows why they just spun.
        void OnAbility(Ability a)
        {
            if (a.isPlayer)
            {
                Big($"<color=#FFB126>{a.def.name}!</color>", 1.1f);
                Banner(a.def.line, 1.6f);
                AudioDirector.Instance?.Confirm();
                return;
            }
            var p = race != null ? race.Player : null;
            if (p == null || (a.transform.position - p.transform.position).sqrMagnitude > 70f * 70f) return;
            var who = a.GetComponent<KartProgress>();
            Banner($"{(who != null ? who.racerName.ToUpperInvariant() : "RIVAL")}  ·  <color=#FFB126>{a.def.name}</color>:  " +
                   a.def.line.ToLowerInvariant(), 2f);
        }

        void AbilityButton()
        {
            if (abilityButton == null) return;
            if (playerAbility == null && race.Player != null) playerAbility = race.Player.GetComponent<Ability>();
            bool show = playerAbility != null && race.state != RaceManager.State.Finished;
            if (abilityButton.gameObject.activeSelf != show) abilityButton.gameObject.SetActive(show);
            if (!show) return;
            if (abilityLabel != null && abilityLabel.text != playerAbility.def.name) abilityLabel.text = playerAbility.def.name;
            bool ready = playerAbility.Ready;
            if (abilityFill != null) abilityFill.fillAmount = playerAbility.charge01;
            var c = ready ? accent : idle; abilityButton.color = new Color(c.r, c.g, c.b, abilityButton.color.a);
            if (abilityLabel != null) abilityLabel.color = ready ? onAccent : new Color(0.96f, 0.94f, 0.9f);
            if (ready && !abilityWasReady) { UiMotion.Punch(abilityButton.transform, 1.15f, 0.3f); Haptics.Play(Haptics.Kind.TierUp); }
            abilityWasReady = ready;
        }

        void Award(Achievements.Def d) => Banner($"<color=#FFB126>ACHIEVEMENT</color>   {d.title.ToUpperInvariant()}   ·   + {Chests.Names[(int)d.reward]}");

        /// A line on the award plate for a few seconds (an achievement, a gold paint earned).
        public void Banner(string text, float seconds = 3.2f)
        {
            if (awardPlate == null || awardLabel == null) return;
            if (awardPlate.activeSelf && awardLabel.text.Contains("ACHIEVEMENT") && !text.Contains("ACHIEVEMENT")) return;   // an award is not cut short
            awardLabel.text = text;
            awardPlate.SetActive(true);
            UiMotion.Punch(awardPlate.transform, 1.12f, 0.3f);
            awardUntil = Time.unscaledTime + seconds;
            AudioDirector.Instance?.Confirm();
        }

        /// A big word in the middle of the screen for a while (Elimination's knockouts).
        public void Callout(string s, float seconds) => Big(s, seconds);

        void Start()
        {
            Achievements.Unlocked += Award;
            Ability.Used += OnAbility;
            if (awardPlate != null) awardPlate.SetActive(false);
            race = RaceManager.Instance;
            if (race == null) return;
            race.onCountdown.AddListener(n => Big(n > 0 ? n.ToString() : "GO!"));
            // The finish says where you came, Beach Buggy's way: a big "4TH", or a gold "1ST!" (in the order karts crossed)
            race.onFinish.AddListener(p =>
            {
                if (!p.isPlayer) return;
                // online the order is the room's to say (other phones' karts do not report crossing here): just FINISH
                if (Elimination.Instance != null || RoomClient.InRaceRoom) { Big("FINISH"); return; }
                int place = 0; foreach (var r in race.racers) if (r.finished && r.finishTime <= p.finishTime) place++;
                // high on the screen: the camera swings round to the driver's face, and the word must not sit on it
                if (bigLabel != null) bigLabel.rectTransform.anchoredPosition = new Vector2(0f, 330f);
                Big(place <= 1 ? "<color=#FFB126>1ST!</color>" : Ordinal(place).ToUpperInvariant(), 3.2f);
                // the race is over: what the thumbs used is put away (the buttons go with ResultsPanel)
                if (itemLabel != null) itemLabel.gameObject.SetActive(false);
                if (ammoLabel != null) ammoLabel.gameObject.SetActive(false);
                if (boostFill != null && boostFill.transform.parent != null) boostFill.transform.parent.gameObject.SetActive(false);
            });
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
            if (lapLabel != null) lapLabel.text = Elimination.Instance != null && race.state == RaceManager.State.Racing
                ? $"OUT IN {Mathf.CeilToInt(Elimination.Instance.SecondsToNext)}"
                : SmashUp.Instance != null ? $"SMASHED {SmashUp.Score}/{SmashUp.Need(3)}" : $"LAP {lap}/{race.totalLaps}";
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
            if (itemLabel != null && !rolling) itemLabel.text = armed ? (held == "Homing Item" ? "HOMING" : held.ToUpperInvariant()) : "";
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

            Roulette(armed, held);
            if (ammoLabel != null) ammoLabel.text = armed && !rolling && playerItems.ammo > 1 ? $"×{playerItems.ammo}" : "";

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
            AbilityButton();
            if (awardPlate != null && awardPlate.activeSelf && Time.unscaledTime > awardUntil) awardPlate.SetActive(false);
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

        void Roulette(bool armed, string held)
        {
            if (armed && !wasArmed && glyphs.Length > 1) { rollUntil = Time.time + RollSeconds; nextFlip = 0f; rolling = true; flips = 0; }
            wasArmed = armed;
            if (!rolling || itemIcon == null) return;
            if (!armed) { rolling = false; return; }
            if (Time.time < rollUntil)
            {
                if (Time.time < nextFlip) return;
                nextFlip = Time.time + 0.07f;
                var next = glyphs[Random.Range(0, glyphs.Length)];
                if (next == itemIcon.sprite) next = glyphs[(System.Array.IndexOf(glyphs, next) + 1) % glyphs.Length];
                itemIcon.sprite = next; itemIcon.enabled = true;
                if (itemWord != null) itemWord.enabled = false;
                if (itemLabel != null) itemLabel.text = "";
                if (++flips % 2 == 0) AudioDirector.Instance?.Click();
                return;
            }
            rolling = false;                                         // it lands: the real item, a punch and a chime
            var glyph = Glyph(held);
            itemIcon.sprite = glyph; itemIcon.enabled = glyph != null;
            if (itemWord != null) itemWord.enabled = glyph == null;
            UiMotion.Punch(itemButton.transform, 1.2f, 0.3f);
            AudioDirector.Instance?.Confirm();
            Haptics.Play(Haptics.Kind.Tick);
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
