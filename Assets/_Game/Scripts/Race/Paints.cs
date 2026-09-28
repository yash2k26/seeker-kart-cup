using UnityEngine;

namespace KartCup
{
    /// Paint jobs: a body colour the player's own kart wears instead of the character's. Four for sale (the Worker's
    /// catalog holds the prices); the character's colour is the free default. Purely a look.
    public static class Paints
    {
        public sealed class Def { public string id, sku, name; public Color color; public bool skrOnly; }

        public static readonly Def[] All =
        {
            new() { id = "sunset",   sku = "paint.sunset",   name = "SUNSET",    color = Hex("FF7A3D") },
            new() { id = "midnight", sku = "paint.midnight", name = "MIDNIGHT",  color = Hex("18213A") },
            new() { id = "rally",    sku = "paint.rally",    name = "SKR RALLY", color = Hex("7B3FE4"), skrOnly = true },
            new() { id = "white",    sku = "paint.white",    name = "WHITE",     color = Hex("F2F2EE") },
        };

        const string Key = "kc.paint";
        public static string SelectedId
        {
            get => PlayerPrefs.GetString(Key, "");
            set { PlayerPrefs.SetString(Key, value ?? ""); PlayerPrefs.Save(); }
        }

        public static Def Find(string id) { foreach (var p in All) if (p.id == id) return p; return null; }
        public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return -1; }

        /// The colour the player's kart is painted: their chosen paint if they own it, else the character's own.
        public static Color ColorFor(CharacterDef c)
        {
            var p = Find(SelectedId);
            return p != null && Unlocks.Has(p.sku) ? p.color : (c != null ? c.tint : Color.white);
        }

        static Color Hex(string h) => new(
            int.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f);
    }
}
