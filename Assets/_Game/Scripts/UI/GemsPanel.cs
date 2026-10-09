using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The gem shop: the balance, three packs (catalog.ts gems.*), paid in SKR, SOL or ORE on the one purchase path.
    /// The pack is credited when the Worker verifies the payment (PendingPurchases), here or on a later retry.
    public sealed class GemsPanel : MonoBehaviour
    {
        public static readonly (string sku, int gems, string name)[] Packs = { ("gems.100", 100, "POUCH"), ("gems.550", 550, "BAG"), ("gems.1200", 1200, "CHEST") };

        [SerializeField] Button[] rows = System.Array.Empty<Button>();
        [SerializeField] TMP_Text[] rowLabels = System.Array.Empty<TMP_Text>(), rowPrices = System.Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text balanceLabel, blurbLabel, primaryLabel, secondaryLabel, tertiaryLabel;
        [SerializeField] Button primaryButton, secondaryButton, tertiaryButton;
        [SerializeField] Color rowIdle = new(0.13f, 0.17f, 0.22f), rowFocus = new(0.2f, 0.26f, 0.33f);
        int focus = 1; bool busy; string message;

        void OnEnable() { Gems.Changed += Show; message = null; Show(); _ = Refresh(); }
        void OnDisable() { Gems.Changed -= Show; }
        async Awaitable Refresh() { await KartApi.Config(maxAgeSeconds: 300); if (this != null && isActiveAndEnabled) Show(); }

        public void Focus(int i) { focus = Mathf.Clamp(i, 0, Packs.Length - 1); message = null; Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click(); Show(); }
        public void Primary() { if (!busy) _ = Buy("SKR"); }
        public void Secondary() { if (!busy) _ = Buy("SOL"); }
        public void Tertiary() { if (!busy) _ = Buy("ORE"); }

        async Awaitable Buy(string currency)
        {
            var pack = Packs[focus];
            if (WalletFlow.Instance == null) { message = "Wallet not ready"; Show(); return; }
            busy = true; message = "Opening wallet…"; Show();
            bool ok = await WalletFlow.Instance.Buy(pack.sku, currency);
            busy = false; message = WalletFlow.Instance.LastStatus;
            if (ok) { Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm(); KartApi.Event("gems_pack", new { pack = pack.sku, currency }); }
            Show();
        }

        /// Editor-side preview (OnEnable never runs in edit mode).
        public void ShowInEditor(int i) { focus = i; Show(); }

        void Show()
        {
            if (balanceLabel != null) balanceLabel.text = $"YOU HAVE  {Gems.Balance}  GEMS";
            for (int i = 0; i < rows.Length && i < Packs.Length; i++)
            {
                var p = Packs[i]; var item = KartApi.Cfg?.Item(p.sku);
                if (rowLabels.Length > i && rowLabels[i] != null) rowLabels[i].text = $"{p.name}   <alpha=#99>{p.gems} GEMS";
                if (rowPrices.Length > i && rowPrices[i] != null) rowPrices[i].text = item != null ? Skr(item) : "…";
                var img = rows[i] != null ? rows[i].GetComponent<Image>() : null;
                if (img != null) img.color = i == focus ? rowFocus : rowIdle;
            }
            var f = Packs[Mathf.Clamp(focus, 0, Packs.Length - 1)];
            var fi = KartApi.Cfg?.Item(f.sku);
            if (blurbLabel != null) blurbLabel.text = message ?? "Recruit the judges: Mert or Akshay 100, Chase 200, Toly 300. Chests and podium finishes pay gems too.";
            Set(primaryLabel, primaryButton, fi != null ? $"BUY  ·  {Skr(fi)}" : KartApi.Cfg == null ? "CONNECT TO BUY" : "…", fi != null && !busy);
            Set(secondaryLabel, secondaryButton, fi != null && !string.IsNullOrEmpty(fi.price.GetValueOrDefault("SOL")) ? $"PAY {Sol(fi)}" : "", fi != null && !busy);
            Set(tertiaryLabel, tertiaryButton, Ore(fi) != "" ? $"PAY {Ore(fi)}" : "", fi != null && !busy);
            UiMotion.Beside(secondaryButton, tertiaryButton);
        }

        static void Set(TMP_Text label, Button button, string text, bool interactable)
        {
            if (label != null) label.text = text;
            if (button != null) { button.gameObject.SetActive(!string.IsNullOrEmpty(text)); button.interactable = interactable; }
        }
        static string Skr(KartApi.ShopItem i) => WalletFlow.Amount(i.price.GetValueOrDefault("SKR"), "SKR");
        static string Sol(KartApi.ShopItem i) => WalletFlow.Amount(i.price.GetValueOrDefault("SOL"), "SOL");
        static string Ore(KartApi.ShopItem i) => i == null || KartApi.Cfg == null || !KartApi.Cfg.TakesOre ? "" : WalletFlow.Amount(i.price.GetValueOrDefault("ORE"), "ORE");
    }

    /// A button that reads the gem balance (the title and the driver screen); tapping it opens the shop (wired in MenuSetup).
    public sealed class GemChip : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        void OnEnable() { Gems.Changed += Refresh; Refresh(); }
        void OnDisable() { Gems.Changed -= Refresh; }
        void Refresh() { if (label != null) label.text = $"{Gems.Balance} GEMS"; }
        public void ShowInEditor() => Refresh();
    }
}
