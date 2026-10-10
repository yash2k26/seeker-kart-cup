using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// Kartel Tour, the career (Beach Buggy's series): five cups of five events, each event worth up to three stars
    /// (1st 3, 2nd 2, 3rd 1; a duel's win 3; a Smash-Up by what it smashed), kept at their best. Each cup ends in a boss
    /// duel against one of the grid's own: beat them and they join your roster (the four judges are otherwise bought)
    /// with their paint (never sold) and a golden chest, and the next cup opens. A cup's field is quicker than the
    /// last (RaceMode.Difficulty) and fires more (RaceMode.Fire), so the stars mean something.
    ///
    /// The shape is Beach Buggy's (research/reports "Beach Buggy career and track design"): two races, a special, a race,
    /// the boss. Every event is one row of data, and no two on the same circuit in a cup are run the same way: the
    /// variety is in the mode, the bubbles, the laps, the direction and the weather, not in more track. The circuits
    /// come in by how forgiving they are (Palm Bay and the Speedway are wide; Frost Peak opens in cup 2, Pine Ridge,
    /// narrow and by its lake, in cup 3), and each cup's special is a different one.
    public static class Tour
    {
        public sealed class Event
        {
            public string id, title; public int track; public RaceMode.Kind mode; public int rival = -1;
            public RaceMode.Items items; public int laps = 1; public bool reverse, mist;
        }
        public sealed class CupDef
        {
            public string name, kicker; public float pace, fire = 1f;
            public int boss; public string bossPaint, bossSku; public Event[] events;
        }

        const RaceMode.Kind Race = RaceMode.Kind.Race, Elim = RaceMode.Kind.Elimination, Duel = RaceMode.Kind.Duel, Smash = RaceMode.Kind.Smash;
        const RaceMode.Items Nitro = RaceMode.Items.Nitro, Rockets = RaceMode.Items.Rockets, Clean = RaceMode.Items.Clean;
        // track indices (Tracks.All): 0 Palm Bay 1 Frost Peak 2 Pine Ridge 3 Sunset Speedway
        // roster indices (TrackSetup.Roster): 0 Toly 1 Mert 2 Akshay 3 Emmett 4 Bitsy 5 Pip 6 Chase 7 Beeman 8 Maroo 9 Andrew
        public static readonly CupDef[] Cups =
        {
            new() { name = "ROOKIE RUN", kicker = "CUP 1", pace = 0.93f, fire = 0.6f, boss = 1, bossPaint = "boss.mert", bossSku = "driver.mert", events = new[]
            {
                new Event { id = "c1e1", title = "Night Sprint", track = 0, mode = Race },
                new Event { id = "c1e2", title = "Under the Lights", track = 3, mode = Race },
                new Event { id = "c1e3", title = "Nitro Rush", track = 0, mode = Race, items = Nitro },
                new Event { id = "c1e4", title = "Dusk Haze", track = 3, mode = Race, mist = true },
                new Event { id = "c1b", title = "BOSS: MERT", track = 0, mode = Duel, rival = 1 },
            } },
            new() { name = "COAST CUP", kicker = "CUP 2", pace = 0.98f, fire = 0.8f, boss = 2, bossPaint = "boss.akshay", bossSku = "driver.akshay", events = new[]
            {
                new Event { id = "c2e1", title = "Cold Start", track = 1, mode = Race },
                new Event { id = "c2e2", title = "The Other Way", track = 0, mode = Race, reverse = true },
                new Event { id = "c2e3", title = "Last One Out", track = 3, mode = Elim },
                new Event { id = "c2e4", title = "Black Ice", track = 1, mode = Race, mist = true },
                new Event { id = "c2b", title = "BOSS: AKSHAY", track = 1, mode = Duel, rival = 2 },
            } },
            new() { name = "PRO SERIES", kicker = "CUP 3", pace = 1.02f, fire = 1.0f, boss = 6, bossPaint = "boss.chase", bossSku = "driver.chase", events = new[]
            {
                new Event { id = "c3e1", title = "Pine Run", track = 2, mode = Race },
                new Event { id = "c3e2", title = "Summit Reverse", track = 1, mode = Race, reverse = true },
                new Event { id = "c3e3", title = "Smash-Up", track = 2, mode = Smash },
                new Event { id = "c3e4", title = "Backstretch", track = 3, mode = Race, reverse = true },
                new Event { id = "c3b", title = "BOSS: CHASE", track = 2, mode = Duel, rival = 6 },
            } },
            new() { name = "LEGENDS", kicker = "CUP 4", pace = 1.06f, fire = 1.15f, boss = 0, bossPaint = "boss.toly", bossSku = "driver.toly", events = new[]
            {
                new Event { id = "c4e1", title = "Midnight Grand", track = 0, mode = Race, laps = 2 },
                new Event { id = "c4e2", title = "Back Through the Pines", track = 2, mode = Race, reverse = true },
                new Event { id = "c4e3", title = "Rocket Rumble", track = 1, mode = Race, items = Rockets },
                new Event { id = "c4e4", title = "Fog Bank", track = 3, mode = Race, mist = true, laps = 2 },
                new Event { id = "c4b", title = "BOSS: TOLY", track = 3, mode = Duel, rival = 0, laps = 2 },
            } },
            // 10 Oct 2026 (the user: "multiple series, tasks and races to clear each"): a fifth, the hardest field, Beeman
            // its boss. He is free already, so the prize is his paint and the chest.
            new() { name = "GRAND TOUR", kicker = "CUP 5", pace = 1.10f, fire = 1.3f, boss = 7, bossPaint = "boss.beeman", bossSku = "", events = new[]
            {
                new Event { id = "c5e1", title = "Coast to Coast", track = 0, mode = Race, reverse = true, laps = 2 },
                new Event { id = "c5e2", title = "Whiteout", track = 1, mode = Race, mist = true, reverse = true },
                new Event { id = "c5e3", title = "Clean Sweep", track = 2, mode = Race, items = Clean },
                new Event { id = "c5e4", title = "Twilight Double", track = 3, mode = Race, laps = 2 },
                new Event { id = "c5b", title = "BOSS: BEEMAN", track = 2, mode = Duel, rival = 7, reverse = true, laps = 2 },
            } },
        };
        /// A boss's special charges this much faster than the player's (the card says so).
        public const float BossCharge = 1.5f;
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
        /// Beach Buggy's ladder: the boss is the wall. A cup opens when the last one's boss is beaten, and that boss opens at
        /// about half the cup's stars (EventGates), as Easy Street's does (8 of 15). Stars across the whole Tour used to
        /// decide it (9/21/36/48), which by cup 4 asked for 80% of everything won.
        public static bool Open(int cup) => cup <= 0 || BossBeaten(cup - 1);
        /// What opens a closed cup: "BEAT MERT".
        public static string Opener(int cup) => cup <= 0 ? "" : Cups[cup - 1].events[^1].title.Replace("BOSS: ", "BEAT ");
        /// Inside a series (Beach Buggy's): the first two events are open, the rest open as the series' own stars come in.
        public static readonly int[] EventGates = { 0, 0, 1, 3, 6 };
        public static int EventGate(int i) => EventGates[Mathf.Clamp(i, 0, EventGates.Length - 1)];
        public static bool EventOpen(int cup, int i) => Open(cup) && CupStars(cup) >= EventGate(i);
        public static string ModeName(Event e) => RaceMode.Name(e.mode, e.items);
        /// The personal best an event's card shows: the one its own way of running the circuit keeps.
        public static string BestKey(Event e) => RaceMode.BestKey(Tracks.At(e.track).id, e.reverse, e.laps, e.items);
        /// The circuit's name, then what is different about this run of it.
        public static string Where(Event e) { var v = Variant(e); return Tracks.At(e.track).name.ToUpperInvariant() + (v.Length > 0 ? "  ·  " + v : ""); }
        /// What is different about this run of the circuit, after its name: "REVERSE  ·  MIST  ·  2 LAPS".
        public static string Variant(Event e) => string.Join("  ·  ", new[] { e.reverse ? "REVERSE" : null, e.mist ? "MIST" : null, e.laps > 1 ? $"{e.laps} LAPS" : null }.Where(x => x != null));
        public static string Goal(Event e) => e.mode switch
        {
            RaceMode.Kind.Duel => "WIN", RaceMode.Kind.Elimination => "LAST ONE IN", RaceMode.Kind.Smash => "2 IN 5", _ => "1ST",
        };
        public static string Rule(Event e, string rival)
        {
            string rule = e.mode switch
            {
                RaceMode.Kind.Duel => (rival != null ? $"One on one against {rival}." : "One on one.") + " Their special charges faster than yours. First over the line wins.",
                RaceMode.Kind.Elimination => "The last kart is knocked out every 15 seconds. Be the last one left.",
                RaceMode.Kind.Smash => "Alone for a lap. Smash the crates and critters by the road: two in every five for three stars.",
                _ => e.items switch
                {
                    RaceMode.Items.Nitro => "Every bubble holds a nitro. Race the others to the line.",
                    RaceMode.Items.Rockets => "Every bubble holds a rocket. Race the others to the line.",
                    RaceMode.Items.Clean => "No items and no specials: only the driving.",
                    _ => "Race the other drivers to the finish line.",
                },
            };
            if (e.reverse) rule += " The circuit runs the other way round.";
            if (e.mist) rule += " In thick mist.";
            if (e.laps > 1) rule += $" {e.laps} laps.";
            return rule;
        }
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
            RaceMode.Clear();
            RaceMode.Current = e.mode; RaceMode.Rival = e.rival; RaceMode.EventId = e.id;
            RaceMode.Difficulty = e.mode == RaceMode.Kind.Duel ? c.pace + 0.03f : c.pace;   // a boss drives a touch above their cup
            RaceMode.Fire = c.fire;
            RaceMode.BossCharge = e.mode == RaceMode.Kind.Duel ? BossCharge : 1f;          // ...and charges faster: a stated rule, never unlimited
            RaceMode.Rule = e.items; RaceMode.Laps = Mathf.Max(1, e.laps); RaceMode.Reverse = e.reverse; RaceMode.Mist = e.mist;
        }

        /// The race is over: keep the best stars, open what they open, pay a beaten boss.
        public static Result Record(string id, int place, float time = 0f)
        {
            var (cup, ev) = Find(id);
            if (ev == null) return null;
            int stars = ev.mode switch
            {
                RaceMode.Kind.Duel => place == 1 ? 3 : 0,
                RaceMode.Kind.Smash => SmashUp.Stars,
                _ => place >= 1 && place <= 3 ? 4 - place : 0,
            };
            int before = Stars(id);
            var r = new Result { id = id, title = ev.title, stars = stars, gained = Mathf.Max(0, stars - before), place = place, time = time };
            if (stars > before)
            {
                var e = S.best.FirstOrDefault(x => x.id == id);
                if (e == null) S.best.Add(new Entry { id = id, stars = stars }); else e.stars = stars;
                Save();
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
                    // the boss was the wall: the next cup opens
                    int next = System.Array.IndexOf(Cups, cup) + 1;
                    if (next < Cups.Length) r.unlocked = $"{Cups[next].kicker} · {Cups[next].name} UNLOCKED";
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
