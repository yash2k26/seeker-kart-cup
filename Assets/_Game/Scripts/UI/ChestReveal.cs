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
    /// card at a time, the best last. With nothing to open it shows how chests are earned and their drop rates.
    public sealed class ChestReveal : MonoBehaviour
    {
        public static ChestReveal Instance { get; private set; }

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform chest, glow;
        [SerializeField] Image chestImage, glowImage;
        [SerializeField] TMP_Text tierLabel, hint, info, collectLabel;
        [SerializeField] GameObject collectButton, infoRoot;
        [SerializeField] GameObject[] cards = System.Array.Empty<GameObject>();
        [SerializeField] Image[] cardIcons = System.Array.Empty<Image>(), cardEdges = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] cardLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] Sprite[] closedSprites = System.Array.Empty<Sprite>(), openSprites = System.Array.Empty<Sprite>();
        [SerializeField] Color[] tierColors = System.Array.Empty<Color>();
        [SerializeField] string[] itemIds = System.Array.Empty<string>();
        [SerializeField] Sprite[] itemIcons = System.Array.Empty<Sprite>();
        [SerializeField] Sprite kartIcon, paintIcon;
        [SerializeField] KartDatabase karts;

        int taps;
        bool busy, opened;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Chests.Karts = karts;
            Hide(instant: true);
        }

        public bool Showing => group != null && group.blocksRaycasts;

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
            SetCards(0);
            chestImage.sprite = closedSprites[(int)Chests.Tier.Treasure];
            chest.localScale = Vector3.one * 0.7f; chest.anchoredPosition = new Vector2(0, 250);
            tierLabel.text = $"STARS  {Chests.Stars} / {Chests.StarGoal}";
            tierLabel.color = tierColors[(int)Chests.Tier.Treasure];
            hint.text = "";
            var o = Chests.Odds;
            info.text =
                "Finish on the podium to earn a chest. It opens straight away.\n" +
                $"1st  ·  wood {o[0][0]}%  ·  silver {o[0][1]}%  ·  gold {o[0][2]}%  ·  golden {o[0][3]}%\n" +
                $"2nd  ·  wood {o[1][0]}%  ·  silver {o[1][1]}%  ·  gold {o[1][2]}%\n" +
                $"3rd  ·  wood {o[2][0]}%  ·  silver {o[2][1]}%  ·  gold {o[2][2]}%\n" +
                $"Stars: 3 for 1st, 2 for 2nd, 1 for 3rd. A full meter is a Golden Treasure Chest.\n" +
                $"The {Chests.GoldPity}th chest in a row without a gold is a gold. Kartel Weekly: a silver chest for driving it, a golden one for a prize place.\n" +
                "<size=80%><color=#B4A7DE>Chests are earned, never sold. What is inside is for the game only and has no cash value.</color></size>";
            collectButton.SetActive(true); collectLabel.text = "GOT IT";
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
                chest.gameObject.SetActive(true);
                chest.localScale = Vector3.one; chest.localRotation = Quaternion.identity;
                // it drops in with a thump
                chest.anchoredPosition = new Vector2(0, 900);
                yield return Tween.UIAnchoredPosition(chest, new Vector2(0, 60), 0.45f, Ease.OutBounce, useUnscaledTime: true).ToYieldInstruction();
                Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
                busy = false;
                while (!opened) yield return null;              // Tap() shakes it three times, then opens it

                // what is inside, one card at a time, the best last
                busy = true;
                var got = Chests.Open();
                var rewards = got?.rewards ?? new List<Chests.Reward>();
                hint.text = "";
                for (int i = 0; i < rewards.Count && i < cards.Length; i++)
                {
                    Card(i, rewards[i]);
                    Place(i, Mathf.Min(rewards.Count, cards.Length));
                    var t = cards[i].transform;
                    t.localScale = new Vector3(0f, 1f, 1f);
                    cards[i].SetActive(true);
                    yield return Tween.Scale(t, Vector3.one, 0.22f, Ease.OutBack, useUnscaledTime: true).ToYieldInstruction();
                    Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
                    yield return new WaitForSecondsRealtime(0.12f);
                }
                if (tier >= Chests.Tier.Gold) { Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm(); }
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
            float strength = 6f + taps * 6f;
            Tween.PunchLocalRotation(chest, new Vector3(0, 0, strength), 0.3f, frequency: 12, useUnscaledTime: true);
            glowImage.color = Fade(tierColors[tier], 0.15f + taps * 0.22f);
            Tween.Scale(glow, Vector3.one * (1f + taps * 0.12f), 0.25f, Ease.OutQuad, useUnscaledTime: true);
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
            hint.text = taps < 3 ? new string('●', taps) + new string('○', 3 - taps) : "";
            if (taps >= 3) StartCoroutine(Burst(tier));
        }

        IEnumerator Burst(int tier)
        {
            busy = true;
            yield return new WaitForSecondsRealtime(0.25f);
            chestImage.sprite = openSprites[tier];
            Tween.PunchScale(chest, Vector3.one * 0.25f, 0.4f, frequency: 6, useUnscaledTime: true);
            glowImage.color = Fade(tierColors[tier], 0.95f);
            glow.localScale = Vector3.one * 1.8f;
            Tween.Scale(glow, Vector3.one * 1.2f, 0.6f, Ease.OutQuad, useUnscaledTime: true);
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm();
            yield return Tween.UIAnchoredPosition(chest, new Vector2(0, 210), 0.35f, Ease.OutQuad, useUnscaledTime: true).ToYieldInstruction();
            opened = true;
        }

        void SetCards(int n) { for (int i = 0; i < cards.Length; i++) cards[i].SetActive(i < n); }

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
        }
#endif
    }
}
