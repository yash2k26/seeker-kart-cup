using System.Collections.Generic;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Bought items into a race (13 Oct 2026, the user): "if the user has bought items already, the user will have it
    /// already before even the user collects it from the race ... at a time two types and max x3 of each, so max 6 ...
    /// the user can't collect the items in the race until they have finished their bought ones ... before the race a
    /// modal to choose which items and how many, but only if more than one is owned; if one, let them use it".
    ///   - at most Kinds kinds, PerKind of each, never more than the player holds
    ///   - one owned in all: it goes in by itself; more: CarryPicker asks; none: nothing
    ///   - an item leaves the inventory when it is fired, so what is carried and not used stays the player's
    ///   - while the player carries any, item boxes give them nothing (ItemGiver never overwrites a held item, and the
    ///     next kind is handed over the moment the last of one is fired)
    public static class Carry
    {
        public const int Kinds = 2, PerKind = 3;
        const string Key = "kc.carry";                       // the last choice, the picker's default: "rocket:3,bomb:2"

        public sealed class Stack { public string id; public int n; }

        /// What the player takes into this race; set by the picker (or by Auto) before the countdown.
        public static List<Stack> Chosen = new();
        /// Tests and tools: skip the picker and race with Chosen as it is.
        public static bool SkipPicker;

        public static int Owned => Inventory.Packs.Sum(p => Inventory.Count(p.id));
        public static bool NeedsPicker => Owned > 1;

        /// One item in all: that one. Otherwise nothing (the picker decides).
        public static List<Stack> Auto()
        {
            var p = Inventory.Packs.FirstOrDefault(x => Inventory.Count(x.id) > 0);
            return Owned == 1 && p != null ? new List<Stack> { new() { id = p.id, n = 1 } } : new List<Stack>();
        }

        /// The rules on any wish: known packs only, first two kinds with any, at most three of each, no more than held.
        public static List<Stack> Clamp(IEnumerable<Stack> want)
        {
            var outList = new List<Stack>();
            foreach (var s in want ?? Enumerable.Empty<Stack>())
            {
                if (s == null || Inventory.Find(s.id) == null || outList.Any(o => o.id == s.id)) continue;
                int n = Mathf.Min(s.n, PerKind, Inventory.Count(s.id));
                if (n <= 0) continue;
                outList.Add(new Stack { id = s.id, n = n });
                if (outList.Count == Kinds) break;
            }
            return outList;
        }

        /// The last choice, re-checked against what is held now.
        public static List<Stack> Last => Clamp(Parse(PlayerPrefs.GetString(Key, "")));
        public static void Remember(List<Stack> chosen) { PlayerPrefs.SetString(Key, Format(chosen)); PlayerPrefs.Save(); }

        public static string Format(IEnumerable<Stack> s) => string.Join(",", s.Select(x => $"{x.id}:{x.n}"));
        public static List<Stack> Parse(string s) => (s ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Split(':')).Where(t => t.Length == 2 && int.TryParse(t[1], out _))
            .Select(t => new Stack { id = t[0], n = int.Parse(t[1]) }).ToList();
    }

    /// On the player's kart for the race: hands over the carried kinds one after the other, and takes each one out of
    /// the inventory as it is fired. Gone from the kart once the last carried item is fired: from then on the boxes fill it.
    public sealed class CarriedItems : MonoBehaviour
    {
        ItemCaster caster;
        ItemManager items;
        readonly Queue<Carry.Stack> waiting = new();
        Carry.Stack current;

        public static void Begin(Kart player, List<Carry.Stack> stacks)
        {
            if (player == null || stacks == null || stacks.Count == 0) return;
            var c = player.GetComponent<ItemCaster>(); var m = FindAnyObjectByType<ItemManager>();
            if (c == null || m == null) return;
            var me = player.gameObject.AddComponent<CarriedItems>();
            me.caster = c; me.items = m;
            foreach (var s in stacks) me.waiting.Enqueue(new Carry.Stack { id = s.id, n = s.n });
            c.castEvent.AddListener(me.OnCast);
            me.Next();
            KartApi.Event("carry", new { take = Carry.Format(stacks) });
        }

        /// How many carried items are still to come after the one in hand (the HUD's "then").
        public Carry.Stack Then => waiting.Count > 0 ? waiting.Peek() : null;

        void OnCast()
        {
            if (current == null) return;
            Inventory.Take(current.id);
            current.n--;
            Debug.Log($"[carry] fired a {current.id}: {current.n} more in hand, {Inventory.Count(current.id)} left at home");
            if (current.n <= 0) Next();
        }

        void Next()
        {
            current = waiting.Count > 0 ? waiting.Dequeue() : null;
            if (current == null) { Debug.Log("[carry] all carried items used: the item boxes are open"); caster.castEvent.RemoveListener(OnCast); Destroy(this); return; }
            var item = items.GetItem(Inventory.Find(current.id)?.item);
            if (item == null) { Next(); return; }
            caster.GiveItem(item, current.n, true);          // the moment the last of one kind is fired: no box gets in between
            Debug.Log($"[carry] in hand: {current.id} x{current.n}");
        }
    }
}
