using System;
using UnityEngine;

namespace KartCup
{
    /// The Daily Challenge (Beach Buggy's): one event a day, the same for everyone, from the UTC date alone (no server):
    /// a circuit, a mode and a rival, and one goal. The first time it is met that day it pays a silver chest and three
    /// stars. Nothing carries over and nothing is lost by missing a day: no streak.
    public static class Daily
    {
        public sealed class Challenge { public int day, track, rival; public RaceMode.Kind mode; public string title, goal; public bool reverse, mist; }

        static readonly string[] Titles = { "Night Shift", "Cold Open", "Pine Dash", "Speed Trap", "Coast Run", "Summit Scrap", "Forest Duel" };

        public static int Today => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

        /// The challenge for a day: every circuit in turn, the modes in a cycle of their own (so they meet differently),
        /// a rival from the roster for duels, and the circuit run the other way round or in mist on a cycle of their own
        /// again, so the same circuit and mode come back differently (Beach Buggy's variety from levers, not track).
        public static Challenge For(int day, int rosterSize = 10)
        {
            var mode = (day % 3) switch { 0 => RaceMode.Kind.Race, 1 => RaceMode.Kind.Elimination, _ => RaceMode.Kind.Duel };
            int track = day % Mathf.Max(1, Tracks.Count);
            int rival = (day * 7 + 3) % Mathf.Max(1, rosterSize);
            return new Challenge
            {
                day = day, track = track, rival = rival, mode = mode, title = Titles[day % Titles.Length],
                goal = mode == RaceMode.Kind.Duel ? "Win the duel" : mode == RaceMode.Kind.Elimination ? "Survive to the top 3" : "Finish on the podium",
                reverse = day % 5 is 1 or 3, mist = day % 7 is 2 or 5,
            };
        }

        public static Challenge Current => For(Today, RaceSpawner.Roster != null ? RaceSpawner.Roster.characters.Length : 10);

        const string Key = "kc.daily.done";
        /// Whether the race just run paid the challenge (the results card's TourResult).
        public static bool LastPaid;
        public static bool Done => PlayerPrefs.GetInt(Key, -1) == Today;

        /// The day of the challenge armed: a race started before UTC midnight and finished after it is still that day's
        /// (judged by the next day's rules, a duel read as a podium race and the wrong day was marked done).
        static int armedDay = -1;

        public static void Arm(Challenge c)
        {
            RaceMode.Clear();   // nothing of the last event carries over
            armedDay = c.day;
            // never a duel against your own driver: the rival is one of ten, and one day in ten it was you
            int rival = c.rival, roster = RaceSpawner.Roster != null ? RaceSpawner.Roster.characters.Length : 10;
            if (rival == CharacterDatabase.SelectedIndex) rival = (rival + 1) % Mathf.Max(2, roster);   // (Arm runs before Cup.Begin sets the cup's player)
            RaceMode.Current = c.mode; RaceMode.Rival = c.mode == RaceMode.Kind.Duel ? rival : -1;
            RaceMode.Reverse = c.reverse; RaceMode.Mist = c.mist;
            RaceMode.Difficulty = c.mode == RaceMode.Kind.Duel ? 1.02f : 1.0f; RaceMode.EventId = "daily";
        }

        /// The race is over: the goal met for the first time today pays out. Returns whether it did.
        public static bool Record(int place)
        {
            int day = armedDay >= 0 ? armedDay : Today;
            var c = For(day, RaceSpawner.Roster != null ? RaceSpawner.Roster.characters.Length : 10);
            bool met = c.mode == RaceMode.Kind.Duel ? place == 1 : place >= 1 && place <= 3;
            if (!met || PlayerPrefs.GetInt(Key, -1) == day) return LastPaid = false;
            PlayerPrefs.SetInt(Key, day); PlayerPrefs.Save();
            Chests.Give(Chests.Tier.Silver);
            Gems.Earn(15, "daily");
            Chests.AddStars(3);
            KartApi.Event("daily", new { day = c.day, place });
            Achievements.Add(Achievements.Stat.DailyDone);
            return LastPaid = true;
        }

        /// "12h 40m" until the next challenge.
        public static string NextIn()
        {
            long s = 86400 - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 86400;
            return s >= 3600 ? $"{s / 3600}h {s % 3600 / 60}m" : $"{Math.Max(1, s / 60)}m";
        }
    }
}
