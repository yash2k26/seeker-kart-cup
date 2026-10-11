using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

// The cup is the game's only real logic, so it gets the only real test. Pure state machine, no scenes, no device.
// Run: Unity -batchmode -projectPath . -executeMethod CupTest.Run -logFile - -quit
public static class CupTest
{
    static int failures;

    [MenuItem("Build/Test Cup")]
    public static void Run()
    {
        failures = 0;
        PointsAndStandings();
        GridReversesEachRace();
        FinalRaceEndsTheCup();
        SingleRace();
        SurvivesARestart();
        TourShape();
        FuelTank();
        Debug.Log(failures == 0 ? "[cuptest] PASS — all checks" : $"[cuptest] {failures} FAILURE(S)");
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void PointsAndStandings()
    {
        Cup.Begin(0, 8);
        Check("a fresh cup starts at race 1", Cup.RaceNumber == 1);
        Check("a full cup runs every track", Cup.Races == Tracks.Count && !Cup.IsSingleRace);
        Check("six racers on the grid", Cup.Grid.Count == 6);
        Check("the player is on pole for race 1", Cup.Grid[0] == 0);

        Cup.Record(Cup.Grid.ToList());                       // everyone finishes in grid order
        Check("winner takes 10", Cup.PointsOf(Cup.Grid[0]) == 10);
        Check("last takes 2", Cup.PointsOf(Cup.Grid[5]) == 2);
        Check("33 points are shared out per race", Cup.Standings.Sum(s => s.points) == 33);
        Check("the winner leads the standings", Cup.Standings[0].character == 0);
        Check("the player's cup place is 1st", Cup.PlayerPlace == 1);
    }

    static void GridReversesEachRace()
    {
        Cup.Begin(0, 8);
        var race1 = Cup.Grid.ToList();
        Cup.Record(race1);
        Cup.Advance();
        Check("advancing moves to race 2", Cup.RaceNumber == 2);
        Check("the leader now starts last", Cup.Grid[5] == race1[0]);
        Check("the backmarker now starts first", Cup.Grid[0] == race1[5]);
        Check("race 2 uses a different circuit", Cup.SceneName == "Race2" && Cup.TrackId == Tracks.At(1).id);

        // The player wins again from the back: 20 points, still clear at the top.
        Cup.Record(new[] { race1[0] }.Concat(Cup.Grid.Where(c => c != race1[0])).ToList());
        Check("points accumulate across races", Cup.PointsOf(race1[0]) == 20);
        Check("the player still leads", Cup.Standings[0].character == race1[0]);
    }

    static void FinalRaceEndsTheCup()
    {
        Cup.Begin(3, 8);
        for (int i = 0; i < Cup.Races; i++)
        {
            Check($"race {i + 1} is not final until the last", Cup.IsFinalRace == (i == Cup.Races - 1));
            Cup.Record(Cup.Grid.ToList());
            if (!Cup.IsFinalRace) Cup.Advance();
        }
        Check("every race runs", Cup.RaceNumber == Cup.Races);
        Check("advancing past the last race does nothing", AdvanceIsNoOp());
        Cup.End();
        Check("ending the cup clears it", !Cup.Active);
    }

    /// A chosen track is a one-race cup: final from the start, on that circuit, restartable on the same one.
    static void SingleRace()
    {
        Cup.Begin(1, 8, new[] { 2 });
        Check("a single race is a one-race cup", Cup.Races == 1 && Cup.IsSingleRace);
        Check("it is the final race straight away", Cup.IsFinalRace && Cup.RaceNumber == 1);
        Check("it runs on the chosen circuit", Cup.SceneName == "Race3" && Cup.TrackIndex == 2 && Cup.TrackId == Tracks.At(2).id);
        Cup.Record(Cup.Grid.ToList());
        Check("advancing a single race does nothing", AdvanceIsNoOp());
        Cup.Begin(Cup.PlayerCharacter, 8, Cup.TrackList);   // RACE AGAIN passes the cup's own list back in
        Check("racing again keeps the same circuit", Cup.Races == 1 && Cup.SceneName == "Race3" && Cup.RaceNumber == 1);
        Cup.End();
    }

    static bool AdvanceIsNoOp()
    {
        int before = Cup.RaceIndex;
        Cup.Advance();
        return Cup.RaceIndex == before;
    }

    static void SurvivesARestart()
    {
        Cup.Begin(2, 8, new[] { 3, 1 });
        Cup.Record(Cup.Grid.ToList());
        Cup.Advance();
        int race = Cup.RaceNumber, player = Cup.PlayerCharacter, lead = Cup.Standings[0].points;
        var grid = Cup.Grid.ToList();

        Cup.Restore();                                       // as if the app had been killed and relaunched
        Check("a half-run cup is still active after a restart", Cup.Active);
        Check("it resumes at the same race", Cup.RaceNumber == race);
        Check("it remembers the player", Cup.PlayerCharacter == player);
        Check("it remembers the points", Cup.Standings[0].points == lead);
        Check("it remembers the grid", Cup.Grid.SequenceEqual(grid));
        Check("it remembers the tracks", Cup.TrackList.SequenceEqual(new[] { 3, 1 }) && Cup.SceneName == "Race2");

        Cup.End();
        Cup.Restore();
        Check("a finished cup does not come back", !Cup.Active);

        PlayerPrefs.SetString("kc.cup", "1|2|0:10,1:8|1,0,2,3,4,5");   // the pre-track-list format
        Cup.Restore();
        Check("a save from before track lists is not resurrected", !Cup.Active);
        PlayerPrefs.DeleteKey("kc.cup");
    }

    /// The Tour's rows keep Beach Buggy's rules (research/reports "Beach Buggy career and track design"): two races, a
    /// special, a race, the boss; a different special in every cup; no circuit run the same way twice in a cup; the wide
    /// circuits first; and the boss, not a star count, opens the next cup.
    static void TourShape()
    {
        var specials = new System.Collections.Generic.HashSet<string>();
        for (int c = 0; c < Tour.Cups.Length; c++)
        {
            var cup = Tour.Cups[c]; var ev = cup.events;
            bool Plain(Tour.Event e) => e.mode == RaceMode.Kind.Race && e.items == RaceMode.Items.Normal;
            Check($"{cup.name}: five events, the boss last", ev.Length == 5 && ev[4].mode == RaceMode.Kind.Duel && ev.Take(4).All(e => e.mode != RaceMode.Kind.Duel));
            Check($"{cup.name}: the special is event 3, the rest are races", !Plain(ev[2]) && new[] { 0, 1, 3 }.All(i => Plain(ev[i])));
            Check($"{cup.name}: its special is new to the Tour", specials.Add(Tour.ModeName(ev[2])));
            var runs = ev.Select(e => $"{e.track}|{e.mode}|{e.items}|{e.laps}|{e.reverse}|{e.mist}").ToList();
            Check($"{cup.name}: no circuit run the same way twice", runs.Distinct().Count() == runs.Count);
            Check($"{cup.name}: the boss opens at 7 of the cup's 12 race stars", Tour.EventGate(4) == 7);
        }
        Check("cup 1 runs only the wide circuits (Palm Bay, the Speedway)", Tour.Cups[0].events.All(e => e.track == 0 || e.track == 3));
        Check("Pine Ridge, the narrow one by the lake, waits for cup 3", Tour.Cups.Take(2).SelectMany(c => c.events).All(e => e.track != 2));
        Check("ids are unique", Tour.Cups.SelectMany(c => c.events).Select(e => e.id).Distinct().Count() == Tour.Cups.Sum(c => c.events.Length));

        Tour.Preview();
        Check("cup 1 is open from the start, cup 2 is not", Tour.Open(0) && !Tour.Open(1));
        Check("a closed cup names its boss", Tour.Opener(1) == "BEAT MERT");
        Tour.Preview(("c1e1", 3), ("c1e2", 3), ("c1e3", 3), ("c1e4", 3));
        Check("every star in cup 1 does not open cup 2", !Tour.Open(1));
        Check("with every race star the boss is open", Tour.EventOpen(0, 4));
        Tour.Preview(("c1e1", 3), ("c1e2", 3));
        Check("events open in order: event 4 waits for event 3's podium", Tour.EventOpen(0, 2) && !Tour.EventOpen(0, 3) && Tour.Needs(0, 3) == "FINISH EVENT 3 ON THE PODIUM FIRST");
        Tour.Preview(("c1e1", 2), ("c1e2", 2), ("c1e3", 1), ("c1e4", 1));
        Check("two seconds and two thirds (6 stars) leave the boss shut", !Tour.EventOpen(0, 4) && Tour.Needs(0, 4) == "1 MORE STAR TO OPEN");
        Tour.Preview(("c1e1", 3), ("c1e2", 3), ("c1b", 3));
        Check("beating Mert opens cup 2", Tour.Open(1) && !Tour.Open(2));

        // what each event arms, and what is kept off the board
        var (c3, smash) = Tour.Find("c3e3");
        Tour.Arm(smash, c3);
        Check("a Smash-Up is alone and unranked", RaceMode.Alone && !RaceMode.Ranked);
        var (c5, boss) = Tour.Find("c5b");
        Tour.Arm(boss, c5);
        Check("Beeman's duel: reversed, two laps, a faster charge", RaceMode.Reverse && RaceMode.Laps == 2 && RaceMode.BossCharge > 1f && !RaceMode.Ranked);
        Check("a reversed best is kept apart from the circuit's", RaceMode.BestKey("forest") == "forest.rev.l2");
        var (c1, plain) = Tour.Find("c1e1");
        Tour.Arm(plain, c1);
        Check("an ordinary race is ranked and clears the last event's levers", RaceMode.Ranked && !RaceMode.Reverse && RaceMode.Laps == 1 && RaceMode.BestKey("beach") == "beach");
        Check("the rookie field fires less than the Grand Tour's", Tour.Cups[0].fire < 1f && Tour.Cups[^1].fire > 1f);
        RaceMode.Clear();
        Tour.Preview();
    }

    /// Fuel: a can a race, one back every RefillSeconds, the clock only running while the tank is not full.
    static void FuelTank()
    {
        long now = 1_000_000; var clock = Fuel.Clock;
        Fuel.Clock = () => now;
        try
        {
            Fuel.Preview(Fuel.Max);
            Check("a full tank starts a race", Fuel.TrySpend("test") && Fuel.Count == Fuel.Max - 1);
            Check("the next can is a full refill away", Fuel.SecondsToNext == Fuel.RefillSeconds);
            now += Fuel.RefillSeconds;
            Check("a can comes back after RefillSeconds", Fuel.Count == Fuel.Max && Fuel.SecondsToNext == 0);
            now += 5000;
            Check("a full tank does not bank time", Fuel.TrySpend("test") && Fuel.SecondsToNext == Fuel.RefillSeconds);
            Fuel.Preview(0);
            Check("an empty tank starts nothing", !Fuel.TrySpend("test") && Fuel.Count == 0);
            now += Fuel.RefillSeconds * 2 + 50;
            Check("two cans and 50 s toward a third", Fuel.Count == 2 && Fuel.SecondsToNext == Fuel.RefillSeconds - 50);
            Fuel.Refund();
            Check("a win hands its can back", Fuel.Count == 3 && Fuel.LastRefund);
            Fuel.Preview(Fuel.Max);
            Fuel.Refund();
            Check("never over a full tank", Fuel.Count == Fuel.Max && !Fuel.LastRefund);
        }
        finally { Fuel.Clock = clock; Fuel.Preview(Fuel.Max); Fuel.ClearLast(); }
    }

    static void Check(string what, bool ok)
    {
        if (!ok) failures++;
        Debug.Log($"[cuptest] {(ok ? "pass" : "FAIL")}  {what}");
    }
}
