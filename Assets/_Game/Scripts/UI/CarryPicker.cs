using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Before a race, when the player holds more than one bought item: which to take and how many (Carry's rules: two
    /// kinds, three of each). A tile per pack; a tap adds one more of it (up to three, or what is held), then puts it
    /// back. The last choice is the default. Built by MenuSetup as Resources/CarryPicker, shown by RaceSpawner.
    public sealed class CarryPicker : MonoBehaviour
    {
        public static CarryPicker Instance { get; private set; }

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform card;
        [SerializeField] Button[] tiles = System.Array.Empty<Button>();
        [SerializeField] Image[] backs = System.Array.Empty<Image>(), icons = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] counts = System.Array.Empty<TMP_Text>(), owned = System.Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text hint, goLabel;
        [SerializeField] GameObject noneButton;   // hidden while nothing is picked: GO already says RACE WITHOUT ITEMS
        [SerializeField] Color idle = new(0.16f, 0.12f, 0.36f), picked = new(1f, 0.694f, 0.149f), muted = new(0.706f, 0.655f, 0.871f);

        int[] take;
        readonly List<int> order = new();     // the kinds in the order they were picked: the first is handed over first
        bool done;

        /// Shows the picker and waits for GO; Carry.Chosen holds the answer.
        public static IEnumerator Ask()
        {
            var prefab = Resources.Load<CarryPicker>("CarryPicker");
            if (prefab == null) { Carry.Chosen = Carry.Last; yield break; }
            var p = Instantiate(prefab);
            p.Open();
            while (p != null && !p.done) yield return null;
            if (p != null) Destroy(p.gameObject, 0.3f);
        }

        void Awake() => Instance = this;

        void Open()
        {
            take = new int[Inventory.Packs.Length];
            foreach (var s in Carry.Last) { int i = System.Array.FindIndex(Inventory.Packs, p => p.id == s.id); if (i >= 0) { take[i] = s.n; order.Add(i); } }
            group.alpha = 0f; Tween.Alpha(group, 1f, 0.2f, useUnscaledTime: true);
            card.localScale = Vector3.one * 0.92f; Tween.Scale(card, 1f, 0.3f, Ease.OutBack, useUnscaledTime: true);
            hint.text = $"Up to {Carry.Kinds} kinds, {Carry.PerKind} of each. Item boxes give you nothing until these are used.";
            hint.color = muted;
            Show();
        }

        /// A tile: one more of this pack, then back to none.
        public void Tap(int i)
        {
            var pack = Inventory.Packs[i];
            int have = Inventory.Count(pack.id), max = Mathf.Min(Carry.PerKind, have);
            if (max <= 0) { Say($"You have no {pack.item.ToLowerInvariant()}s. Buy them in ITEMS.", true); return; }
            if (take[i] == 0 && order.Count >= Carry.Kinds) { Say($"{Carry.Kinds} kinds at most: put one back first.", true); return; }
            take[i] = take[i] >= max ? 0 : take[i] + 1;
            if (take[i] == 0) order.Remove(i); else if (!order.Contains(i)) order.Add(i);
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
            Tween.PunchScale(tiles[i].transform, Vector3.one * 0.08f, 0.2f, frequency: 6, useUnscaledTime: true);
            Show();
        }

        public void Go() => Finish(order.Select(i => new Carry.Stack { id = Inventory.Packs[i].id, n = take[i] }).ToList());
        public void None() => Finish(new List<Carry.Stack>());

        void Finish(List<Carry.Stack> chosen)
        {
            if (done) return;
            Carry.Chosen = Carry.Clamp(chosen);
            Carry.Remember(Carry.Chosen);
            AudioDirector.Instance?.Confirm();
            done = true;
            Tween.Alpha(group, 0f, 0.2f, useUnscaledTime: true);
            group.blocksRaycasts = false;
        }

        void Say(string s, bool warn)
        {
            hint.text = s; hint.color = warn ? new Color(1f, 0.45f, 0.45f) : muted;
            if (warn) { Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Error(); Tween.ShakeLocalPosition(hint.transform, new Vector3(10f, 0f, 0f), 0.3f, useUnscaledTime: true); }
        }

        void Show()
        {
            var packs = Inventory.Packs;
            for (int i = 0; i < tiles.Length && i < packs.Length; i++)
            {
                int have = Inventory.Count(packs[i].id);
                bool on = take[i] > 0;
                if (backs.Length > i && backs[i] != null) backs[i].color = on ? picked : idle;
                if (icons.Length > i && icons[i] != null) icons[i].color = on ? new Color(0.12f, 0.06f, 0.3f) : have > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                if (counts.Length > i && counts[i] != null) { counts[i].text = on ? $"×{take[i]}" : ""; }
                if (owned.Length > i && owned[i] != null) { owned[i].text = have > 0 ? $"{have} OWNED" : "NONE"; owned[i].color = on ? new Color(0.12f, 0.06f, 0.3f) : muted; }
                tiles[i].interactable = true;
            }
            int total = order.Sum(i => take[i]);
            if (goLabel != null) goLabel.text = total > 0 ? $"RACE WITH {total} ITEM{(total == 1 ? "" : "S")}" : "RACE WITHOUT ITEMS";
            if (noneButton != null) noneButton.SetActive(total > 0);
        }

        /// MenuPreview: the picker as it opens with a choice made.
        public void PreviewInEditor(params (int pack, int n)[] chosen)
        {
            take = new int[Inventory.Packs.Length]; order.Clear();
            foreach (var (pack, n) in chosen) { take[pack] = n; order.Add(pack); }
            group.alpha = 1f;
            hint.text = $"Up to {Carry.Kinds} kinds, {Carry.PerKind} of each. Item boxes give you nothing until these are used.";
            Show();
        }
    }
}
