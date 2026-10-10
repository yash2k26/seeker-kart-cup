using System.Collections.Generic;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// What the item boxes hold. Left alone, every box rolls any item when it is touched: a row of five could hand out
    /// five of the same, and the next row the same again. Here each row (TrackSetup parents its boxes under one
    /// transform) holds different items, a box that has been taken comes back holding something else, and whatever
    /// the player has just picked up is taken out of every other box, so the next pickup is never a repeat.
    public sealed class ItemBag : MonoBehaviour
    {
        ItemGiver[][] rows;
        Renderer[][] shown;
        bool[][] wasShown;
        string[] names;
        ItemCaster player;
        string playerHolds;
        readonly System.Random rnd = new System.Random();

        void Start()
        {
            var manager = FindAnyObjectByType<ItemManager>();
            names = manager != null ? manager.GetComponentsInChildren<Item>().Select(i => i.itemName).Distinct().ToArray() : new string[0];
            rows = transform.Cast<Transform>().Select(r => r.GetComponentsInChildren<ItemGiver>()).Where(r => r.Length > 0).ToArray();
            // Nitro Rush and Rocket Rumble (RaceMode.Only): every bubble holds the one item, for everyone, all race
            if (RaceMode.Only is { } only && names.Contains(only))
            {
                foreach (var row in rows) foreach (var box in row) box.itemName = only;
                enabled = false;
                return;
            }
            if (names.Length < 2 || rows.Length == 0) { enabled = false; return; }
            shown = rows.Select(r => r.Select(b => b.GetComponent<Renderer>()).ToArray()).ToArray();
            wasShown = rows.Select(r => r.Select(_ => true).ToArray()).ToArray();
            foreach (var row in rows)
            {
                var deal = names.OrderBy(_ => rnd.Next()).ToArray();
                for (int i = 0; i < row.Length; i++) row[i].itemName = deal[i % deal.Length];
            }
        }

        void Update()
        {
            // a box hides while it cools down: when it comes back, it holds something new
            for (int r = 0; r < rows.Length; r++)
                for (int i = 0; i < rows[r].Length; i++)
                {
                    bool on = shown[r][i] == null || shown[r][i].enabled;
                    if (on && !wasShown[r][i]) Refill(r, i, rows[r][i].itemName);
                    wasShown[r][i] = on;
                }

            if (player == null) { var kart = RaceManager.Instance != null ? RaceManager.Instance.Player : null; if (kart != null) player = kart.GetComponent<ItemCaster>(); }
            string holds = player != null && player.item != null && player.ammo > 0 ? player.item.itemName : null;
            if (holds != null && holds != playerHolds)
                for (int r = 0; r < rows.Length; r++)
                    for (int i = 0; i < rows[r].Length; i++)
                        if (rows[r][i].itemName == holds) Refill(r, i, holds);
            playerHolds = holds;
        }

        /// A new item for one box: not `not`, and not what any other box in its row holds.
        void Refill(int r, int i, string not)
        {
            var taken = new HashSet<string>(rows[r].Where((_, k) => k != i).Select(b => b.itemName)) { not };
            var free = names.Where(n => !taken.Contains(n)).ToArray();
            if (free.Length == 0) free = names.Where(n => n != not).ToArray();
            rows[r][i].itemName = free[rnd.Next(free.Length)];
        }
    }
}
