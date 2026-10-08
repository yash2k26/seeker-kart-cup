using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The garage: every kart on the podium in turn with its five powers, what it costs (the powers set the price),
    /// whether you own it, and one free test drive before you decide. Paints sit under the kart as swatches. The
    /// shop's other tab, ITEMS, sells what you take into a race.
    public sealed class GaragePanel : Carousel
    {
        [SerializeField] KartDatabase garage;
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Transform podium;
        [SerializeField] GameObject[] kartPrefabs, driverPrefabs;
        [SerializeField] CharacterCarousel characters;     // shares the podium; hidden while the garage is open
        [SerializeField] TMP_Text nameLabel, blurbLabel, priceLabel, statusLabel, primaryLabel, secondaryLabel, tertiaryLabel;
        [SerializeField] Button primaryButton, secondaryButton, tertiaryButton;   // tertiary: PAY in ORE, the right half of the SOL row
        [SerializeField] Image[] swatches = System.Array.Empty<Image>();   // 0 = driver colour, then Paints.All
        [SerializeField] StatBar[] powerBars = System.Array.Empty<StatBar>();   // SPEED, TURBO, GRIP, ARMOR, ATTACK
        [SerializeField] float baseYaw = 65f, spin = 14f;   // same turntable as CharacterCarousel

        GameObject current;
        int index;
        int paintFocus = -1;      // -1 = a kart is in focus; otherwise the swatch being looked at
        bool busy;

        void OnEnable()
        {
            if (characters != null) characters.SetVisible(false);
            var def = roster != null ? roster.Selected : null;
            index = garage != null ? garage.IndexOf(string.IsNullOrEmpty(KartDatabase.SelectedId) ? (def != null ? def.kartId : "oodi") : KartDatabase.SelectedId) : 0;
            paintFocus = -1;
            if (statusLabel != null) statusLabel.text = "";
            WalletFlow.ProfileChanged += OnProfile;
            Show();
            _ = LoadPrices();
        }

        /// The first open waits on the Worker's quote; redraw when it lands so "prices loading" becomes a price.
        async Awaitable LoadPrices()
        {
            if (!Application.isPlaying || KartApi.Cfg != null) return;
            await KartApi.Config();
            if (isActiveAndEnabled) Show();
        }

        void OnDisable()
        {
            WalletFlow.ProfileChanged -= OnProfile;
            if (current != null) Destroy(current);
            if (characters != null) characters.SetVisible(true);
            var bd = FindFirstObjectByType<MenuBackdrop>(); if (bd != null) bd.Fit = 1f;
        }

        void OnProfile(KartApi.Profile _) { if (isActiveAndEnabled) Show(); }

        /// Editor-side preview: show a kart for sale on the podium (Start/OnEnable never run in edit mode).
        public void ShowInEditor(int i, int paint = -1)
        {
            if (characters != null) characters.SetVisible(false);
            index = i; paintFocus = paint;
            Show();
            if (podium != null) podium.localRotation = Quaternion.Euler(0, baseYaw, 0);
        }

        void Update()
        {
            if (!Dragging) DragYaw = Mathf.Lerp(DragYaw, 0f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (podium != null) podium.localRotation = Quaternion.Euler(0, baseYaw + Time.time * spin + DragYaw, 0);
        }

        public override void Next() => Step(+1);
        public override void Previous() => Step(-1);

        void Step(int delta)
        {
            if (garage == null || garage.karts.Length == 0) return;
            index = ((index + delta) % garage.karts.Length + garage.karts.Length) % garage.karts.Length;
            paintFocus = -1;
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
            Show();
        }

        /// Swatch tap: 0 = the driver's own colour, 1.. = a paint. Owned paints select; unowned ones show their price.
        public void Swatch(int i)
        {
            paintFocus = i;
            if (i == 0) { Paints.SelectedId = ""; paintFocus = -1; }
            else
            {
                var p = Paints.All[Mathf.Clamp(i - 1, 0, Paints.All.Length - 1)];
                if (Paints.Owns(p, Kart?.id)) { Paints.SelectedId = p.id; paintFocus = -1; }
            }
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
            Show();
        }

        KartDef Kart => garage != null ? garage.At(index) : null;
        Paints.Def FocusedPaint => paintFocus > 0 ? Paints.All[Mathf.Clamp(paintFocus - 1, 0, Paints.All.Length - 1)] : null;

        void Show()
        {
            var def = Kart; var character = roster != null ? roster.Selected : null;
            if (def == null) return;

            if (current != null) Destroy(current);
            var kartPrefab = kartPrefabs.FirstOrDefault(k => k.name == "Kart_" + def.id) ?? kartPrefabs.FirstOrDefault();
            var driverPrefab = character != null ? driverPrefabs.FirstOrDefault(d => d.name.EndsWith(character.driverId)) : null;
            var paint = FocusedPaint;
            var colour = paint != null ? paint.color : Paints.ColorFor(character, def.id);
            if (kartPrefab != null && podium != null) current = CharacterCarousel.Mount(kartPrefab, driverPrefab ?? driverPrefabs.FirstOrDefault(), podium, colour, Application.isPlaying);
            if (current != null && (paint != null ? paint.gold : Paints.GoldOn(def.id))) CharacterDef.Paint(current, colour, gold: true);   // 24k is a metal
            Frame(current);

            bool owned = KartDatabase.Owned(def);
            bool selected = KartDatabase.SelectedId == def.id || (string.IsNullOrEmpty(KartDatabase.SelectedId) && character != null && character.kartId == def.id);
            var item = KartApi.Cfg?.Item(def.sku);

            if (paint != null)
            {
                bool pOwned = Paints.Owns(paint, def.id);
                var pItem = KartApi.Cfg?.Item(paint.sku);
                if (nameLabel != null) nameLabel.text = paint.name;
                if (paint.gold)
                {
                    // Beach Buggy's 24k skin: each kart's own, earned by winning a Grand Prix in it; never sold
                    if (blurbLabel != null) blurbLabel.text = pOwned ? $"Earned with the {def.displayName}. Every kart has its own." : $"Win a Grand Prix in the {def.displayName} to earn its gold. Never sold.";
                    if (priceLabel != null) priceLabel.text = pOwned ? "EARNED" : "GRAND PRIX PRIZE";
                    SetButton(primaryLabel, primaryButton, pOwned ? "SELECTED" : "", false); SetButton(secondaryLabel, secondaryButton, "", false);
                }
                else if (paint.boss != null && !pOwned)
                {
                    // a Tour prize: shown, never sold
                    if (blurbLabel != null) blurbLabel.text = $"Beat {paint.boss} in KARTEL TOUR to win it.";
                    if (priceLabel != null) priceLabel.text = "TOUR PRIZE";
                    SetButton(primaryLabel, primaryButton, "", false); SetButton(secondaryLabel, secondaryButton, "", false); SetButton(tertiaryLabel, tertiaryButton, "", false);
                }
                else
                {
                    if (blurbLabel != null) blurbLabel.text = paint.boss != null ? $"Won from {paint.boss} in Kartel Tour." : pItem?.blurb ?? "";
                    if (priceLabel != null) priceLabel.text = pOwned ? "UNLOCKED" : Price(pItem);
                    SetButton(primaryLabel, primaryButton, pOwned ? "SELECTED" : pItem != null ? $"UNLOCK  ·  {Skr(pItem)}" : "…", !pOwned && pItem != null);
                    SetButton(secondaryLabel, secondaryButton, !pOwned && pItem != null && !paint.skrOnly ? $"PAY {Sol(pItem)}" : paint.skrOnly && !pOwned ? "SKR ONLY" : "", !pOwned && pItem != null && !paint.skrOnly);
                    SetButton(tertiaryLabel, tertiaryButton, !pOwned && Ore(pItem) != "" ? $"PAY {Ore(pItem)}" : "", !pOwned);
                }
            }
            else
            {
                if (nameLabel != null) nameLabel.text = def.displayName.ToUpperInvariant();
                // Said before the money leaves: powers count in solo and free rooms; a paid room races equal karts.
                if (blurbLabel != null) blurbLabel.text = def.IsFree ? def.blurb : $"{def.blurb}   <alpha=#99>Paid rooms race equal karts.";
                if (priceLabel != null) priceLabel.text = def.IsFree ? $"FREE  ·  {def.Tier}" : owned ? $"UNLOCKED  ·  {def.Tier}" : $"LOCKED  ·  {KartPrice(item, def)}";
                if (owned) SetButton(primaryLabel, primaryButton, selected ? "SELECTED" : "SELECT", !selected);
                else SetButton(primaryLabel, primaryButton, item != null ? $"UNLOCK  ·  {Skr(item)}" : KartApi.Cfg == null ? "CONNECT TO BUY" : "…", item != null);
                bool trial = !owned && !KartDatabase.TrialUsed(def.id);
                SetButton(secondaryLabel, secondaryButton,
                    owned ? "" : trial ? "TEST DRIVE" : item != null && !string.IsNullOrEmpty(item.price.GetValueOrDefault("SOL")) ? $"PAY {Sol(item)}" : "",
                    !owned && (trial || item != null));
                SetButton(tertiaryLabel, tertiaryButton, !owned && Ore(item) != "" ? $"PAY {Ore(item)}" : "", !owned);
            }
            UiMotion.Beside(secondaryButton, tertiaryButton);

            for (int i = 0; i < powerBars.Length; i++)
                if (powerBars[i] != null) powerBars[i].SetCount(i switch { 0 => def.speed, 1 => def.turbo, 2 => def.grip, 3 => def.armor, _ => def.attack });

            for (int i = 0; i < swatches.Length; i++)
            {
                if (swatches[i] == null) continue;
                var p = i == 0 ? null : Paints.All[Mathf.Clamp(i - 1, 0, Paints.All.Length - 1)];
                swatches[i].color = i == 0 ? (character != null ? character.tint : Color.white) : (Paints.Owns(p, def.id) ? p.color : Color.Lerp(p.color, Color.black, 0.45f));
                bool active = i == 0 ? paintFocus < 0 && string.IsNullOrEmpty(Paints.SelectedId) : paintFocus == i || (paintFocus < 0 && Paints.SelectedId == p.id);
                swatches[i].transform.localScale = Vector3.one * (active ? 1.18f : 1f);
            }
        }

        /// The menu camera framed to this kart, closer than the shared pose (Beach Buggy's car fills its screen).
        static void Frame(GameObject kart)
        {
            var backdrop = FindFirstObjectByType<MenuBackdrop>();
            if (backdrop == null || kart == null) return;
            backdrop.Spot = null;   // the garage is on the turntable, not on the street
            var b = new Bounds(kart.transform.position, Vector3.zero);
            foreach (var r in kart.GetComponentsInChildren<MeshRenderer>()) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, b.size.z);
            backdrop.Fit = Mathf.Clamp(size / 6.0f, 0.8f, 0.92f);   // 4.7 m karts to 5.6 m trucks (wheels and all): 8-20 % closer, the head still in frame
        }

        static void SetButton(TMP_Text label, Button button, string text, bool interactable)
        {
            if (label != null) label.text = text;
            if (button != null) { button.gameObject.SetActive(!string.IsNullOrEmpty(text)); button.interactable = interactable; }
        }

        static string Skr(KartApi.ShopItem i) => WalletFlow.Amount(i.price.GetValueOrDefault("SKR"), "SKR");
        static string Sol(KartApi.ShopItem i) => WalletFlow.Amount(i.price.GetValueOrDefault("SOL"), "SOL");
        static string Ore(KartApi.ShopItem i) => i == null || KartApi.Cfg == null || !KartApi.Cfg.TakesOre ? "" : WalletFlow.Amount(i.price.GetValueOrDefault("ORE"), "ORE");
        static string Price(KartApi.ShopItem i) => i == null ? (KartApi.Cfg == null ? "PRICES LOADING…" : "NOT FOR SALE") : $"{Skr(i)}" + (string.IsNullOrEmpty(i.price.GetValueOrDefault("SOL")) ? "  ·  SKR ONLY" : $"   or {Sol(i)}");
        /// A kart's price reads offline too: the powers say what it costs before the Worker quotes it in SKR and SOL.
        static string KartPrice(KartApi.ShopItem i, KartDef def) => i == null ? (KartApi.Cfg == null ? $"{def.Sol:0.##} SOL  ·  {def.Tier}" : "NOT FOR SALE") : $"{Skr(i)}  or  {Sol(i)}  ·  {def.Tier}";

        public void Primary()
        {
            var def = Kart; if (def == null || busy) return;
            var paint = FocusedPaint;
            if (paint != null) { _ = Buy(paint.sku, "SKR", () => { Paints.SelectedId = paint.id; paintFocus = -1; }); return; }
            if (KartDatabase.Owned(def)) { KartDatabase.SelectedId = def.id; Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Confirm(); Show(); return; }
            _ = Buy(def.sku, "SKR", () => KartDatabase.SelectedId = def.id);
        }

        public void Secondary()
        {
            var def = Kart; if (def == null || busy) return;
            var paint = FocusedPaint;
            if (paint != null) { _ = Buy(paint.sku, "SOL", () => { Paints.SelectedId = paint.id; paintFocus = -1; }); return; }
            if (!KartDatabase.Owned(def) && !KartDatabase.TrialUsed(def.id))
            {
                KartDatabase.ArmTrial(def.id);
                KartDatabase.SelectedId = def.id;
                Say("Test drive armed for your next solo race.");
                KartApi.Event("trial_arm", new { kart = def.id });
                Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Confirm();
                Show();
                return;
            }
            _ = Buy(def.sku, "SOL", () => KartDatabase.SelectedId = def.id);
        }

        /// ORE: the same purchase, paid on the third rail.
        public void Tertiary()
        {
            var def = Kart; if (def == null || busy) return;
            var paint = FocusedPaint;
            if (paint != null) { _ = Buy(paint.sku, "ORE", () => { Paints.SelectedId = paint.id; paintFocus = -1; }); return; }
            if (!KartDatabase.Owned(def)) _ = Buy(def.sku, "ORE", () => KartDatabase.SelectedId = def.id);
        }

        async Awaitable Buy(string sku, string currency, System.Action onDone)
        {
            if (WalletFlow.Instance == null) { Say("Wallet not ready"); return; }
            busy = true;
            Say("Opening wallet…");
            bool ok = await WalletFlow.Instance.Buy(sku, currency);
            busy = false;
            Say(WalletFlow.Instance.LastStatus);
            if (ok) { onDone?.Invoke(); Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm(); }
            Show();
        }

        void Say(string s) { if (statusLabel != null) statusLabel.text = s; }
    }
}
