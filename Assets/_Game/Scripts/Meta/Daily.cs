using System;
using UnityEngine;

namespace KartCup
{
    /// The Daily Challenge (Beach Buggy's): one event a day, the same for everyone, from the UTC date alone (no server):
    /// a circuit, a mode and a rival, and one goal. The first time it is met that day it pays a silver chest and three
    /// stars. Nothing carries over and nothing is lost by missing a day: no streak.
    public static class Daily
    {
        public sealed class Challenge { public int day, track, rival; public RaceMode.Kind mode; public string title, goal; }

        static readonly string[] Titles = { "Night Shift", "Cold Open", "Pine Dash", "Speed Trap", "Coast Run", "Summit Scrap", "Forest Duel" };

        public static int Today => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);

        /// The challenge for a day: every circuit in turn, the modes in a cycle of their own (so they meet differently),
        /// a rival from the roster for duels.
        public static Challenge For(int day, int rosterSize = 10)
        {
            var mode = (day % 3) switch { 0 => RaceMode.Kind.Race, 1 => RaceMode.Kind.Elimination, _ => RaceMode.Kind.Duel };
            int track = day % Mathf.Max(1, Tracks.Count);
            int rival = (day * 7 + 3) % Mathf.Max(1, rosterSize);
            return new Challenge
            {
                day = day, track = track, rival = rival, mode = mode, title = Titles[day % Titles.Length],
                goal = mode == RaceMode.Kind.Duel ? "Win the duel" : mode == RaceMode.Kind.Elimination ? "Survive to the top 3" : "Finish on the podium",
            };
        }

        public static Challenge Current => For(Today, RaceSpawner.Roster != null ? RaceSpawner.Roster.characters.Length : 10);

        const string Key = "kc.daily.done";
        /// Whether the race just run paid the challenge (the results card's TourResult).
        public static bool LastPaid;
        public static bool Done => PlayerPrefs.GetInt(Key, -1) == Today;

        public static void Arm(Challenge c)
        {
            RaceMode.Current = c.mode; RaceMode.Rival = c.mode == RaceMode.Kind.Duel ? c.rival : -1;
            RaceMode.Difficulty = c.mode == RaceMode.Kind.Duel ? 1.02f : 1.0f; RaceMode.EventId = "daily";
        }

        /// The race is over: the goal met for the first time today pays out. Returns whether it did.
        public static bool Record(int place)
        {
            var c = Current;
            bool met = c.mode == RaceMode.Kind.Duel ? place == 1 : place >= 1 && place <= 3;
            if (!met || Done) return LastPaid = false;
            PlayerPrefs.SetInt(Key, Today); PlayerPrefs.Save();
            Chests.Give(Chests.Tier.Silver);
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
