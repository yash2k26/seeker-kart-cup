using System.Collections.Generic;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

/// Every in-game currency under random use (12 Oct 2026, the user: "check ... every in-game currency ... stress it as much
/// as you can"): gems, fuel, chests and stars, items, the daily spin, judge recruits and Tour stars, tens of thousands of
/// random operations each, checked against a plain model of what should have happened and against the rules that must
/// always hold (nothing negative, nothing over its cap, nothing granted twice, saved exactly as held). The player's own
/// saved state is put back afterwards.       ./tools/unity.sh EconomyStress.Run
public static class EconomyStress
{
    static int failures, checks;
    static void Check(string what, bool ok, string extra = "")
    {
        checks++;
        if (ok) return;
        failures++;
        Debug.Log($"[economy] FAIL  {what}  {extra}");
    }

    // what the player has saved, by type (PlayerPrefs.GetString on an int key reads as missing)
    static readonly string[] IntKeys = { "kc.gems", "kc.fuel", "kc.spin.day", "kc.daily.done", "kc.item.rocket", "kc.item.homing", "kc.item.bomb", "kc.item.nitro", "kc.item.bouncer", "kc.item.sunbomb" };
    static readonly string[] StringKeys = { "kc.fuel.at", "kc.chests", "kc.unlocks", "kc.earned", "kc.loadout", "kc.tour", "kc.purchases", "kc.ach", "kc.carry", "kc.trials" };

    public static void Run()
    {
        // the player's saved economy, put back at the end whatever happens
        var ints = IntKeys.Where(PlayerPrefs.HasKey).ToDictionary(k => k, k => PlayerPrefs.GetInt(k));
        var strings = StringKeys.Where(PlayerPrefs.HasKey).ToDictionary(k => k, k => PlayerPrefs.GetString(k));
        var rng = new System.Random(20261012);
        Random.InitState(20261012);
        try
        {
            Chests.Karts = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
            GemsUnderStress(rng);
            FuelUnderStress(rng);
            ChestsUnderStress();
            ItemsUnderStress(rng);
            SpinUnderStress();
            RecruitsUnderStress();
            TourUnderStress(rng);
            DailyUnderStress();
            AchievementsUnderStress();
            CloudSaveRoundTrip();
            CarryRules();
        }
        catch (System.Exception e) { failures++; Debug.Log("[economy] FAIL  threw: " + e); }
        finally
        {
            foreach (var k in IntKeys) { PlayerPrefs.DeleteKey(k); if (ints.TryGetValue(k, out var v)) PlayerPrefs.SetInt(k, v); }
            foreach (var k in StringKeys) { PlayerPrefs.DeleteKey(k); if (strings.TryGetValue(k, out var v)) PlayerPrefs.SetString(k, v); }
            PlayerPrefs.Save();
            Fuel.Clock = () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        Debug.Log(failures == 0 ? $"[economy] PASS — all {checks} checks" : $"[economy] {failures} FAILURE(S) of {checks} checks");
        if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    // ---------- gems: earn and spend at random, the balance always the model's, never negative, saved as held ----------
    static void GemsUnderStress(System.Random rng)
    {
        PlayerPrefs.SetInt("kc.gems", 0); ResetCache(typeof(Gems));
        long model = 0;
        for (int i = 0; i < 30000; i++)
        {
            int n = rng.Next(-60, 700);
            if (rng.Next(2) == 0) { Gems.Earn(n, "stress"); if (n > 0) model = System.Math.Min(model + n, int.MaxValue); }
            else { bool ok = Gems.Spend(n, "stress"); bool should = n >= 0 && n <= model; if (ok != should) { Check($"gems: Spend({n}) at {model} answered {ok}", false); break; } if (ok) model -= n; }
            if (Gems.Balance != model || Gems.Balance < 0) { Check($"gems: after op {i} the balance is {Gems.Balance}, the model {model}", false); break; }
        }
        Check("gems: 30,000 random earns and spends keep the balance exact and never negative", Gems.Balance == model && Gems.Balance >= 0);
        Check("gems: the balance is saved as held", PlayerPrefs.GetInt("kc.gems") == Gems.Balance);
        Check("gems: a negative spend is refused (it would be a gift)", !Gems.Spend(-100, "x"));
        int before = Gems.Balance; Gems.Earn(-50, "x"); Gems.Earn(0, "x");
        Check("gems: earning nothing or less changes nothing", Gems.Balance == before);
        Gems.Earn(int.MaxValue, "x"); Gems.Earn(int.MaxValue, "x");
        Check("gems: a huge balance stops at the top, never wraps negative", Gems.Balance == int.MaxValue);
        foreach (int place in new[] { 1, 2, 3, 4, 6, 0, -1 }) { PlayerPrefs.SetInt("kc.gems", 0); ResetCache(typeof(Gems)); Gems.Podium(place); Check($"gems: a podium place {place} pays {new[] { 5, 3, 2 }.ElementAtOrDefault(place - 1)}", Gems.Balance == (place >= 1 && place <= 3 ? new[] { 5, 3, 2 }[place - 1] : 0)); }
    }

    // ---------- fuel: a clock that runs, jumps and runs backwards; spends, refunds and fills with gems ----------
    static void FuelUnderStress(System.Random rng)
    {
        long now = 1_800_000_000; Fuel.Clock = () => now;
        PlayerPrefs.DeleteKey("kc.fuel"); PlayerPrefs.DeleteKey("kc.fuel.at");
        Check("fuel: a new phone starts with a full tank", Fuel.Count == Fuel.Max && Fuel.Full && Fuel.SecondsToNext == 0);
        int spent = 0, refused = 0;
        for (int i = 0; i < 20000; i++)
        {
            int op = rng.Next(10);
            if (op < 4) { int c = Fuel.Count; bool ok = Fuel.TrySpend("stress"); if (ok) spent++; else refused++; if (ok != (c > 0)) { Check($"fuel: TrySpend at {c} answered {ok}", false); break; } if (ok && Fuel.Count != c - 1) { Check("fuel: a spend takes exactly one can", false); break; } }
            else if (op < 5) { int c = Fuel.Count; Fuel.Refund(); if (Fuel.Count != System.Math.Min(Fuel.Max, c + 1)) { Check($"fuel: a refund from {c} gave {Fuel.Count}", false); break; } }
            else if (op < 6) { int c = Fuel.Count; PlayerPrefs.SetInt("kc.gems", rng.Next(0, 60)); ResetCache(typeof(Gems)); int g = Gems.Balance; bool ok = Fuel.Fill(); bool should = c < Fuel.Max && g >= Fuel.FillPrice;
                               if (ok != should || (ok && (Fuel.Count != Fuel.Max || Gems.Balance != g - Fuel.FillPrice)) || (!ok && Gems.Balance != g)) { Check($"fuel: Fill at {c} cans and {g} gems answered {ok}, now {Fuel.Count} cans {Gems.Balance} gems", false); break; } }
            else if (op < 9) now += rng.Next(0, 1500);
            else now += rng.Next(-90000, 400000);        // the clock set back a day, or forward a few
            int count = Fuel.Count, wait = Fuel.SecondsToNext;
            if (count < 0 || count > Fuel.Max) { Check($"fuel: the tank holds {count}", false); break; }
            if (wait < 0 || wait > Fuel.RefillSeconds || (count == Fuel.Max) != (wait == 0 && Fuel.Full)) { Check($"fuel: {count} cans, next in {wait} s", false); break; }
        }
        Check($"fuel: 20,000 random spends, refunds, fills and clock jumps keep 0..{Fuel.Max} cans and a wait within {Fuel.RefillSeconds} s ({spent} spent, {refused} refused on an empty tank)", failures == 0);
        // the refill: one can every RefillSeconds, a full tank stops the clock, a clock set back never freezes the wait
        PlayerPrefs.SetInt("kc.fuel", 0); PlayerPrefs.SetString("kc.fuel.at", now.ToString());
        now += Fuel.RefillSeconds * 2 + 10; Check("fuel: two waits refill two cans", Fuel.Count == 2);
        now += Fuel.RefillSeconds * 100; Check("fuel: a long time away refills the tank, no more", Fuel.Count == Fuel.Max);
        Fuel.TrySpend("x"); long setAt = now; now -= 86400;
        Check("fuel: a clock set back a day shows at most one full wait", Fuel.SecondsToNext <= Fuel.RefillSeconds && Fuel.SecondsToNext > 0);
        now += Fuel.RefillSeconds + 1; Check("fuel: ...and the can still comes one wait later, not a day later", Fuel.Count == Fuel.Max);
    }

    // ---------- chests: thousands of podiums, the gold pity, the star meter, and every chest opened ----------
    static void ChestsUnderStress()
    {
        Chests.Preview(0);
        PlayerPrefs.SetInt("kc.gems", 0); ResetCache(typeof(Gems));
        var counts = new int[4]; int sinceGold = 0, worstDrought = 0;
        for (int i = 0; i < 6000; i++)
        {
            var (tier, stars, _) = Chests.Earn(1 + i % 3);
            if (tier == null) { Check("chests: a podium always earns a chest", false); break; }
            counts[(int)tier.Value]++;
            sinceGold = tier.Value >= Chests.Tier.Gold ? 0 : sinceGold + 1;
            worstDrought = Mathf.Max(worstDrought, sinceGold);
            if (Chests.Stars < 0 || Chests.Stars >= Chests.StarGoal) { Check($"chests: the meter reads {Chests.Stars}/{Chests.StarGoal}", false); break; }
        }
        Check($"chests: never more than {Chests.GoldPity - 1} podiums without a gold (worst {worstDrought})", worstDrought <= Chests.GoldPity - 1);
        Check("chests: off the podium earns nothing", Chests.Earn(4).tier == null && Chests.Earn(0).tier == null && Chests.Earn(-3).tier == null);
        Debug.Log($"[economy] 6000 podiums: wood {counts[0]} silver {counts[1]} gold {counts[2]} treasure {counts[3]}, {Chests.Pending} chests waiting");
        Chests.Preview(0);
        Chests.AddStars(100);
        // (Preview starts past the first fill, so the goal is 30: 100 stars fill it three times and leave 10)
        Check("chests: a big star gain fills the meter as often as it should", Chests.Pending == 3 && Chests.Stars == 10, $"{Chests.Pending} chests, {Chests.Stars} stars");
        Chests.AddStars(-40);
        Check("chests: negative stars change nothing", Chests.Stars < Chests.StarGoal && Chests.Stars >= 0);
        // open a big pile of every tier and check what comes out against each tier's promise
        Chests.Preview(0, Enumerable.Range(0, 400).Select(i => (Chests.Tier)(i % 4)).ToArray());
        var gemRange = new[] { (2, 5), (5, 10), (12, 20), (30, 45) };   // 13 Oct 2026: chests a quarter of what they were
        var itemsBefore = Inventory.Packs.ToDictionary(p => p.id, p => Inventory.Count(p.id));
        int gemsBefore = Gems.Balance, gemsSeen = 0, opened = 0; var itemsSeen = new Dictionary<string, int>();
        while (Chests.Pending > 0)
        {
            var o = Chests.Open(); if (o == null) break; opened++;
            var (t, rewards) = o.Value;
            var gem = rewards.Where(r => r.kind == "gems").ToList();
            if (gem.Count != 1 || gem[0].qty < gemRange[(int)t].Item1 || gem[0].qty > gemRange[(int)t].Item2) { Check($"chests: a {t} chest gave gems {string.Join(",", gem.Select(g => g.qty))}", false); break; }
            gemsSeen += gem[0].qty;
            int items = rewards.Where(r => r.kind == "item").Sum(r => r.qty), fuel = rewards.Where(r => r.kind == "fuel").Sum(r => r.qty);
            bool fair = t switch
            {
                Chests.Tier.Wood => items + fuel == 1,                     // an item or a can
                Chests.Tier.Silver => items == 1 && fuel == 1,
                Chests.Tier.Gold => items == 2 && fuel == 1,
                _ => items == 4 && fuel == 2,
            };
            if (!fair) { Check($"chests: a {t} chest gave {items} items and {fuel} cans", false); break; }
            foreach (var r in rewards.Where(r => r.kind == "item")) itemsSeen[r.id] = itemsSeen.GetValueOrDefault(r.id) + r.qty;
            if (t == Chests.Tier.Treasure && !rewards.Any(r => r.kind == "item" && r.id == "sunbomb")) { Check("chests: a golden treasure chest always has a sun bomb", false); break; }
            if (rewards.Any(r => r.kind == "paint" && Paints.Find(r.id) is { } p && p.skrOnly)) { Check("chests: never an SKR-only paint", false); break; }
        }
        Check($"chests: {opened} chests opened, each with its tier's items and gems", opened == 400 && Chests.Pending == 0);
        Check("chests: every gem shown was credited, once", Gems.Balance == gemsBefore + gemsSeen, $"{Gems.Balance} vs {gemsBefore}+{gemsSeen}");
        Check("chests: every item shown was put in the inventory, once", itemsSeen.All(kv => Inventory.Count(kv.Key) == System.Math.Min(9999, itemsBefore[kv.Key] + kv.Value)));
        Check("chests: opening with none waiting gives nothing", Chests.Open() == null);
        Check("chests: the weekly claim pays once a week", Chests.ClaimWeekly(3000, true) && !Chests.ClaimWeekly(3000, true) && !Chests.ClaimWeekly(2999, false) && Chests.ClaimWeekly(3001, false));
    }

    // ---------- items: adds and takes at random, never negative, never past the cap ----------
    static void ItemsUnderStress(System.Random rng)
    {
        var model = Inventory.Packs.ToDictionary(p => p.id, p => 0);
        foreach (var p in Inventory.Packs) PlayerPrefs.SetInt("kc.item." + p.id, 0);
        for (int i = 0; i < 20000; i++)
        {
            var id = Inventory.Packs[rng.Next(Inventory.Packs.Length)].id;
            if (rng.Next(3) == 0) { int n = rng.Next(-5, 30); Inventory.Add(id, n); if (n > 0) model[id] = System.Math.Min(9999, model[id] + n); }
            else { bool ok = Inventory.Take(id); if (ok != (model[id] > 0)) { Check($"items: Take({id}) at {model[id]} answered {ok}", false); break; } if (ok) model[id]--; }
            if (Inventory.Count(id) != model[id] || Inventory.Count(id) < 0) { Check($"items: {id} holds {Inventory.Count(id)}, the model {model[id]}", false); break; }
        }
        Check("items: 20,000 random adds and takes match the model, never negative", Inventory.Packs.All(p => Inventory.Count(p.id) == model[p.id]));
        Inventory.Add(null, 5); Inventory.Add("", 5);
        Check("items: a pack with no id is ignored", Inventory.Count("") == 0);
    }

    // ---------- the daily spin: once a UTC day, and what it lands on is paid ----------
    static void SpinUnderStress()
    {
        PlayerPrefs.DeleteKey("kc.spin.day");
        Chests.Preview(0);
        Check("spin: ready on a new day", DailySpin.Ready);
        int i = DailySpin.Spin();
        Check("spin: lands on a slice", i >= 0 && i < DailySpin.Wheel.Length);
        Check("spin: a second spin the same day is refused", DailySpin.Spin() == -1 && !DailySpin.Ready);
        var p = DailySpin.Wheel[i];
        bool paid = p.kind switch { DailySpin.Kind.Chest => Chests.Pending == 1, DailySpin.Kind.Stars => Chests.Stars == p.amount, _ => Inventory.Count(p.item) >= p.amount };
        Check($"spin: its prize ({p.label}) was paid", paid);
        int[] landed = new int[DailySpin.Wheel.Length];
        for (int k = 0; k < 8000; k++) { PlayerPrefs.DeleteKey("kc.spin.day"); int s = DailySpin.Spin(); if (s >= 0) landed[s]++; }
        Check($"spin: 8000 spins land on every slice about as often ({string.Join(" ", landed)})", landed.All(c => c > 800 && c < 1200));
    }

    // ---------- the judges: each recruited for their price in gems, once, and never on credit ----------
    static void RecruitsUnderStress()
    {
        var roster = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        var judges = roster != null ? roster.characters.Where(d => d != null && !d.IsFree).ToList() : new List<CharacterDef>();
        Check("recruits: the five judges are locked (Mert, Akshay, Chase, Toly, Beeman)", judges.Select(j => j.displayName).OrderBy(n => n).SequenceEqual(new[] { "Akshay", "Beeman", "Chase", "Mert", "Toly" }), string.Join(",", judges.Select(j => j.displayName)));
        Unlocks.Clear(); PlayerPrefs.DeleteKey("kc.earned");
        foreach (var d in judges)
        {
            int price = Gems.PriceOf(d);
            PlayerPrefs.SetInt("kc.gems", price - 1); ResetCache(typeof(Gems));
            Check($"recruits: {d.displayName} cannot be had for {price - 1} gems", !Gems.Spend(price, d.sku) && !CharacterDatabase.Owned(d) && Gems.Balance == price - 1);
            PlayerPrefs.SetInt("kc.gems", price); ResetCache(typeof(Gems));
            bool ok = Gems.Spend(price, d.sku); if (ok) Unlocks.Earn(d.sku);
            Check($"recruits: {d.displayName} for {price} gems leaves 0 and drives for you", ok && Gems.Balance == 0 && CharacterDatabase.Owned(d));
        }
        Check("recruits: the prices rise with the Tour (100, 100, 200, 300, 400)", judges.Select(Gems.PriceOf).OrderBy(x => x).SequenceEqual(new[] { 100, 100, 200, 300, 400 }));
    }

    // ---------- Tour stars: a best kept, never lowered, the total never past the maximum ----------
    static void TourUnderStress(System.Random rng)
    {
        PlayerPrefs.DeleteKey("kc.tour"); ResetCache(typeof(Tour), "state");
        var ids = Tour.Cups.SelectMany(c => c.events).Where(e => e.mode != RaceMode.Kind.Smash).Select(e => e.id).ToArray();
        var best = ids.ToDictionary(i => i, i => 0);
        for (int k = 0; k < 5000; k++)
        {
            var id = ids[rng.Next(ids.Length)]; int place = rng.Next(-1, 8);
            var (_, ev) = Tour.Find(id);
            int stars = ev.mode == RaceMode.Kind.Duel ? (place == 1 ? 3 : 0) : place >= 1 && place <= 3 ? 4 - place : 0;
            Tour.Record(id, place, 60f);
            best[id] = Mathf.Max(best[id], stars);
            if (Tour.Stars(id) != best[id]) { Check($"tour: {id} after place {place} holds {Tour.Stars(id)} stars, the model {best[id]}", false); break; }
        }
        Check("tour: 5000 random results keep each event's best, never lower it", ids.All(i => Tour.Stars(i) == best[i]));
        Check($"tour: the total never passes {Tour.MaxStars}", Tour.Total <= Tour.MaxStars && Tour.Total == best.Values.Sum());
        Check("tour: an unknown event records nothing", Tour.Record("nope", 1) == null);
    }

    // ---------- the Daily Challenge: every circuit and mode in turn, paid once a day, judged by the day it started ----------
    static void DailyUnderStress()
    {
        int day0 = Daily.Today;
        var seen = new int[Tracks.Count]; var modes = new HashSet<RaceMode.Kind>();
        for (int d = day0; d < day0 + 400; d++) { var c = Daily.For(d); seen[c.track]++; modes.Add(c.mode); }
        Check($"daily: 400 days visit every circuit evenly ({string.Join(" ", seen)}) and all three modes", seen.All(n => n >= 70) && modes.Count == 3);
        for (int d = day0; d < day0 + 3; d++)
        {
            var c = Daily.For(d);
            PlayerPrefs.DeleteKey("kc.daily.done"); Chests.Preview(0); PlayerPrefs.SetInt("kc.gems", 0); ResetCache(typeof(Gems));
            Daily.Arm(c);
            int miss = c.mode == RaceMode.Kind.Duel ? 2 : 4, hit = c.mode == RaceMode.Kind.Duel ? 1 : 3;
            Check($"daily {c.mode}: place {miss} does not meet '{c.goal}'", !Daily.Record(miss));
            Check($"daily {c.mode}: place {hit} meets it and pays a silver chest, 15 gems and 3 stars", Daily.Record(hit) && Chests.Pending == 1 && Gems.Balance == 15 && Chests.Stars == 3);
            Check($"daily {c.mode}: paid once that day", !Daily.Record(hit) && Chests.Pending == 1 && Gems.Balance == 15);
        }
        // the race started yesterday and finished after midnight: yesterday's challenge, yesterday marked
        PlayerPrefs.DeleteKey("kc.daily.done");
        var yesterday = Daily.For(day0 - 1); Daily.Arm(yesterday);
        Daily.Record(yesterday.mode == RaceMode.Kind.Duel ? 1 : 1);
        Check("daily: a race from before midnight is yesterday's, and today's is still to do", PlayerPrefs.GetInt("kc.daily.done") == day0 - 1 && !Daily.Done);
        int me = CharacterDatabase.SelectedIndex, clashes = 0;
        for (int d = day0; d < day0 + 200; d++) { var c = Daily.For(d); if (c.mode != RaceMode.Kind.Duel) continue; Daily.Arm(c); if (RaceMode.Rival == me) clashes++; }
        Check("daily: a duel is never against your own driver", clashes == 0);
        RaceMode.Clear();
    }

    // ---------- carrying bought items into a race: two kinds, three of each, never more than held ----------
    static void CarryRules()
    {
        void Hold(string id, int n) { PlayerPrefs.SetInt("kc.item." + id, n); }
        foreach (var p in Inventory.Packs) Hold(p.id, 0);
        Hold("rocket", 7); Hold("bomb", 1); Hold("homing", 2);
        Carry.Stack S(string id, int n) => new() { id = id, n = n };
        var got = Carry.Clamp(new[] { S("rocket", 5), S("bomb", 3), S("homing", 2) });
        Check("carry: two kinds at most, three of each at most, never more than held", Carry.Format(got) == "rocket:3,bomb:1", Carry.Format(got));
        got = Carry.Clamp(new[] { S("nitro", 2), S("ghost", 3), S("homing", 3), S("homing", 1), S("rocket", 0), S("sunbomb", 1) });
        Check("carry: none held, unknown, repeated and zero are dropped", Carry.Format(got) == "homing:2", Carry.Format(got));
        Check("carry: more than one held asks before the race", Carry.NeedsPicker && Carry.Auto().Count == 0);
        foreach (var p in Inventory.Packs) Hold(p.id, 0);
        Hold("sunbomb", 1);
        Check("carry: exactly one held goes in by itself, no question", !Carry.NeedsPicker && Carry.Format(Carry.Auto()) == "sunbomb:1", Carry.Format(Carry.Auto()));
        Hold("sunbomb", 0);
        Check("carry: nothing held, nothing carried, no question", !Carry.NeedsPicker && Carry.Auto().Count == 0);
        Hold("bomb", 2); Carry.Remember(new() { S("bomb", 3), S("rocket", 2) });
        Check("carry: the last choice comes back checked against what is held now", Carry.Format(Carry.Last) == "bomb:2", Carry.Format(Carry.Last));
        Check("carry: a choice reads back as written", Carry.Format(Carry.Parse("rocket:3,bomb:1")) == "rocket:3,bomb:1" && Carry.Parse("junk,::,x:y").Count == 0);
    }

    // ---------- cloud save: everything a player has made survives a wipe (a reinstall) and comes back exactly ----------
    static void CloudSaveRoundTrip()
    {
        var backup = CloudSave.Snapshot();
        try
        {
            CloudSave.Apply("{}");
            Check("cloud save: a wiped phone reads the defaults (20 gems, no items, no best, no trial spent)",
                  Gems.Balance == 20 && Inventory.Count("rocket") == 0 && Tracks.Best("beach") == 0f && !KartDatabase.TrialUsed("k_test"));
            Gems.Earn(757, "test");
            Inventory.Add("rocket", 5); Carry.Remember(new() { new Carry.Stack { id = "rocket", n = 3 } });
            Tracks.RecordBest("beach", 61.25f); Tracks.RecordBest(RaceMode.BestKey("alpine", true, 2, RaceMode.Items.Nitro), 70.5f);
            KartDatabase.SpendTrial("k_test"); Unlocks.Earn("driver.test");
            Chests.AddStars(4); Chests.Give(Chests.Tier.Gold);
            Tour.Record(Tour.Cups[0].events[0].id, 1, 50f);
            var made = CloudSave.Snapshot();
            int stars = Tour.Stars(Tour.Cups[0].events[0].id);

            CloudSave.Apply("{}");
            Check("cloud save: a wipe drops what the game had in memory too", Gems.Balance == 20 && Tour.Stars(Tour.Cups[0].events[0].id) == 0);
            CloudSave.Apply(made);
            Check("cloud save: the save brings back gems, items, loadout, bests, trials, recruits, Tour",
                  Gems.Balance == 777 && Inventory.Count("rocket") == 5 && Carry.Format(Carry.Last) == "rocket:3" && Mathf.Approximately(Tracks.Best("beach"), 61.25f)
                  && Mathf.Approximately(Tracks.Best(RaceMode.BestKey("alpine", true, 2, RaceMode.Items.Nitro)), 70.5f)
                  && KartDatabase.TrialUsed("k_test") && Unlocks.Has("driver.test") && stars > 0 && Tour.Stars(Tour.Cups[0].events[0].id) == stars,
                  $"gems {Gems.Balance} rocket {Inventory.Count("rocket")} best {Tracks.Best("beach")} tour {Tour.Stars(Tour.Cups[0].events[0].id)}/{stars}");
            Check("cloud save: the same progress gives the same snapshot (no needless saves)", CloudSave.Snapshot() == made);
            CloudSave.Apply("{\"i\":{\"kc.gems\":5,\"kc.device\":1},\"s\":{\"kc.session\":\"forged\",\"kc.unlocks\":\"kart.gold\"}}");
            Check("cloud save: a save sets only progress, never the session, device id or bought unlocks",
                  Gems.Balance == 5 && PlayerPrefs.GetString("kc.session", "") != "forged" && !Unlocks.Has("kart.gold"));
            CloudSave.Apply("not json {");
            Check("cloud save: an unreadable save is a fresh start, not a crash", Gems.Balance == 20);
        }
        finally { CloudSave.Apply(backup); }
    }

    // ---------- achievements: done waits for a claim, a claim pays once, an old save's done ones count as claimed ----------
    static void AchievementsUnderStress()
    {
        PlayerPrefs.DeleteKey("kc.ach"); ResetCache(typeof(Achievements), "s");
        Chests.Preview(0);
        Achievements.Add(Achievements.Stat.Races, 50);
        var all = Achievements.All.Where(d => d.stat == Achievements.Stat.Races).ToList();
        Check("achievements: 50 races finish the three race ones", all.All(Achievements.IsDone) && Achievements.ClaimableCount == 3);
        Check("achievements: done is not paid until claimed", Chests.Pending == 0 && Chests.Stars == 0);
        int stars = all.Sum(d => d.stars);
        Check("achievements: a claim pays its chest and stars, once", Achievements.Claim(all[0]) && !Achievements.Claim(all[0]) && Chests.Pending == 1);
        foreach (var d in all) Achievements.Claim(d);
        Check("achievements: claiming the rest pays the rest", Achievements.ClaimableCount == 0 && Chests.Pending == 3, $"{Chests.Pending} chests");
        Check("achievements: a not-done one cannot be claimed", !Achievements.Claim(Achievements.All.First(d => d.id == "wins-25")));
        // a save from before claims (no version): what it had done was paid then, so it is claimed
        PlayerPrefs.SetString("kc.ach", "{\"counts\":[3,1,1,0,0,0,0,0,0,0,0,0,0,0,0],\"done\":[\"races-1\",\"wins-1\"],\"tracksWon\":[]}");
        ResetCache(typeof(Achievements), "s");
        var w1 = Achievements.All.First(d => d.id == "wins-1");
        Check("achievements: an old save's done ones are claimed already, nothing paid twice", Achievements.IsClaimed(w1) && !Achievements.CanClaim(w1) && Achievements.ClaimableCount == 0);
        Check("achievements: five circuits and five bosses (Mount Ember, Beeman)", Achievements.All.First(d => d.id == "tracks-4").goal == 5 && Achievements.All.First(d => d.id == "bosses-4").goal == 5);
    }

    /// The static caches some of these keep (Gems' balance, Tour's state): cleared so the next read is from PlayerPrefs.
    static void ResetCache(System.Type t, string field = "cache")
    {
        var f = t.GetField(field, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        f?.SetValue(null, null);
    }
}
