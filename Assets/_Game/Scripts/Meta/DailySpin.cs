using UnityEngine;

namespace KartCup
{
    /// Beach Buggy's daily reward wheel, without its ads: one free spin a day (the UTC day, as the Daily Challenge),
    /// eight prizes in equal slices, so what the wheel shows is the odds. Nothing carries over and nothing is lost by
    /// missing a day: no streak, and no paid spins.
    public static class DailySpin
    {
        public enum Kind { Chest, Stars, Item }
        public sealed class Prize { public Kind kind; public Chests.Tier tier; public int amount; public string item, label, glyph; }

        public static readonly Prize[] Wheel =
        {
            new() { kind = Kind.Chest, tier = Chests.Tier.Wood,   label = "WOOD CHEST",   glyph = "chest" },
            new() { kind = Kind.Item,  item = "nitro",  amount = 2, label = "NITRO ×2",   glyph = "nitro" },
            new() { kind = Kind.Stars, amount = 2,                 label = "2 STARS",     glyph = "star" },
            new() { kind = Kind.Item,  item = "rocket", amount = 2, label = "ROCKET ×2",  glyph = "rocket" },
            new() { kind = Kind.Chest, tier = Chests.Tier.Silver, label = "SILVER CHEST", glyph = "chest" },
            new() { kind = Kind.Item,  item = "homing", amount = 2, label = "HOMING ×2",  glyph = "homing" },
            new() { kind = Kind.Stars, amount = 4,                 label = "4 STARS",     glyph = "star" },
            new() { kind = Kind.Chest, tier = Chests.Tier.Gold,   label = "GOLD CHEST",   glyph = "chest" },
        };

        const string Key = "kc.spin.day";
        public static bool Ready => PlayerPrefs.GetInt(Key, -1) != Daily.Today;

        /// Spin today's wheel: the slice it lands on is paid at once. -1 if it was spun today already.
        public static int Spin()
        {
            if (!Ready) return -1;
            int i = Random.Range(0, Wheel.Length);
            PlayerPrefs.SetInt(Key, Daily.Today); PlayerPrefs.Save();
            var p = Wheel[i];
            switch (p.kind)
            {
                case Kind.Chest: Chests.Give(p.tier); break;
                case Kind.Stars: Chests.AddStars(p.amount); break;
                case Kind.Item: Inventory.Add(p.item, p.amount); break;
            }
            KartApi.Event("daily_spin", new { prize = p.label });
            return i;
        }
    }
}
