using System;
using UnityEngine;

namespace KartCup
{
    /// Fuel, Beach Buggy's race tickets (the user, 11 Oct 2026: "cash ... which only gets deducted when you do a race ...
    /// it gets 5 to 4"): a tank of Max cans, one spent on every solo race start, one back every RefillSeconds, and the
    /// can back when the race is won (BBR1 refunds the winner's ticket). Restarting from the pause menu is free, as in
    /// Beach Buggy. Never on an online room (other players are waiting) or Kartel Weekly (a cash prize: extra attempts
    /// must not be for sale). A full tank costs FillPrice gems. Kept on the phone like gems.
    /// ponytail: a PlayerPrefs count and the time of the last refill; a Worker-side ledger if the clock gets gamed.
    public static class Fuel
    {
        public const int Max = 6;
        public const int RefillSeconds = 600;
        public const int FillPrice = 25;

        const string CountKey = "kc.fuel", AtKey = "kc.fuel.at";
        /// The clock, unix seconds (a test sets its own).
        public static Func<long> Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        /// (before, after): the chips animate a spend and a refill.
        public static event Action<int, int> Changed;
        /// Whether the race just run handed its can back (the results card says so).
        public static bool LastRefund { get; private set; }

        public static int Count { get { Tick(); return PlayerPrefs.GetInt(CountKey, Max); } }
        public static bool Full => Count >= Max;

        /// Seconds until the next can, 0 with a full tank.
        public static int SecondsToNext
        {
            get
            {
                if (Count >= Max) return 0;
                long at = At();
                return (int)Math.Max(0, RefillSeconds - (Clock() - at));
            }
        }

        /// "8:32" until the next can.
        public static string NextIn() { int s = SecondsToNext; return $"{s / 60}:{s % 60:00}"; }

        /// One can for a race; false with an empty tank (the caller shows FuelPanel).
        public static bool TrySpend(string race)
        {
            int c = Count;
            if (c <= 0) return false;
            if (c >= Max) SetAt(Clock());                 // the refill clock starts with the first can out of a full tank
            Set(c, c - 1);
            LastRefund = false;
            KartApi.Event("fuel_spent", new { race, left = c - 1 });
            return true;
        }

        /// A won race hands its can back.
        public static void Refund()
        {
            int c = Count;
            LastRefund = c < Max;
            if (c < Max) Set(c, c + 1);
        }

        /// A full tank for FillPrice gems.
        public static bool Fill()
        {
            int c = Count;
            if (c >= Max || !Gems.Spend(FillPrice, "fuel")) return false;
            Set(c, Max);
            return true;
        }

        public static void ClearLast() => LastRefund = false;

        /// Cans the clock has refilled since the last look.
        static void Tick()
        {
            int c = PlayerPrefs.GetInt(CountKey, Max);
            if (c >= Max) return;
            long now = Clock(), at = At();
            long gained = (now - at) / RefillSeconds;
            if (gained <= 0) return;
            int next = (int)Math.Min(Max, c + gained);
            SetAt(next >= Max ? now : at + gained * RefillSeconds);
            PlayerPrefs.SetInt(CountKey, next); PlayerPrefs.Save();
            Changed?.Invoke(c, next);
        }

        static long At() => long.TryParse(PlayerPrefs.GetString(AtKey, ""), out long v) ? v : Clock();
        static void SetAt(long t) => PlayerPrefs.SetString(AtKey, t.ToString());
        static void Set(int before, int after) { PlayerPrefs.SetInt(CountKey, after); PlayerPrefs.Save(); Changed?.Invoke(before, after); }

#if UNITY_EDITOR
        /// Tests: a tank of `count` cans, its last refill `ago` seconds back.
        public static void Preview(int count, long ago = 0) { PlayerPrefs.SetInt(CountKey, count); SetAt(Clock() - ago); PlayerPrefs.Save(); }
#endif
    }
}
