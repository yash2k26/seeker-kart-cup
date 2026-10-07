namespace KartCup
{
    /// What a kart costs follows what it can do: its five powers (0..5 pips each) add up, and the total lands on a
    /// rung, priced in SOL (the SKR price follows it). worker/src/catalog.ts holds the same rule (solForPowers), so the
    /// Worker quotes what the card shows offline.
    public static class KartPricing
    {
        public const int FreeMax = 11, StreetMax = 15, HeroMax = 19;

        // 8 Oct 2026 (research/26): Beach Buggy's car prices in the US and Europe ($4.99-9.99, 0.043-0.114 SOL) and what
        // Solana players pay for an item they cannot resell. Was 0.10 / 0.15 / 0.25.
        public static double SolFor(int total) => total <= FreeMax ? 0 : total <= StreetMax ? 0.05 : total <= HeroMax ? 0.08 : 0.12;
        public static string TierFor(int total) => total <= FreeMax ? "FREE" : total <= StreetMax ? "STREET" : total <= HeroMax ? "HERO" : "LEGEND";
    }
}
