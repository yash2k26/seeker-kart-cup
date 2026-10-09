using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// Kartel Tour, the career (Beach Buggy's series): four cups of five events, each event worth up to three stars
    /// (1st 3, 2nd 2, 3rd 1; a duel's win 3), kept at their best. Stars open the next cup. Each cup ends in a boss duel
    /// against one of the grid's own: beat them and they join your roster (the four judges are otherwise bought) with
    /// their paint (never sold) and a golden chest. A cup's
    /// field is quicker than the last (RaceMode.Difficulty), so the stars mean something.
    public static class Tour
    {
        public sealed class Event { public string id, title; public int track; public RaceMode.Kind mode; public int rival = -1; }
        public sealed class CupDef
        {
            public string name, kicker; public int gate; public float pace;
            public int boss; public string bossPaint, bossSku; public Event[] events;
        }

        // roster indices (TrackSetup.Roster): 0 Toly 1 Mert 2 Akshay 3 Emmett 4 Bitsy 5 Pip 6 Chase 7 Beeman 8 Maroo 9 Andrew
        public static readonly CupDef[] Cups =
        {
            new() { name = "ROOKIE RUN", kicker = "CUP 1", gate = 0, pace = 0.93f, boss = 1, bossPaint = "boss.mert", bossSku = "driver.mert", events = new[]
            {
                new Event { id = "c1e1", title = "Night Sprint", track = 0, mode = RaceMode.Kind.Race },
                new Event { id = "c1e2", title = "Pine Run", track = 2, mode = RaceMode.Kind.Race },
                new Event { id = "c1e3", title = "Last One Out", track = 0, mode = RaceMode.Kind.Elimination },
                new Event { id = "c1e4", title = "Cold Start", track = 1, mode = RaceMode.Kind.Race },
                new Event { id = "c1b", title = "BOSS: MERT", track = 0, mode = RaceMode.Kind.Duel, rival = 1 },
            } },
            new() { name = "COAST CUP", kicker = "CUP 2", gate = 9, pace = 0.98f, boss = 2, bossPaint = "boss.akshay", bossSku = "driver.akshay", events = new[]
            {
                new Event { id = "c2e1", title = "Sunset Laps", track = 3, mode = RaceMode.Kind.Race },
                new Event { id = "c2e2", title = "Ridge Cut", track = 2, mode = RaceMode.Kind.Elimination },
                new Event { id = "c2e3", title = "Harbour Lights", track = 0, mode = RaceMode.Kind.Race },
                new Event { id = "c2e4", title = "Black Ice", track = 1, mode = RaceMode.Kind.Elimination },
                new Event { id = "c2b", title = "BOSS: AKSHAY", track = 1, mode = RaceMode.Kind.Duel, rival = 2 },
            } },
            new() { name = "PRO SERIES", kicker = "CUP 3", gate = 21, pace = 1.02f, boss = 6, bossPaint = "boss.chase", bossSku = "driver.chase", events = new[]
            {
                new Event { id = "c3e1", title = "Speedway Heat", track = 3, mode = RaceMode.Kind.Elimination },
                new Event { id = "c3e2", title = "Summit Rush", track = 1, mode = RaceMode.Kind.Race },
                new Event { id = "c3e3", title = "Forest Fury", track = 2, mode = RaceMode.Kind.Race },
                new Event { id = "c3e4", title = "Coast Knockout", track = 0, mode = RaceMode.Kind.Elimination },
                new Event { id = "c3b", title = "BOSS: CHASE", track = 2, mode = RaceMode.Kind.Duel, rival = 6 },
            } },
            new() { name = "LEGENDS", kicker = "CUP 4", gate = 36, pace = 1.06f, boss = 0, bossPaint = "boss.toly", bossSku = "driver.toly", events = new[]
            {
                new Event { id = "c4e1", title = "Midnight Grand", track = 0, mode = RaceMode.Kind.Race },
                new Event { id = "c4e2", title = "Peak Elimination", track = 1, mode = RaceMode.Kind.Elimination },
                new Event { id = "c4e3", title = "Pine Gauntlet", track = 2, mode = RaceMode.Kind.Race },
                new Event { id = "c4e4", title = "Final Lap Fever", track = 3, mode = RaceMode.Kind.Race },
                new Event { id = "c4b", title = "BOSS: TOLY", track = 3, mode = RaceMode.Kind.Duel, rival = 0 },
            } },
            // 10 Oct 2026 (the user: "multiple series, tasks and races to clear each"): a fifth, the hardest field, Beeman
            // its boss. He is free already, so the prize is his paint and the chest.
            new() { name = "GRAND TOUR", kicker = "CUP 5", gate = 48, pace = 1.10f, boss = 7, bossPaint = "boss.beeman", bossSku = "", events = new[]
            {
                new Event { id = "c5e1", title = "Coast to Coast", track = 0, mode = RaceMode.Kind.Race },
                new Event { id = "c5e2", title = "Frost Knockout", track = 1, mode = RaceMode.Kind.Elimination },
                new Event { id = "c5e3", title = "Canopy Chase", track = 2, mode = RaceMode.Kind.Race },
                new Event { id = "c5e4", title = "Speedway Survivor", track = 3, mode = RaceMode.Kind.Elimination },
                new Event { id = "c5b", title = "BOSS: BEEMAN", track = 2, mode = RaceMode.Kind.Duel, rival = 7 },
            } },
        };
        public const int StarsPerEvent = 3;
        public static int MaxStars => Cups.Sum(c => c.events.Length) * StarsPerEvent;

        [Serializable] sealed class Entry { public string id; public int stars; }
        [Serializable] sealed class State { public List<Entry> best = new(); }
        const string Key = "kc.tour";
        static State state;
        static State S
        {
            get
            {
                if (state != null) return state;
                try { state = JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key, "")); } catch { state = null; }
                return state ??= new State();
            }
        }
        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(S)); PlayerPrefs.Save(); }

        public static int Stars(string id) => S.best.FirstOrDefault(e => e.id == id)?.stars ?? 0;
        public static int Total => S.best.Sum(e => e.stars);
        public static int CupStars(int cup) => Cups[cup].events.Sum(e => Stars(e.id));
        public static bool Open(int cup) => Total >= Cups[cup].gate;
        /// Inside a series (Beach Buggy's): the first two events are open, the rest open as the series' own stars come in.
        public static readonly int[] EventGates = { 0, 0, 1, 3, 6 };
        public static int EventGate(int i) => EventGates[Mathf.Clamp(i, 0, EventGates.Length - 1)];
        public static bool EventOpen(int cup, int i) => Open(cup) && CupStars(cup) >= EventGate(i);
        public static string Goal(Event e) => e.mode switch { RaceMode.Kind.Duel => "WIN", RaceMode.Kind.Elimination => "LAST ONE IN", _ => "1ST" };
        public static string Rule(Event e, string rival) => e.mode switch
        {
            RaceMode.Kind.Duel => rival != null ? $"One on one against {rival}. First over the line wins." : "One on one. First over the line wins.",
            RaceMode.Kind.Elimination => "The last kart is knocked out every 15 seconds. Be the last one left.",
            _ => "Race the other drivers to the finish line.",
        };
        public static bool BossBeaten(int cup) => Stars(Cups[cup].events[^1].id) > 0;
        public static (CupDef cup, Event ev) Find(string id)
        {
            foreach (var c in Cups) foreach (var e in c.events) if (e.id == id) return (c, e);
            return (null, null);
        }

        /// What the last Tour race earned, for the results card (TourResult).
        public sealed class Result { public string id, title; public int stars, gained, place; public float time; public string unlocked, prize; }
        public static Result Last { get; private set; }
        public static void ClearLast() => Last = null;

        /// Points the next race at an event: the grid, the pace and the scoring.
        public static void Arm(Event e, CupDef c)
        {
            RaceMode.Current = e.mode; RaceMode.Rival = e.rival; RaceMode.EventId = e.id;
            RaceMode.Difficulty = e.mode == RaceMode.Kind.Duel ? c.pace + 0.03f : c.pace;   // a boss drives a touch above their cup
        }

        /// The race is over: keep the best stars, open what they open, pay a beaten boss.
        public static Result Record(string id, int place, float time = 0f)
        {
            var (cup, ev) = Find(id);
            if (ev == null) return null;
            int stars = ev.mode == RaceMode.Kind.Duel ? (place == 1 ? 3 : 0) : place >= 1 && place <= 3 ? 4 - place : 0;
            int before = Stars(id), totalBefore = Total;
            var r = new Result { id = id, title = ev.title, stars = stars, gained = Mathf.Max(0, stars - before), place = place, time = time };
            if (stars > before)
            {
                var e = S.best.FirstOrDefault(x => x.id == id);
                if (e == null) S.best.Add(new Entry { id = id, stars = stars }); else e.stars = stars;
                Save();
                // a cup this race opened
                for (int i = 0; i < Cups.Length; i++)
                    if (totalBefore < Cups[i].gate && Total >= Cups[i].gate) r.unlocked = $"{Cups[i].kicker} · {Cups[i].name} UNLOCKED";
                // a boss beaten the first time: their paint and a golden chest
                if (ev.mode == RaceMode.Kind.Duel && before == 0 && stars > 0)
                {
                    var paint = Paints.All.FirstOrDefault(p => p.sku == cup.bossPaint);
                    if (paint != null) Unlocks.Earn(paint.sku);
                    bool recruited = !string.IsNullOrEmpty(cup.bossSku) && !Unlocks.Has(cup.bossSku);
                    if (recruited) Unlocks.Earn(cup.bossSku);   // the boss drives for you now (Beach Buggy's recruit)
                    Chests.Give(Chests.Tier.Treasure);
                    Gems.Earn(50, "boss");
                    string who = ev.title.Replace("BOSS: ", "");
                    r.prize = (recruited ? $"{who} JOINS YOUR ROSTER  ·  " : "") + $"{paint?.name ?? "A PAINT"} IS YOURS  ·  + GOLDEN TREASURE CHEST";
                }
            }
            KartApi.Event("tour", new { id, place, stars, gained = r.gained });
            Achievements.Set(Achievements.Stat.TourStars, Total);
            Achievements.Set(Achievements.Stat.BossesBeaten, Enumerable.Range(0, Cups.Length).Count(BossBeaten));
            return Last = r;
        }

#if UNITY_EDITOR
        public static void Preview(params (string id, int stars)[] best) { state = new State(); foreach (var (id, s) in best) state.best.Add(new Entry { id = id, stars = s }); }
#endif
    }
}
