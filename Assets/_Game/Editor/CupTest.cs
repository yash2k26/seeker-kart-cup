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

    static void Check(string what, bool ok)
    {
        if (!ok) failures++;
        Debug.Log($"[cuptest] {(ok ? "pass" : "FAIL")}  {what}");
    }
}
