namespace KartCup
{
    /// What a kart costs follows what it can do: its five powers (0..5 pips each) add up, and the total lands on a
    /// rung, priced in SOL (the SKR price follows it). worker/src/catalog.ts holds the same rule (solForPowers), so the
    /// Worker quotes what the card shows offline.
    public static class KartPricing
    {
        public const int FreeMax = 11, StreetMax = 15, HeroMax = 19;

        public static double SolFor(int total) => total <= FreeMax ? 0 : total <= StreetMax ? 0.10 : total <= HeroMax ? 0.15 : 0.25;
        public static string TierFor(int total) => total <= FreeMax ? "FREE" : total <= StreetMax ? "STREET" : total <= HeroMax ? "HERO" : "LEGEND";
    }
}
