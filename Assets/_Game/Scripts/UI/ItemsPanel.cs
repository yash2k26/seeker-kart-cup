using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The shop's items tab: packs of things to take into a race (rockets, bombs, nitro...), how many of each you hold,
    /// and which one the next race starts with. Tap a row to look at a pack; the buttons under the card buy it.
    public sealed class ItemsPanel : MonoBehaviour
    {
        [SerializeField] Button[] rows = System.Array.Empty<Button>();
        [SerializeField] TMP_Text[] rowLabels = System.Array.Empty<TMP_Text>(), rowPrices = System.Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text blurbLabel, loadoutLabel, primaryLabel, secondaryLabel;
        [SerializeField] Button primaryButton, secondaryButton;
        [SerializeField] Color rowIdle = new(0.13f, 0.17f, 0.22f), rowFocus = new(0.2f, 0.26f, 0.33f);

        int focus;
        bool busy;
        string message;

        void OnEnable() { message = null; WalletFlow.ProfileChanged += OnProfile; Show(); _ = LoadPrices(); }
        void OnDisable() { WalletFlow.ProfileChanged -= OnProfile; }
        void OnProfile(KartApi.Profile _) { if (isActiveAndEnabled) Show(); }

        /// The first open waits on the Worker's quote; redraw when it lands.
        async Awaitable LoadPrices()
        {
            if (!Application.isPlaying || KartApi.Cfg != null) return;
            await KartApi.Config();
            if (isActiveAndEnabled) Show();
        }

        public void Focus(int i)
        {
            focus = Mathf.Clamp(i, 0, Inventory.Packs.Length - 1); message = null;
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
            Show();
        }

        /// The starting item cycles through nothing and whatever you hold.
        public void CycleLoadout()
        {
            var order = new System.Collections.Generic.List<string> { "" };
            order.AddRange(Inventory.Packs.Where(p => Inventory.Count(p.id) > 0).Select(p => p.id));
            int at = Mathf.Max(0, order.IndexOf(Inventory.Loadout));
            Inventory.Loadout = order[(at + 1) % order.Count];
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
            Show();
        }

        public void Primary() { if (!busy) _ = Buy("SKR"); }
        public void Secondary() { if (!busy) _ = Buy("SOL"); }

        async Awaitable Buy(string currency)
        {
            var pack = Inventory.Packs[Mathf.Clamp(focus, 0, Inventory.Packs.Length - 1)];
            if (WalletFlow.Instance == null) { message = "Wallet not ready"; Show(); return; }
            busy = true; message = "Opening wallet…"; Show();
            bool ok = await WalletFlow.Instance.Buy(pack.sku, currency);
            busy = false;
            message = WalletFlow.Instance.LastStatus;
            if (ok)
            {
                Inventory.Add(pack.id, pack.qty);
                if (string.IsNullOrEmpty(Inventory.Loadout)) Inventory.Loadout = pack.id;
                Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm();
                KartApi.Event("pack", new { pack = pack.id, currency });
            }
            Show();
        }

        /// Editor-side preview (OnEnable never runs in edit mode).
        public void ShowInEditor(int i) { focus = i; Show(); }

        void Show()
        {
            var packs = Inventory.Packs;
            for (int i = 0; i < rows.Length && i < packs.Length; i++)
            {
                var p = packs[i]; int owned = Inventory.Count(p.id);
                if (rowLabels.Length > i && rowLabels[i] != null) rowLabels[i].text = owned > 0 ? $"{p.name.ToUpperInvariant()}   <alpha=#99>{owned} OWNED" : p.name.ToUpperInvariant();
                var item = KartApi.Cfg?.Item(p.sku);
                if (rowPrices.Length > i && rowPrices[i] != null) rowPrices[i].text = item != null ? Skr(item) : $"{p.sol:0.###} SOL";
                var img = rows[i] != null ? rows[i].GetComponent<Image>() : null;
                if (img != null) img.color = i == focus ? rowFocus : rowIdle;
            }
            var f = packs[Mathf.Clamp(focus, 0, packs.Length - 1)];
            var fi = KartApi.Cfg?.Item(f.sku);
            if (blurbLabel != null)
                blurbLabel.text = message ?? (fi != null ? $"{f.blurb}   ·   {Skr(fi)}  or  {Sol(fi)}"
                                            : KartApi.Cfg == null ? $"{f.blurb}   ·   {f.sol:0.###} SOL" : f.blurb);
            var l = Inventory.Find(Inventory.Loadout);
            if (loadoutLabel != null)
                loadoutLabel.text = l == null || Inventory.Count(l.id) <= 0 ? "START WITH:  NOTHING" : $"START WITH:  {l.item.ToUpperInvariant()}   ({Inventory.Count(l.id)} LEFT)";
            Set(primaryLabel, primaryButton, fi != null ? $"UNLOCK  ·  {Skr(fi)}" : KartApi.Cfg == null ? "CONNECT TO BUY" : "…", fi != null && !busy);
            Set(secondaryLabel, secondaryButton, fi != null && !string.IsNullOrEmpty(fi.price.GetValueOrDefault("SOL")) ? $"PAY {Sol(fi)}" : "", fi != null && !busy);
        }

        static void Set(TMP_Text label, Button button, string text, bool interactable)
        {
            if (label != null) label.text = text;
            if (button != null) { button.gameObject.SetActive(!string.IsNullOrEmpty(text)); button.interactable = interactable; }
        }

        static string Skr(KartApi.ShopItem i) => WalletFlow.Amount(i.price.GetValueOrDefault("SKR"), "SKR");
        static string Sol(KartApi.ShopItem i) => WalletFlow.Amount(i.price.GetValueOrDefault("SOL"), "SOL");
    }
}
