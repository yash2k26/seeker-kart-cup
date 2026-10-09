using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Opening chests: its own canvas over everything, kept across scenes once the menu has run (as the loading
    /// curtain), so the results card can open the chest it just earned and the title can open the rest.
    /// The ceremony: the chest drops in with its name (the tier is already decided, and shown: no fake climbs, no near
    /// misses), three taps shake it harder with its colour leaking out, it bursts open, and what is inside flips over a
    /// card at a time, the best last. With nothing to open it shows how chests are earned, three tiles to glance at, and
    /// the drop rates one tap further.
    public sealed class ChestReveal : MonoBehaviour
    {
        public static ChestReveal Instance { get; private set; }

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform chest, glow;
        [SerializeField] Image chestImage, glowImage;
        [SerializeField] TMP_Text tierLabel, hint, info, collectLabel;
        [SerializeField] GameObject collectButton, infoRoot, howTo;   // howTo: the three tiles; info: the drop rates
        [SerializeField] TMP_Text ratesLabel;
        [SerializeField] GameObject[] cards = System.Array.Empty<GameObject>();
        [SerializeField] Image[] cardIcons = System.Array.Empty<Image>(), cardEdges = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] cardLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] Sprite[] closedSprites = System.Array.Empty<Sprite>(), openSprites = System.Array.Empty<Sprite>();
        [SerializeField] Color[] tierColors = System.Array.Empty<Color>();
        [SerializeField] string[] itemIds = System.Array.Empty<string>();
        [SerializeField] Sprite[] itemIcons = System.Array.Empty<Sprite>();
        [SerializeField] Sprite kartIcon, paintIcon;
        [SerializeField] KartDatabase karts;
        [SerializeField] RectTransform rays;                 // the beams behind an opened chest, turning slowly
        [SerializeField] Image raysImage;
        [SerializeField] RectTransform[] coins = System.Array.Empty<RectTransform>(), sparkles = System.Array.Empty<RectTransform>();

        int taps;
        bool busy, opened;
        Image back; Color backColor;          // the dim backdrop: it flashes the chest's colour on the burst
        float raySpin = 18f;                  // deg/s; the burst spins the beams up and they settle
        Tween hintPulse;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Chests.Karts = karts;
            back = transform.Find("Back")?.GetComponent<Image>();
            if (back != null) backColor = back.color;
            Hide(instant: true);
        }

        public bool Showing => group != null && group.blocksRaycasts;

        void Update()
        {
            raySpin = Mathf.Lerp(raySpin, 18f, 1f - Mathf.Exp(-2.2f * Time.unscaledDeltaTime));
            if (rays != null && rays.gameObject.activeSelf) rays.Rotate(0f, 0f, -raySpin * Time.unscaledDeltaTime);
            // the sparkles twinkle round an opened chest, each on its own beat
            for (int i = 0; i < sparkles.Length; i++)
                if (sparkles[i] != null && sparkles[i].gameObject.activeSelf)
                    sparkles[i].localScale = Vector3.one * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * (2.2f + i * 0.37f) + i * 1.7f));
        }

        /// Open what is waiting, one after another; with nothing waiting, how chests work.
        public void OpenNext()
        {
            Chests.Karts = karts;
            group.alpha = 0f; group.blocksRaycasts = true;
            Tween.Alpha(group, 1f, 0.2f, useUnscaledTime: true);
            if (Chests.Pending == 0) { ShowInfo(); return; }
            StopAllCoroutines();
            StartCoroutine(Present());
        }

        public void Close() { if (!busy) Hide(); }

        void Hide(bool instant = false)
        {
            if (group == null) return;
            group.blocksRaycasts = false;
            if (instant) group.alpha = 0f; else Tween.Alpha(group, 0f, 0.2f, useUnscaledTime: true);
        }

        void ShowInfo()
        {
            infoRoot.SetActive(true);
            chest.gameObject.SetActive(true); glow.gameObject.SetActive(false);
            Effects(false, 0);
            SetCards(0);
            chestImage.sprite = closedSprites[(int)Chests.Tier.Treasure];
            chest.localScale = Vector3.one * 0.7f; chest.anchoredPosition = new Vector2(0, 250);
            tierLabel.color = tierColors[(int)Chests.Tier.Treasure];
            hint.text = "";
            // a table: the chests across, the podium places down
            var o = Chests.Odds;
            static string Col(int t) => $"<pos={150 + t * 230}>";
            var rows = "<color=#B4A7DE>" + Col(0) + "WOOD" + Col(1) + "SILVER" + Col(2) + "GOLD" + Col(3) + "GOLDEN</color>";
            for (int p = 0; p < o.Length; p++)
            {
                rows += "\n" + new[] { "1ST", "2ND", "3RD" }[p];
                for (int t = 0; t < o[p].Length; t++) rows += Col(t) + (o[p][t] > 0 ? $"{o[p][t]}%" : "—");
            }
            info.text = rows + $"\n<align=center><size=80%><color=#B4A7DE>No gold in {Chests.GoldPity - 1} chests? The next one is gold.  ·  Earned, never sold, no cash value.</color></size>";
            Rates(false);
            collectButton.SetActive(true); collectLabel.text = "GOT IT";
        }

        /// The info page's DROP RATES / BACK.
        public void ToggleRates() { Haptics.Play(Haptics.Kind.Tick); Rates(!info.gameObject.activeSelf); }

        void Rates(bool on)
        {
            howTo.SetActive(!on); info.gameObject.SetActive(on);
            tierLabel.text = on ? "DROP RATES" : $"STARS  {Chests.Stars} / {Chests.StarGoal}";
            ratesLabel.text = on ? "BACK" : "DROP RATES";
        }

        IEnumerator Present()
        {
            while (Chests.Next is { } tier)
            {
                busy = true; opened = false; taps = 0;
                infoRoot.SetActive(false);
                SetCards(0);
                collectButton.SetActive(false);
                chestImage.sprite = closedSprites[(int)tier];
                tierLabel.text = Chests.Names[(int)tier];
                tierLabel.color = tierColors[(int)tier];
                hint.text = "TAP TO OPEN";
                glow.gameObject.SetActive(true);
                glowImage.color = Fade(tierColors[(int)tier], 0.15f);
                Effects(false, (int)tier);
                chest.gameObject.SetActive(true);
                chest.localRotation = Quaternion.identity;
                // it falls in, gathering speed, and lands with a thump: a squash, a jolt through the light, dust of its colour
                chest.anchoredPosition = new Vector2(0, 900); chest.localScale = Vector3.one * 0.55f;
                Tween.Scale(chest, Vector3.one, 0.4f, Ease.InQuad, useUnscaledTime: true);
                yield return Tween.UIAnchoredPositionY(chest, 60f, 0.4f, Ease.InQuad, useUnscaledTime: true).ToYieldInstruction();
                Haptics.Play(Haptics.Kind.Land); AudioDirector.Instance?.Hit();
                chest.localScale = new Vector3(1.24f, 0.74f, 1f);
                Tween.Scale(chest, Vector3.one, 0.55f, Ease.OutElastic, useUnscaledTime: true);
                Jolt(12f, 0.3f);
                glowImage.color = Fade(tierColors[(int)tier], 0.4f); glow.localScale = Vector3.one * 0.5f;
                Tween.Scale(glow, Vector3.one, 0.5f, Ease.OutQuad, useUnscaledTime: true);
                Tween.Color(glowImage, Fade(tierColors[(int)tier], 0.15f), 0.7f, useUnscaledTime: true);
                hint.transform.localScale = Vector3.one;
                hintPulse = Tween.Scale(hint.transform, Vector3.one * 1.07f, 0.55f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
                busy = false;
                while (!opened) yield return null;              // Tap() strains it three times, then it bursts

                // what is inside, one card at a time, the best last
                busy = true;
                var got = Chests.Open();
                var rewards = got?.rewards ?? new List<Chests.Reward>();
                hint.text = "";
                int count = Mathf.Min(rewards.Count, cards.Length);
                for (int i = 0; i < count; i++)
                {
                    Card(i, rewards[i]);
                    Place(i, count);
                    var rt = (RectTransform)cards[i].transform;
                    var to = rt.anchoredPosition;
                    // out of the chest: small, askew, flying to its place and landing with a little overshoot
                    rt.anchoredPosition = chest.anchoredPosition + new Vector2(0f, -30f);
                    rt.localScale = Vector3.one * 0.15f; rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-28f, 28f));
                    cards[i].SetActive(true);
                    Tween.UIAnchoredPosition(rt, to, 0.36f, Ease.OutBack, useUnscaledTime: true);
                    Tween.LocalRotation(rt, Vector3.zero, 0.36f, Ease.OutBack, useUnscaledTime: true);
                    Tween.PunchScale(chest, new Vector3(-0.05f, 0.06f, 0f), 0.2f, frequency: 4, useUnscaledTime: true);   // the chest coughs one up
                    bool last = i == count - 1;
                    yield return Tween.Scale(rt, Vector3.one, 0.36f, Ease.OutBack, useUnscaledTime: true).ToYieldInstruction();
                    if (last) { Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm(); Tween.PunchScale(rt, Vector3.one * 0.18f, 0.45f, frequency: 5, useUnscaledTime: true); Tween.PunchScale(glow, Vector3.one * 0.2f, 0.4f, frequency: 4, useUnscaledTime: true); }
                    else { Haptics.Play(Haptics.Kind.Pickup); AudioDirector.Instance?.PositionUp(); }
                    yield return new WaitForSecondsRealtime(last ? 0.2f : 0.05f);
                }
                collectButton.SetActive(true);
                collectLabel.text = Chests.Pending > 0 ? $"NEXT CHEST  ({Chests.Pending})" : "COLLECT";
                busy = false;
                bool next = false;
                void Go() => next = true;
                onCollect = Go;
                while (!next) yield return null;
                onCollect = null;
            }
            Hide();
        }

        System.Action onCollect;

        /// The collect / got-it button.
        public void Collect()
        {
            if (busy) return;
            if (onCollect != null) onCollect();
            else Hide();
        }

        /// A tap anywhere on the overlay: shakes a closed chest (three taps open it).
        public void Tap()
        {
            if (busy || opened || Chests.Next == null || hint.text.Length == 0) return;
            taps++;
            var tier = (int)Chests.Next.Value;
            // the lid strains harder each time: a hop, a squeeze, a rattle, more of its colour leaking out
            Tween.PunchLocalRotation(chest, new Vector3(0, 0, 7f + 7f * taps), 0.35f, frequency: 9 + 3 * taps, useUnscaledTime: true);
            Tween.PunchScale(chest, new Vector3(0.07f * taps, -0.06f * taps, 0f), 0.32f, frequency: 7, useUnscaledTime: true);
            if (taps < 3)
                Tween.UIAnchoredPositionY(chest, 60f + 28f * taps, 0.11f, Ease.OutQuad, useUnscaledTime: true)
                     .Chain(Tween.UIAnchoredPositionY(chest, 60f, 0.26f, Ease.OutBounce, useUnscaledTime: true));
            glowImage.color = Fade(tierColors[tier], 0.15f + taps * 0.25f);
            Tween.Scale(glow, Vector3.one * (1f + taps * 0.14f), 0.25f, Ease.OutQuad, useUnscaledTime: true);
            Tween.PunchScale(glow, Vector3.one * 0.12f, 0.3f, frequency: 4, useUnscaledTime: true);
            Haptics.Play(taps < 3 ? Haptics.Kind.TierUp : Haptics.Kind.Hit); AudioDirector.Instance?.TierUp(taps);
            if (taps == 2) for (int i = 0; i < sparkles.Length; i++) if (sparkles[i] != null) sparkles[i].gameObject.SetActive(i < 3);   // the first glints escape
            hint.text = taps < 3 ? new string('●', taps) + new string('○', 3 - taps) : "";
            if (taps >= 3) StartCoroutine(Burst(tier));
        }

        /// A jolt through the light and the lettering: the chest itself is left to its own tweens.
        void Jolt(float strength, float seconds)
        {
            Tween.ShakeLocalPosition(glow, new Vector3(strength, strength * 0.7f, 0f), seconds, frequency: 14, useUnscaledTime: true);
            if (rays != null && rays.gameObject.activeSelf) Tween.ShakeLocalPosition(rays, new Vector3(strength, strength * 0.7f, 0f), seconds, frequency: 14, useUnscaledTime: true);
            Tween.ShakeLocalPosition(tierLabel.transform, new Vector3(strength * 0.6f, strength * 0.4f, 0f), seconds, frequency: 14, useUnscaledTime: true);
        }

        IEnumerator Burst(int tier)
        {
            busy = true;
            hintPulse.Stop(); hint.transform.localScale = Vector3.one;
            // it draws itself in...
            yield return Tween.Scale(chest, new Vector3(0.84f, 1.16f, 1f), 0.13f, Ease.InQuad, useUnscaledTime: true).ToYieldInstruction();
            // ...and goes off: the lid, a flash of its colour across the screen, a jolt, the beams spinning up
            chestImage.sprite = openSprites[tier];
            chest.localScale = new Vector3(1.32f, 0.72f, 1f);
            Tween.Scale(chest, Vector3.one, 0.6f, Ease.OutElastic, useUnscaledTime: true);
            if (back != null) { back.color = Fade(tierColors[tier], tier >= 2 ? 0.9f : 0.75f); Tween.Color(back, backColor, 0.45f, Ease.OutQuad, useUnscaledTime: true); }
            glowImage.color = Color.white; glow.localScale = Vector3.one * 2.6f;
            Tween.Color(glowImage, Fade(tierColors[tier], 0.95f), 0.35f, useUnscaledTime: true);
            Tween.Scale(glow, Vector3.one * 1.25f, 0.7f, Ease.OutQuad, useUnscaledTime: true);
            Effects(true, tier); raySpin = 220f;
            Jolt(16f + 4f * tier, 0.4f);
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm(); AudioDirector.Instance?.TierUp(4);
            CoinBurst(tier);
            yield return new WaitForSecondsRealtime(0.3f);
            // up it hops to make room for what was inside
            yield return Tween.UIAnchoredPositionY(chest, 236f, 0.26f, Ease.OutQuad, useUnscaledTime: true).ToYieldInstruction();
            Tween.UIAnchoredPositionY(chest, 210f, 0.3f, Ease.OutBounce, useUnscaledTime: true);
            opened = true;
        }

        void SetCards(int n) { for (int i = 0; i < cards.Length; i++) cards[i].SetActive(i < n); }

        /// The light round an opened chest: beams in its colour behind it, sparkles round it.
        void Effects(bool on, int tier)
        {
            if (rays != null)
            {
                rays.gameObject.SetActive(on);
                if (on) { raysImage.color = Fade(tierColors[tier], 0f); Tween.Color(raysImage, Fade(tierColors[tier], tier >= 2 ? 0.8f : 0.5f), 0.4f, useUnscaledTime: true); }
            }
            if (!on) foreach (var c in coins) if (c != null) c.gameObject.SetActive(false);   // CoinBurst lights them
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (sparkles[i] == null) continue;
                sparkles[i].gameObject.SetActive(on && i < 3 + tier * 2);
                var a = i * 2.39996f; float r = 230f + 60f * (i % 3);
                sparkles[i].anchoredPosition = new Vector2(Mathf.Cos(a) * r, 210f + Mathf.Sin(a) * r * 0.7f);
            }
        }

        /// Coins thrown up out of the chest, falling away as they fade: a handful from silver, a shower from the golden.
        void CoinBurst(int tier)
        {
            int n = Mathf.Min(coins.Length, new[] { 0, 6, 12, coins.Length }[tier]);
            for (int i = 0; i < coins.Length; i++)
            {
                var c = coins[i];
                if (c == null) continue;
                c.gameObject.SetActive(i < n);
                if (i >= n) continue;
                c.anchoredPosition = new Vector2(Random.Range(-50f, 50f), 140f);
                c.localScale = Vector3.one * Random.Range(0.6f, 1.15f);
                float t = Random.Range(0.8f, 1.3f), x = Random.Range(-820f, 820f), peak = 140f + Random.Range(180f, 520f);
                Tween.UIAnchoredPositionX(c, x, t, Ease.Linear, useUnscaledTime: true);                     // sideways at a steady speed...
                Tween.UIAnchoredPositionY(c, peak, t * 0.42f, Ease.OutQuad, useUnscaledTime: true)           // ...up, slowing...
                     .Chain(Tween.UIAnchoredPositionY(c, -420f, t * 0.58f, Ease.InQuad, useUnscaledTime: true));   // ...and down, gathering speed: gravity
                Tween.LocalRotation(c, new Vector3(0, 0, Random.Range(-720f, 720f)), t, useUnscaledTime: true);
                var img = c.GetComponent<Image>();
                img.color = Color.white;
                Tween.Alpha(img, 0f, 0.3f, startDelay: t - 0.3f, useUnscaledTime: true);
            }
        }

        /// Rows of four, each row centred on what it holds.
        void Place(int i, int n)
        {
            int row = i / 4, inRow = Mathf.Min(4, n - row * 4);
            ((RectTransform)cards[i].transform).anchoredPosition = new Vector2((i % 4 - (inRow - 1) / 2f) * 404f, -60f - row * 214f);
        }

        void Card(int i, Chests.Reward r)
        {
            if (i < cardLabels.Length) cardLabels[i].text = r.label;
            if (i < cardEdges.Length) cardEdges[i].color = tierColors[(int)r.rarity];
            if (i >= cardIcons.Length) return;
            var icon = cardIcons[i];
            icon.color = Color.white;
            switch (r.kind)
            {
                case "item": int k = System.Array.IndexOf(itemIds, r.id); icon.sprite = k >= 0 && k < itemIcons.Length ? itemIcons[k] : kartIcon; break;
                case "trial": icon.sprite = kartIcon; break;
                case "gems": icon.sprite = paintIcon; icon.color = new Color(1f, 0.69f, 0.15f); break;   // the gold swatch stands for gems
                default: icon.sprite = paintIcon; icon.color = Paints.Find(r.id)?.color ?? Color.white; break;
            }
        }

        static Color Fade(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }

#if UNITY_EDITOR
        /// MenuPreview: the overlay as it looks with `tier` open and `rewards` out, or (rewards null) the info page.
        public void PreviewInEditor(Chests.Tier tier, List<Chests.Reward> rewards)
        {
            group.alpha = 1f;
            if (rewards == null) { ShowInfo(); return; }
            infoRoot.SetActive(false);
            chestImage.sprite = openSprites[(int)tier]; chest.anchoredPosition = new Vector2(0, 210);
            glow.gameObject.SetActive(true); glowImage.color = Fade(tierColors[(int)tier], 0.9f); glow.localScale = Vector3.one * 1.2f;
            tierLabel.text = Chests.Names[(int)tier]; tierLabel.color = tierColors[(int)tier]; hint.text = "";
            for (int i = 0; i < cards.Length; i++) { cards[i].SetActive(i < rewards.Count); if (i < rewards.Count) { Card(i, rewards[i]); Place(i, rewards.Count); } }
            collectButton.SetActive(true); collectLabel.text = "COLLECT";
            Effects(true, (int)tier);
            var rnd = new System.Random(3);
            for (int i = 0; i < coins.Length; i++)                 // the shower, caught mid-air
            {
                coins[i].gameObject.SetActive(i < new[] { 0, 6, 12, coins.Length }[(int)tier]);
                coins[i].anchoredPosition = new Vector2(rnd.Next(-720, 720), rnd.Next(-120, 380));
                coins[i].localRotation = Quaternion.Euler(0, 0, rnd.Next(0, 360));
            }
            for (int i = 0; i < sparkles.Length; i++) sparkles[i].localScale = Vector3.one * (0.5f + 0.5f * (i % 2));
        }
#endif
    }
}
