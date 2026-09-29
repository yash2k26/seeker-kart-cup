namespace KartCup
{
    /// What a kart costs follows what it can do: its five powers (0..5 pips each) add up, and the total lands on a
    /// rung. worker/src/catalog.ts holds the same rule (usdForPowers), so the Worker quotes what the card shows offline.
    public static class KartPricing
    {
        public const int FreeMax = 11, StreetMax = 15, HeroMax = 19;

        public static double UsdFor(int total) => total <= FreeMax ? 0 : total <= StreetMax ? 2.99 : total <= HeroMax ? 4.99 : 7.99;
        public static string TierFor(int total) => total <= FreeMax ? "FREE" : total <= StreetMax ? "STREET" : total <= HeroMax ? "HERO" : "LEGEND";
    }
}
