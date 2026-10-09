using UnityEngine;

namespace KartCup
{
    /// Paint jobs: a body colour the player's own kart wears instead of the character's. Four for sale (the Worker's
    /// catalog holds the prices); the character's colour is the free default. Purely a look.
    public static class Paints
    {
        public sealed class Def { public string id, sku, name; public Color color; public bool skrOnly; public string boss; public bool gold; }

        public static readonly Def[] All =
        {
            new() { id = "sunset",   sku = "paint.sunset",   name = "SUNSET",    color = Hex("FF7A3D") },
            new() { id = "midnight", sku = "paint.midnight", name = "MIDNIGHT",  color = Hex("18213A") },
            new() { id = "rally",    sku = "paint.rally",    name = "SKR RALLY", color = Hex("7B3FE4"), skrOnly = true },
            new() { id = "white",    sku = "paint.white",    name = "WHITE",     color = Hex("F2F2EE") },
            // Kartel Tour's boss prizes: never sold, only won (Tour.Record grants them)
            new() { id = "bossmert",   sku = "boss.mert",   name = "MERT ORANGE",  color = Hex("FF6A1A"), boss = "MERT" },
            new() { id = "bossakshay", sku = "boss.akshay", name = "AKSHAY SKY",   color = Hex("2EC2FF"), boss = "AKSHAY" },
            new() { id = "bosschase",  sku = "boss.chase",  name = "CHASE CHROME", color = Hex("C9D2DE"), boss = "CHASE" },
            new() { id = "bosstoly",   sku = "boss.toly",   name = "TOLY GOLD",    color = Hex("FFB126"), boss = "TOLY" },
            new() { id = "bossbeeman", sku = "boss.beeman", name = "BEEMAN LIME",  color = Hex("B5FF3A"), boss = "BEEMAN" },
            // Beach Buggy's 24k gold skin: never sold. Each kart earns its own by winning a Grand Prix (GoldFor/EarnGold).
            new() { id = "gold",       sku = "gold",        name = "24K GOLD",     color = Hex("E9B640"), gold = true },
        };

        /// The gold a kart has earned, by kart id ("gold.oobi").
        public static bool GoldFor(string kartId) => !string.IsNullOrEmpty(kartId) && Unlocks.Has("gold." + kartId);
        public static void EarnGold(string kartId) { if (!string.IsNullOrEmpty(kartId) && !GoldFor(kartId)) { Unlocks.Earn("gold." + kartId); KartApi.Event("gold", new { kart = kartId }); } }
        /// The kart the player drove in the race just run (RaceSpawner), so a Grand Prix win knows whose gold it is.
        public static string Driving;

        /// Whether the player owns a paint for this kart: bought, won from a boss, or (gold) earned with this kart.
        public static bool Owns(Def p, string kartId) => p != null && (p.gold ? GoldFor(kartId) : Unlocks.Has(p.sku));

        const string Key = "kc.paint";
        public static string SelectedId
        {
            get => PlayerPrefs.GetString(Key, "");
            set { PlayerPrefs.SetString(Key, value ?? ""); PlayerPrefs.Save(); }
        }

        public static Def Find(string id) { foreach (var p in All) if (p.id == id) return p; return null; }
        public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return -1; }

        /// The colour the player's kart is painted: their chosen paint if they own it, else the character's own.
        public static Color ColorFor(CharacterDef c, string kartId = null)
        {
            var p = Find(SelectedId);
            return p != null && Owns(p, kartId) ? p.color : (c != null ? c.tint : Color.white);
        }

        /// The selected paint is this kart's gold, and it has earned it: the body is metal, not paint.
        public static bool GoldOn(string kartId) { var p = Find(SelectedId); return p != null && p.gold && GoldFor(kartId); }

        static Color Hex(string h) => new(
            int.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f);
    }
}
