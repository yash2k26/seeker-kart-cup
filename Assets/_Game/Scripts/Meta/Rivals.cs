using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// "mert.skr beat your Palm Bay time by 0.4 s · RACE": who got past you on a board since you last looked. Each
    /// board (this week's time trial, then each circuit's all-time board) is remembered as the wallets ahead of you;
    /// a wallet ahead now that was not then passed you. One rival named, the closest; three or more are counted.
    /// Nothing on the first look (there is nothing to compare with), and at most two a day: a rival, not a nag.
    public static class Rivals
    {
        public sealed class Pass { public string text; public bool weekly; public int track; }

        [Serializable] sealed class Seen { public int myMs; public List<string> ahead = new(); }
        const string Key = "kc.rivals.", DayKey = "kc.rivals.day", CountKey = "kc.rivals.count";
        const int PerDay = 2;

        /// The newest pass worth a toast, or null. Every board looked at is remembered either way.
        public static async Awaitable<Pass> Check()
        {
            var wallet = WalletFlow.Instance != null && WalletFlow.Instance.Profile != null ? WalletFlow.Instance.Profile.wallet : null;
            if (string.IsNullOrEmpty(wallet)) return null;
            Pass found = null;
            try
            {
                var b = await Weekly.Fetch();
                if (b != null)
                {
                    var rows = b.entries.Select(e => (e.wallet, e.name, ms: e.bestMs)).ToList();
                    found = Compare($"w{b.week}", rows, wallet, Tracks.At(Mathf.Max(0, Weekly.TrackIndex)).name, "lap", weekly: true, track: Weekly.TrackIndex);
                }
                for (int i = 0; i < Tracks.Count; i++)
                {
                    var board = await KartApi.Leaderboard(Tracks.At(i).id);
                    if (board == null) continue;
                    var p = Compare(Tracks.At(i).id, board.entries.Select(e => (e.wallet, e.name, ms: e.bestMs)).ToList(), wallet, Tracks.At(i).name, "time", weekly: false, track: i);
                    found ??= p;
                }
            }
            catch (Exception e) { Debug.Log("[rivals] " + e.Message); }
            if (found == null || !Allowed()) return null;
            return found;
        }

        static Pass Compare(string key, List<(string wallet, string name, int ms)> rows, string wallet, string trackName, string what, bool weekly, int track)
        {
            int me = rows.FindIndex(r => r.wallet == wallet);
            Seen before = null;
            try { before = JsonUtility.FromJson<Seen>(PlayerPrefs.GetString(Key + key, "")); } catch { }
            var now = new Seen { myMs = me >= 0 ? rows[me].ms : 0, ahead = me >= 0 ? rows.Take(me).Select(r => r.wallet).ToList() : new List<string>() };
            PlayerPrefs.SetString(Key + key, JsonUtility.ToJson(now)); PlayerPrefs.Save();
            if (before == null || before.myMs == 0) return null;                        // the first look, or you were not on it
            if (me < 0) return new Pass { text = $"You were pushed off the {trackName} board", weekly = weekly, track = track };
            var passers = rows.Take(me).Where(r => !before.ahead.Contains(r.wallet)).ToList();
            if (passers.Count == 0) return null;
            var closest = passers[^1];
            float by = (rows[me].ms - closest.ms) / 1000f;
            return new Pass
            {
                text = passers.Count >= 3 ? $"{passers.Count} drivers passed you on {trackName}" : $"{closest.name} beat your {trackName} {what} by {by:0.00} s",
                weekly = weekly, track = track,
            };
        }

        static bool Allowed()
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            if (PlayerPrefs.GetString(DayKey, "") != today) { PlayerPrefs.SetString(DayKey, today); PlayerPrefs.SetInt(CountKey, 0); }
            int n = PlayerPrefs.GetInt(CountKey, 0);
            if (n >= PerDay) return false;
            PlayerPrefs.SetInt(CountKey, n + 1); PlayerPrefs.Save();
            return true;
        }
    }
}
