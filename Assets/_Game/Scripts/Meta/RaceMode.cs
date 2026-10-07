namespace KartCup
{
    /// How the next race is run, set by whoever starts it (the Tour, the Daily Challenge) and read on the grid
    /// (RaceSpawner), by the HUD and by the results card. A plain race leaves it at its defaults. Kartel Weekly's time
    /// trial keeps its own flag (Weekly.Racing).
    public static class RaceMode
    {
        public enum Kind { Race, Elimination, Duel }

        public static Kind Current = Kind.Race;
        /// The duel's opponent (a roster index); the boss starts on pole.
        public static int Rival = -1;
        /// The field's pace for this event: each AI's committed share of its top speed is scaled by it.
        public static float Difficulty = 1f;
        /// The Tour or Daily event being driven ("" for a free race): the results card scores it.
        public static string EventId = "";

        public static bool InEvent => !string.IsNullOrEmpty(EventId);

        public static void Clear() { Current = Kind.Race; Rival = -1; Difficulty = 1f; EventId = ""; }

        public static string Name(Kind k) => k switch { Kind.Elimination => "ELIMINATION", Kind.Duel => "DUEL", _ => "RACE" };
    }
}
