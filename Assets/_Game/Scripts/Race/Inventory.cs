using UnityEngine;

namespace KartCup
{
    /// What you carry into a race: packs of consumables bought in the shop's items tab, counted on the phone, one
    /// spent per solo race as the starting item. A pack is credited when the Worker verifies its purchase.
    /// `item` is the ItemManager name the race hands to the kart; the packs mirror worker/src/catalog.ts.
    public static class Inventory
    {
        public sealed class Def { public string id, item, name, blurb, sku; public int qty; public double usd; }

        public static readonly Def[] Packs =
        {
            new() { id = "rocket",  item = "Rocket",      name = "Rockets x3",  blurb = "Straight, fast, no manners.",            sku = "item.rocket3",  qty = 3, usd = 0.49 },
            new() { id = "homing",  item = "Homing Item", name = "Homing x3",   blurb = "Finds the kart ahead and turns it round.", sku = "item.homing3",  qty = 3, usd = 0.49 },
            new() { id = "bomb",    item = "Bomb",        name = "Bombs x3",    blurb = "Drops behind you. Waits.",                sku = "item.bomb3",    qty = 3, usd = 0.49 },
            new() { id = "nitro",   item = "Nitro",       name = "Nitro x3",    blurb = "A turbo with the lid off.",               sku = "item.nitro3",   qty = 3, usd = 0.49 },
            new() { id = "bouncer", item = "Bouncer",     name = "Bouncers x3", blurb = "Off the walls until it finds someone.",    sku = "item.bouncer3", qty = 3, usd = 0.49 },
            new() { id = "sunbomb", item = "Sun Bomb",    name = "Sun Bombs x3", blurb = "Lob it, count to two, stand well back.", sku = "item.sunbomb3", qty = 3, usd = 0.79 },
        };

        public static Def Find(string id) { foreach (var p in Packs) if (p.id == id) return p; return null; }
        public static Def BySku(string sku) { foreach (var p in Packs) if (p.sku == sku) return p; return null; }

        public static int Count(string id) => string.IsNullOrEmpty(id) ? 0 : PlayerPrefs.GetInt("kc.item." + id, 0);
        public static void Add(string id, int n) { PlayerPrefs.SetInt("kc.item." + id, Count(id) + n); PlayerPrefs.Save(); }

        /// One out of the pack, if there is one.
        public static bool Take(string id)
        {
            int c = Count(id);
            if (c <= 0) return false;
            PlayerPrefs.SetInt("kc.item." + id, c - 1); PlayerPrefs.Save();
            return true;
        }

        /// The pack the next race starts with ("" = nothing): picked in the shop, spent on the grid.
        public static string Loadout
        {
            get => PlayerPrefs.GetString("kc.loadout", "");
            set { PlayerPrefs.SetString("kc.loadout", value ?? ""); PlayerPrefs.Save(); }
        }
    }
}
