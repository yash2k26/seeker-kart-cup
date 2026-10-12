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
        [SerializeField] TMP_Text blurbLabel, nameLabel, ownedLabel, loadoutLabel, primaryLabel, secondaryLabel, tertiaryLabel;
        [SerializeField] Button primaryButton, secondaryButton, tertiaryButton;   // tertiary: PAY in ORE, the right half of the SOL row
        [SerializeField] Color rowIdle = new(0.13f, 0.17f, 0.22f), rowFocus = new(0.2f, 0.26f, 0.33f);
        // the stage (13 Oct 2026, the user: "why not showing rockets, bombs and etc"): the karts tab turns its kart on the
        // turntable and this tab left it bare. The focused pack's own race model turns there now, one per pack in
        // Inventory.Packs order; nitro has no model in the race, so it is a bottle, painted blue.
        [SerializeField] Transform podium;
        [SerializeField] GameObject[] models = System.Array.Empty<GameObject>();
        [SerializeField] Material nitroMaterial;
        [SerializeField] float baseYaw = 65f, spin = 24f, size = 2.4f, hover = 0.9f;
        GameObject shown; int shownIndex = -1; float shownY;

        int focus;
        bool busy;
        string message;

        void OnEnable() { message = null; shownIndex = -1; WalletFlow.ProfileChanged += OnProfile; Show(); _ = LoadPrices(); }
        void OnDisable()
        {
            WalletFlow.ProfileChanged -= OnProfile;
            if (shown != null) Destroy(shown);
            shown = null; shownIndex = -1;
            var backdrop = FindFirstObjectByType<MenuBackdrop>(); if (backdrop != null) backdrop.Fit = 1f;
        }

        void Update()
        {
            if (podium != null && shown != null) podium.localRotation = Quaternion.Euler(0, baseYaw + Time.time * spin, 0);
            if (shown != null) shown.transform.localPosition = new Vector3(shown.transform.localPosition.x, shownY + Mathf.Sin(Time.time * 2f) * 0.08f, shown.transform.localPosition.z);
        }

        /// The pack's race model on the turntable: a copy with only what draws (no scripts, bodies or colliders: a rocket
        /// must not fly off, a bomb must not go off), scaled to fill the stage and hovering over it.
        void Stage(int i)
        {
            if (i == shownIndex || podium == null || !Application.isPlaying) return;
            shownIndex = i;
            if (shown != null) Destroy(shown);
            shown = null;
            var prefab = i < models.Length ? models[i] : null;
            if (prefab == null) return;
            var holder = new GameObject("ItemOnStage");
            holder.SetActive(false);                                  // nothing in the copy wakes up before it is stripped
            holder.transform.SetParent(podium, false);
            var copy = Instantiate(prefab, holder.transform);
            copy.transform.localPosition = Vector3.zero; copy.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 || i == 1 ? 35f : 0f);   // rockets tilted up, ready to go
            for (int pass = 0; pass < 3; pass++)                      // components another one requires go on a later pass
                foreach (var c in copy.GetComponentsInChildren<Component>(true))
                    if (c is MonoBehaviour || c is Rigidbody || c is Collider || c is AudioSource || c is Joint) DestroyImmediate(c);
            if (Inventory.Packs[i].id == "nitro" && nitroMaterial != null)
            {
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", new Color(0.25f, 0.75f, 1.6f));
                foreach (var r in copy.GetComponentsInChildren<MeshRenderer>(true)) { r.sharedMaterial = nitroMaterial; r.SetPropertyBlock(block); }
            }
            holder.SetActive(true);
            var b = new Bounds(); bool any = false;
            foreach (var r in copy.GetComponentsInChildren<Renderer>())
                if (r is MeshRenderer || r is SkinnedMeshRenderer) { if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; } }
            if (any)
            {
                float k = size / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z));
                holder.transform.localScale = Vector3.one * k;
                var centre = podium.InverseTransformPoint(b.center);       // centred on the turntable, its foot over the deck
                var foot = podium.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
                shownY = hover - foot.y * k + (podium.InverseTransformPoint(holder.transform.position).y);
                holder.transform.localPosition = new Vector3(-centre.x * k, shownY, -centre.z * k);
            }
            shown = holder;
            var backdrop = FindFirstObjectByType<MenuBackdrop>();
            if (backdrop != null) { backdrop.Spot = null; backdrop.Fit = 0.85f; }   // the camera on the turntable, as the karts tab frames it
            PrimeTween.Tween.Scale(holder.transform, holder.transform.localScale * 0.6f, holder.transform.localScale, 0.28f, PrimeTween.Ease.OutBack);
        }
        void OnProfile(KartApi.Profile _) { if (isActiveAndEnabled) Show(); }

        /// The first open waits on the Worker's quote; redraw when it lands.
        async Awaitable LoadPrices()
        {
            if (!Application.isPlaying || KartApi.Cfg != null) return;
            await KartApi.Config();
            if (isActiveAndEnabled) Show();
        }

        /// The arrows: the next pack and the one before, round and round.
        public void Next() => Focus((focus + 1) % Inventory.Packs.Length);
        public void Previous() => Focus((focus - 1 + Inventory.Packs.Length) % Inventory.Packs.Length);

        public void Focus(int i)
        {
            focus = Mathf.Clamp(i, 0, Inventory.Packs.Length - 1); message = null;
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
            Show();
        }

        public void Primary() { if (!busy) _ = Buy("SKR"); }
        public void Secondary() { if (!busy) _ = Buy("SOL"); }
        public void Tertiary() { if (!busy) _ = Buy("ORE"); }

        async Awaitable Buy(string currency)
        {
            var pack = Inventory.Packs[Mathf.Clamp(focus, 0, Inventory.Packs.Length - 1)];
            if (WalletFlow.Instance == null) { message = "Wallet not ready"; Show(); return; }
            busy = true; message = "Opening wallet…"; Show();
            bool ok = await PurchaseSheet.Pay(pack.name, "item:" + pack.id, pack.sku, currency, $"{pack.qty} added to your items.");
            busy = false;
            message = WalletFlow.Instance.LastStatus;
            if (ok)
            {
                // the pack was credited when the Worker granted it (PendingPurchases.Verify), here or on a later retry
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
                if (rowLabels.Length > i && rowLabels[i] != null) rowLabels[i].text = owned > 0 ? owned.ToString() : "";
                var item = KartApi.Cfg?.Item(p.sku);
                if (rowPrices.Length > i && rowPrices[i] != null) rowPrices[i].text = item != null ? Skr(item) : $"{p.sol:0.###} SOL";
                var img = rows[i] != null ? rows[i].GetComponent<Image>() : null;
                if (img != null) img.color = i == focus ? rowFocus : rowIdle;
                if (rows[i] != null) rows[i].transform.localScale = Vector3.one * (i == focus ? 1.12f : 1f);
            }
            Stage(Mathf.Clamp(focus, 0, packs.Length - 1));
            var f = packs[Mathf.Clamp(focus, 0, packs.Length - 1)];
            var fi = KartApi.Cfg?.Item(f.sku);
            int have = Inventory.Count(f.id);
            if (nameLabel != null) nameLabel.text = f.name.ToUpperInvariant();
            if (ownedLabel != null) ownedLabel.text = have > 0 ? $"{have} IN YOUR GARAGE" : "NONE YET";
            if (blurbLabel != null)
                blurbLabel.text = message ?? (fi != null ? $"{f.blurb}   ·   {Skr(fi)}  or  {Sol(fi)}"
                                            : KartApi.Cfg == null ? $"{f.blurb}   ·   {f.sol:0.###} SOL" : f.blurb);
            if (loadoutLabel != null) loadoutLabel.text = $"TAKE UP TO {Carry.Kinds} KINDS, {Carry.PerKind} OF EACH, INTO A RACE";
            Set(primaryLabel, primaryButton, fi != null ? $"UNLOCK  ·  {Skr(fi)}" : KartApi.Cfg == null ? "CONNECT TO BUY" : "…", fi != null && !busy);
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
}
