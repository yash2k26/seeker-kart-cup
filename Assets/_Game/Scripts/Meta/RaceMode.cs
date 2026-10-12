namespace KartCup
{
    /// How the next race is run, set by whoever starts it (the Tour, the Daily Challenge) and read on the grid
    /// (RaceSpawner), by the HUD and by the results card. A plain race leaves it at its defaults. Kartel Weekly's time
    /// trial keeps its own flag (Weekly.Racing).
    ///
    /// Beach Buggy never builds a harder track for a harder event: it runs the same circuit with something else changed
    /// (research/reports "Beach Buggy career and track design"). Each lever here is one of those: the mode, what the
    /// bubbles hold, the laps, the direction, the weather, how keen the field is to fire.
    public static class RaceMode
    {
        /// Smash: Beach Buggy's Stampede. Alone on the circuit for a lap; the stars are for what you smash on the way.
        public enum Kind { Race, Elimination, Duel, Smash }
        /// What the bubbles hold. Beach Buggy's specials are one forced item and a goal (Boost Blitz, Shooting Gallery);
        /// Clean is Beach Buggy Racing 2's: no items and no specials, only the driving.
        public enum Items { Normal, Clean, Nitro, Rockets }

        public static Kind Current = Kind.Race;
        /// The duel's opponent (a roster index); the boss starts on pole.
        public static int Rival = -1;
        /// The field's pace for this event: each AI's committed share of its top speed is scaled by it.
        public static float Difficulty = 1f;
        /// How keen the field is to fire what it picks up, apart from how fast it drives: a rookie field holds its rockets.
        public static float Fire = 1f;
        /// A boss's special charges this many times as fast as yours (said on the event card, so it is a rule, not a cheat).
        public static float BossCharge = 1f;
        public static Items Rule = Items.Normal;
        /// More of the circuit's hazards (oil, animal crossings): 0 in a free race and the Tour's first cup, up to 4.
        public static int Hazard;
        /// Roadworks slaloms on the straights (Roadworks): the Tour's later series.
        public static int Works;
        public static int Laps = 1;
        /// The circuit driven the other way round (TrackDirection), and in thick mist.
        public static bool Reverse, Mist;
        /// The Tour or Daily event being driven ("" for a free race): the results card scores it.
        public static string EventId = "";

        public static bool InEvent => !string.IsNullOrEmpty(EventId);
        /// A time that belongs on the circuit's board: the ordinary lap, the ordinary bubbles. Anything else races
        /// unranked (RaceSession) and keeps its own personal best (BestKey).
        public static bool Ranked => Rule == Items.Normal && !Reverse && Laps == 1 && Current != Kind.Smash;
        /// Nobody else on the grid.
        public static bool Alone => Current == Kind.Smash;
        /// The one item every bubble holds, or null for the usual mix.
        public static string Only => Rule switch { Items.Nitro => "Nitro", Items.Rockets => "Rocket", _ => null };

        public static void Clear()
        {
            Current = Kind.Race; Rival = -1; Difficulty = 1f; Fire = 1f; BossCharge = 1f;
            Rule = Items.Normal; Laps = 1; Reverse = Mist = false; EventId = ""; Hazard = 0; Works = 0;
        }

        /// The personal best this race is kept under: the circuit's own for an ordinary race, its own row otherwise.
        public static string BestKey(string trackId) => BestKey(trackId, Reverse, Laps, Rule);
        public static string BestKey(string trackId, bool reverse, int laps, Items rule) => trackId + (reverse ? ".rev" : "") + (laps > 1 ? $".l{laps}" : "")
            + (rule is Items.Nitro or Items.Rockets ? "." + rule.ToString().ToLowerInvariant() : "");

        public static string Name(Kind k) => k switch { Kind.Elimination => "ELIMINATION", Kind.Duel => "DUEL", Kind.Smash => "SMASH-UP", _ => "RACE" };
        public static string Name(Kind k, Items rule) => k != Kind.Race ? Name(k) : rule switch
        {
            Items.Nitro => "NITRO RUSH", Items.Rockets => "ROCKET RUMBLE", Items.Clean => "CLEAN RACE", _ => "RACE",
        };
    }
}
