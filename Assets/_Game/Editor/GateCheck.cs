using KartCup;
using UnityEditor;
using UnityEngine;

/// The lap's gate arithmetic (KartProgress.GatesAhead / Window), checked without a race.   tools/unity.sh GateCheck.Run
public static class GateCheck
{
    public static void Run()
    {
        int fails = 0;
        void Is(int got, int want, string what) { if (got != want) { fails++; Debug.LogError($"[gatecheck] {what}: {got}, expected {want}"); } }
        Is(KartProgress.GatesAhead(0, -1, 30), 1, "the start line first");
        Is(KartProgress.GatesAhead(5, -1, 30), 0, "a gate before the start line");
        Is(KartProgress.GatesAhead(6, 5, 30), 1, "the next gate");
        Is(KartProgress.GatesAhead(8, 5, 30), 3, "two gates missed");
        Is(KartProgress.GatesAhead(5, 5, 30), 0, "the same gate again");
        Is(KartProgress.GatesAhead(4, 5, 30), 29, "a gate behind (driving backwards): beyond the window");
        Is(KartProgress.GatesAhead(0, 28, 30), 2, "the line, with the last gate missed");
        Is(KartProgress.GatesAhead(1, 29, 30), 2, "the line itself missed");
        Is(KartProgress.Window(30), 5, "window on 30 gates");
        Is(KartProgress.Window(12), 3, "window floor");
        Debug.Log(fails == 0 ? "[gatecheck] all checks pass" : $"[gatecheck] {fails} failing");
        if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
    }
}
