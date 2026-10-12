using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartCup
{
    /// The player's progress kept with their wallet on the Worker (12 Oct 2026, the user: "even if it deletes the game and
    /// redownloads it and logs in with the same wallet, it should see all the details again"). One snapshot of every
    /// progress key (gems, chests and stars, Tour, achievements, recruits, items, loadout, picks, fuel, Daily, bests,
    /// spent trials) goes up when it changes; at sign-in and on every return to the menu the wallet's save is checked.
    /// What was bought (karts, drivers) already lives on the Worker (Unlocks). Settings, the device id, the session and
    /// in-flight state (an armed trial, a cup in progress, payments being checked) stay on the phone.
    ///   - the wallet has nothing saved: this phone's progress becomes the wallet's (a phone last used by another wallet
    ///     starts that one fresh instead; the other's progress is safe in its own save)
    ///   - the wallet has a save this phone is not in step with (a reinstall, another phone): it replaces this phone's
    ///     progress, guest progress included, and the menu reloads to show it
    ///   - a phone behind another phone's newer save never overwrites it (the Worker answers 409): it stops sending
    ///     until the next menu visit brings the newer one down
    public static class CloudSave
    {
        const string RevKey = "kc.save.rev", WalletKey = "kc.save.wallet";   // this phone's bookkeeping, never in the save

        static readonly string[] Ints = { "kc.gems", "kc.fuel", "kc.daily.done", "kc.spin.day", "kc.character", "kc.welcomed", "kc.starter.gems" };
        static readonly string[] Strings = { "kc.ach", "kc.chests", "kc.tour", "kc.earned", "kc.kart", "kc.paint", "kc.loadout", "kc.fuel.at", KartDatabase.TrialsKey };
        static IEnumerable<string> IntKeys => Ints.Concat(Inventory.Packs.Select(p => "kc.item." + p.id));
        static IEnumerable<string> FloatKeys =>                                     // a best per circuit, direction, laps and item rule
            from t in Tracks.All from rev in new[] { false, true } from laps in new[] { 1, 2, 3, 4 }
            from rule in new[] { RaceMode.Items.Normal, RaceMode.Items.Nitro, RaceMode.Items.Rockets }
            select Tracks.BestKey(RaceMode.BestKey(t.id, rev, laps, rule));

        sealed class Snap
        {
            public SortedDictionary<string, int> i = new();
            public SortedDictionary<string, float> f = new();
            public SortedDictionary<string, string> s = new();
        }

        /// Every progress key this phone holds, as one JSON text (the same progress always gives the same text).
        public static string Snapshot()
        {
            var n = new Snap();
            foreach (var k in IntKeys) if (PlayerPrefs.HasKey(k)) n.i[k] = PlayerPrefs.GetInt(k);
            foreach (var k in FloatKeys) if (PlayerPrefs.HasKey(k)) n.f[k] = PlayerPrefs.GetFloat(k);
            foreach (var k in Strings) if (PlayerPrefs.HasKey(k)) n.s[k] = PlayerPrefs.GetString(k);
            return JsonConvert.SerializeObject(n);
        }

        /// Makes this phone's progress exactly `json` (a key it lacks goes back to its default; a key that is not progress
        /// is ignored, so a save can never set the session or the device id) and drops what the game had read into memory.
        public static void Apply(string json)
        {
            Snap n = null;
            try { n = JsonConvert.DeserializeObject<Snap>(json ?? ""); } catch (Exception e) { Debug.LogWarning("[save] unreadable save: " + e.Message); }
            n ??= new Snap();
            HashSet<string> ints = new(IntKeys), floats = new(FloatKeys), strings = new(Strings);
            foreach (var k in ints.Concat(floats).Concat(strings)) PlayerPrefs.DeleteKey(k);
            foreach (var kv in n.i ?? new()) if (ints.Contains(kv.Key)) PlayerPrefs.SetInt(kv.Key, kv.Value);
            foreach (var kv in n.f ?? new()) if (floats.Contains(kv.Key)) PlayerPrefs.SetFloat(kv.Key, kv.Value);
            foreach (var kv in n.s ?? new()) if (strings.Contains(kv.Key)) PlayerPrefs.SetString(kv.Key, kv.Value ?? "");
            PlayerPrefs.Save();
            Gems.Reload(); Chests.Reload(); Achievements.Reload(); Tour.Reload();
        }

        static bool ready;       // this run is in step with the wallet's save: sending is allowed
        static bool busy;
        static string sent;      // the snapshot the wallet's save holds, as far as this run knows
        static int reloads;      // a guard: a menu that reloads to show a save must never reload forever

        /// Sign-in and every return to the menu (WalletFlow). Brings the wallet's save down, or sends this phone's up.
        public static async Awaitable Pull(string wallet)
        {
            if (busy || string.IsNullOrEmpty(wallet) || !KartApi.HasSession) return;
            busy = true;
            bool replaced = false;
            try
            {
                var r = await KartApi.SaveGet();
                if (r == null) return;
                string bound = PlayerPrefs.GetString(WalletKey, "");
                bool inStep = bound == wallet && PlayerPrefs.GetInt(RevKey, 0) == r.rev;
                if (r.rev == 0 && bound != "" && bound != wallet) { Apply("{}"); replaced = true; }   // another wallet's phone: start fresh
                else if (r.rev > 0 && !inStep) { Apply(r.data); replaced = true; }
                PlayerPrefs.SetString(WalletKey, wallet); PlayerPrefs.SetInt(RevKey, r.rev); PlayerPrefs.Save();
                if (replaced) sent = r.rev > 0 ? Snapshot() : null;   // in step: what was sent this run still stands
                else if (r.rev == 0) sent = null;
                ready = true;
                if (replaced) Debug.Log($"[save] {(r.rev > 0 ? "brought down save " + r.rev : "a fresh start")} for {wallet}");
            }
            catch (Exception e) { Debug.LogWarning("[save] pull: " + e.Message); }
            finally { busy = false; }
            if (replaced && ++reloads <= 2) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            else await Push();
        }

        /// Sends this phone's progress if it changed since the last send. Every 20 s, and when the app goes to the background.
        public static async Awaitable Push()
        {
            if (!ready || busy || !KartApi.HasSession) return;
            var snap = Snapshot();
            if (snap == sent) return;
            busy = true;
            try
            {
                var r = await KartApi.SavePut(PlayerPrefs.GetInt(RevKey, 0), snap);
                PlayerPrefs.SetInt(RevKey, r.rev); PlayerPrefs.Save();
                sent = snap;
            }
            catch (KartApi.ApiException e) when (e.Status == 409) { ready = false; Debug.LogWarning("[save] another phone saved first; the menu will bring it down"); }
            catch (Exception e) { Debug.LogWarning("[save] push: " + e.Message); }
            finally { busy = false; }
        }

        /// Signed out: nothing more goes up under this wallet. The phone keeps its progress.
        public static void SignedOut() { ready = false; sent = null; }

        sealed class Driver : MonoBehaviour
        {
            void Start() => InvokeRepeating(nameof(Tick), 20f, 20f);
            void Tick() => _ = Push();
            void OnApplicationPause(bool paused) { if (paused) _ = Push(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("CloudSave");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }
    }
}
