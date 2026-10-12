using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Buying anything (12 Oct 2026, the user: "a new modal should be opened where it shows that the wallet is getting
    /// opened or fetching if you have funds, and once we come back from the wallet, if the transaction is successful, the
    /// kart gets a purchased stamp with an animation and the sound of a stamp; if it failed, the kart is in grey and red
    /// written something"). Its own canvas over the menu: what is being bought with its picture and price, each step as
    /// the wallet goes (price, sign-in, balance, approve, confirm), then PURCHASED stamped across it, PAID · CONFIRMING,
    /// or the picture drained grey with what went wrong in red. Gem buys (recruits) go straight to the stamp.
    public sealed class PurchaseSheet : MonoBehaviour
    {
        public static PurchaseSheet Instance { get; private set; }

        [SerializeField] CanvasGroup group, stampGroup;
        [SerializeField] RectTransform card, stamp, spinner;
        [SerializeField] Image picture;
        [SerializeField] TMP_Text title, price, step, result, reason, closeLabel;
        [SerializeField] GameObject closeButton;
        [SerializeField] Material gray;
        [SerializeField] AudioClip stampClip;
        // pictures by key: "kart:<id>", "driver:<driverId>", "item:<id>", "gems"; anything else gets the fallback
        [SerializeField] string[] pictureKeys = Array.Empty<string>();
        [SerializeField] Sprite[] pictures = Array.Empty<Sprite>();
        [SerializeField] Sprite fallback;
        [SerializeField] Color gold = new(1f, 0.694f, 0.149f), red = new(1f, 0.36f, 0.38f), muted = new(0.706f, 0.655f, 0.871f);

        bool working;
        string doneLine;   // what a success means for this buy: unlocked, added to your items, added to your gems
        public bool Showing => group != null && group.blocksRaycasts;

        void Awake() { Instance = this; Hide(instant: true); }

        void Update()
        {
            if (!working) return;
            if (spinner != null) spinner.Rotate(0f, 0f, -300f * Time.unscaledDeltaTime);
            var w = WalletFlow.Instance;
            if (w != null && step != null && !string.IsNullOrEmpty(w.LastStatus) && step.text != w.LastStatus) step.text = w.LastStatus;
        }

        /// A shop purchase through the wallet, on the sheet from the tap to its outcome. Returns what WalletFlow.Buy did.
        public static async Awaitable<bool> Pay(string what, string pictureKey, string sku, string currency, string doneLine = "Unlocked on your wallet, on every phone you sign in to.")
        {
            var w = WalletFlow.Instance;
            if (w == null) return false;
            var s = Instance;
            if (s != null) { s.Open(what, pictureKey, "", doneLine); _ = s.FillPrice(sku, currency); }
            bool ok = false;
            try { ok = await w.Buy(sku, currency); }
            finally { if (s != null && s.Showing) s.Finish(ok, w.LastPending, w.LastStatus); }
            return ok;
        }

        /// Bought with gems: no wallet to wait on, straight to the stamp.
        public static void Bought(string what, string pictureKey, string priceText, string doneLine)
        {
            var s = Instance;
            if (s == null) return;
            s.Open(what, pictureKey, priceText, doneLine);
            s.Finish(true, false, "");
        }

        public void Close() => Hide(instant: false);

        void Open(string what, string key, string priceText, string done)
        {
            Tween.StopAll(group); Tween.StopAll(card); Tween.StopAll(stamp);
            Set(what, key, priceText, done);
            Tween.Alpha(group, 1f, 0.18f, useUnscaledTime: true);
            card.localScale = Vector3.one * 0.9f;
            Tween.Scale(card, 1f, 0.3f, Ease.OutBack, useUnscaledTime: true);
        }

        /// The working look, no motion: what, its picture and price, the first step.
        void Set(string what, string key, string priceText, string done)
        {
            working = true; doneLine = done;
            group.blocksRaycasts = true;
            title.text = what.ToUpperInvariant();
            int i = Array.IndexOf(pictureKeys, key);
            picture.sprite = i >= 0 && i < pictures.Length && pictures[i] != null ? pictures[i] : fallback;
            picture.material = null; picture.color = Color.white;
            price.text = priceText; price.color = gold;
            step.text = "Getting today's price…"; step.gameObject.SetActive(true); spinner.gameObject.SetActive(true);
            result.text = ""; reason.text = "";
            stamp.gameObject.SetActive(false);
            closeButton.SetActive(false);
        }

        async Awaitable FillPrice(string sku, string currency)
        {
            var cfg = await KartApi.Config(maxAgeSeconds: 300);
            var p = cfg?.Item(sku)?.price;
            if (this != null && working && p != null && p.TryGetValue(currency, out var v) && !string.IsNullOrEmpty(v)) price.text = WalletFlow.Amount(v, currency);
        }

        void Finish(bool ok, bool pending, string why)
        {
            Outcome(ok, pending, why);
            if (ok)
            {
                // the stamp is still to land: YOURS and DONE come with it
                result.text = ""; reason.text = ""; closeButton.SetActive(false);
                Stamp();
            }
            else if (!pending)
            {
                Tween.ShakeLocalPosition(card, new Vector3(22f, 0f, 0f), 0.4f, frequency: 12, useUnscaledTime: true);
                Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Error();
            }
        }

        /// The outcome's look, no motion (a success shows the stamp already down).
        void Outcome(bool ok, bool pending, string why)
        {
            working = false;
            spinner.gameObject.SetActive(false); step.gameObject.SetActive(false);
            closeButton.SetActive(true);
            if (closeLabel != null) closeLabel.text = ok ? "DONE" : "CLOSE";
            if (ok)
            {
                stamp.gameObject.SetActive(true); stampGroup.alpha = 1f; stamp.localScale = Vector3.one; stamp.localEulerAngles = new Vector3(0f, 0f, -11f);
                result.text = "YOURS"; result.color = gold; reason.text = doneLine; reason.color = muted;
            }
            else if (pending)
            {
                result.text = "PAID · CONFIRMING"; result.color = gold;
                reason.text = "It unlocks by itself as soon as Solana confirms. Nothing more to do."; reason.color = muted;
            }
            else
            {
                picture.material = gray; picture.color = new Color(1f, 1f, 1f, 0.6f);
                price.color = muted;
                result.text = "NOT BOUGHT"; result.color = red;
                reason.text = string.IsNullOrEmpty(why) ? "Nothing was charged." : why; reason.color = red;
            }
        }

        /// MenuPreview: 0 working, 1 bought (stamped), 2 failed, 3 paid and confirming.
        public void PreviewInEditor(string what, string key, string priceText, int state, string why = "", string done = "Yours to race, on every phone you sign in to.")
        {
            Set(what, key, priceText, done);
            group.alpha = 1f; card.localScale = Vector3.one;
            if (state == 0) step.text = "Approve in your wallet…";
            else Outcome(state == 1, state == 3, why);
        }

        /// The stamp comes down hard from above the card, lands crooked with a thump, and the card takes the blow.
        void Stamp()
        {
            stamp.gameObject.SetActive(true);
            stampGroup.alpha = 0f;
            stamp.localScale = Vector3.one * 2.8f;
            stamp.localEulerAngles = new Vector3(0f, 0f, 6f);
            Sequence.Create(useUnscaledTime: true)
                .ChainDelay(0.35f)
                .Chain(Tween.Scale(stamp, 0.92f, 0.13f, Ease.InQuad, useUnscaledTime: true))
                .Group(Tween.Alpha(stampGroup, 1f, 0.07f, useUnscaledTime: true))
                .Group(Tween.LocalRotation(stamp, new Vector3(0f, 0f, -11f), 0.13f, useUnscaledTime: true))
                .ChainCallback(this, s => s.Impact())
                .Chain(Tween.Scale(stamp, 1f, 0.14f, Ease.OutBack, useUnscaledTime: true));
        }

        void Impact()
        {
            if (stampClip != null) AudioDirector.Instance?.Play(stampClip, 1f, UnityEngine.Random.Range(0.96f, 1.04f));
            Haptics.Play(Haptics.Kind.Win);
            closeButton.SetActive(true);
            Tween.PunchScale(card, new Vector3(-0.035f, -0.05f, 0f), 0.28f, frequency: 6, useUnscaledTime: true);
            result.text = "YOURS"; result.color = gold;
            reason.text = doneLine; reason.color = muted;
        }

        void Hide(bool instant)
        {
            working = false;
            if (group == null) return;
            group.blocksRaycasts = false;
            Tween.StopAll(group);
            if (instant) group.alpha = 0f; else Tween.Alpha(group, 0f, 0.18f, useUnscaledTime: true);
        }
    }
}
