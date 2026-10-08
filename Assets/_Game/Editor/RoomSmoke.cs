using System.Collections.Generic;
using System.Linq;
using KartCup;
using PowerslideKartPhysics;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The online room, end to end, from the editor: host a real room on the local Worker (wrangler dev, MOCK_CHAIN=1),
// let a scripted Node opponent join, start, load the track, race it with the AI on our kart, and read the room's
// verdict off the results card. Proves the phone's side of the protocol without a phone.
// Run: cd worker && npx wrangler dev --port 8787   then   ./tools/unity.sh RoomSmoke.Run   (exits itself)
public static class RoomSmoke
{
    const string Base = "http://127.0.0.1:8787";
    static double start, stageAt;
    static int stage, failures;
    static string code;
    static System.Diagnostics.Process bot;
    static bool driving, sawRemote, remoteMoved, sawRacing, sawCountdown, sawIntro;
    static double podiumAt;
    static int podiumRacers;
    static string podiumTitle;
    static Vector3 remoteStart;
    static int lastLogged = -1, lastClockLog = -1;
    static float maxClockErr = float.MinValue;
    static readonly Queue<string> botLines = new();

    [MenuItem("Build/Room Smoke")]
    public static void Run()
    {
        EditorUtility.audioMasterMute = true;   // a headless test run must not play the game's sound through the laptop
        if (!EditorSettings.enterPlayModeOptionsEnabled || EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.DisableDomainReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            AssetDatabase.SaveAssets();
        }
        Cup.End();
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Menu.unity", OpenSceneMode.Single);
        start = EditorApplication.timeSinceStartup; stage = 0; failures = 0;
        driving = sawRemote = remoteMoved = sawRacing = sawCountdown = sawIntro = false; podiumAt = -1; podiumRacers = 0; podiumTitle = null; lastLogged = -1; lastClockLog = -1; maxClockErr = float.MinValue;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        AudioListener.volume = 0f;   // and silent in play mode too, whatever the game's settings set it to
        double t = EditorApplication.timeSinceStartup - start;
        lock (botLines) while (botLines.Count > 0) Debug.Log("[roomsmoke] " + botLines.Dequeue());
        var client = RoomClient.Instance;

        switch (stage)
        {
            case 0:
                if (t < 1.5) return;
                stage = -1; stageAt = t;
                Begin();
                return;
            case 1:   // waiting for the bot to appear
                if (client?.Room != null && client.Room.players.Count == 2)
                {
                    Check("bot joined the room", true);
                    Check("we are the host", client.IsHost);
                    client.Send(new { t = "start" });
                    stage = 2; stageAt = t;
                }
                else if (t - stageAt > 25) Fail("bot never joined");
                return;
            case 2:   // waiting for the track to load
                if (SceneManager.GetActiveScene().name == "Race1") { Check("room load message switched to the race scene", true); stage = 3; stageAt = t; }
                else if (t - stageAt > 30) Fail("race scene never loaded");
                return;
            case 3:
                Race(client, t);
                return;
            case 4:   // the room's verdict is in: the podium plays, then the card comes back over it
                Ceremony(client, t);
                return;
        }
    }

    static async void Begin()
    {
        try
        {
            KartApi.BaseUrl = Base;
            var created = await KartApi.CreateRoom(new List<string> { "beach" }, "0", "SOL", false);
            code = created.code;
            Check("created a room on the local Worker", !string.IsNullOrEmpty(code) && code.Length == 5);
            var client = RoomClient.Get();
            bool ok = await client.Join(code, 0, "oodi");
            Check("joined our own room (welcome received)", ok && client.Room != null && client.Room.players.Count == 1);
            Check("server clock offset learned from the welcome", client.Room != null && client.Room.serverNow > 0);

            var psi = new System.Diagnostics.ProcessStartInfo("node", $"scripts/bot.mjs {Base} {code}")
            {
                WorkingDirectory = System.IO.Path.GetFullPath("worker"), UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            bot = System.Diagnostics.Process.Start(psi);
            bot.OutputDataReceived += (_, e) => { if (e.Data != null) lock (botLines) botLines.Enqueue(e.Data); };
            bot.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (botLines) botLines.Enqueue("ERR " + e.Data); };
            bot.BeginOutputReadLine(); bot.BeginErrorReadLine();
            stage = 1; stageAt = EditorApplication.timeSinceStartup - start;
        }
        catch (System.Exception e) { Fail("setup threw: " + e.Message); }
    }

    static void Race(RoomClient client, double t)
    {
        var race = RaceManager.Instance;
        if (race == null || client == null) { if (t - stageAt > 20) Fail("no RaceManager in the race scene"); return; }

        // Our kart gets the AI, as in ContractTest, so it really completes two laps through the real gates.
        if (!driving && race.Player != null)
        {
            driving = true;
            var go = race.Player.gameObject;
            var mobile = go.GetComponent<KartInputMobile>();
            if (mobile != null) Object.DestroyImmediate(mobile);
            var ai = go.AddComponent<KartAi>();
            ai.SetTarget(Object.FindObjectsByType<BasicWaypoint>(FindObjectsSortMode.None).OrderBy(w => w.name).First());
            Check("our kart spawned as the player", race.Player.isPlayer && !race.Player.remote);
            Check("NetRace is bound in the race scene", NetRace.Instance != null);
            Check("item boxes are off online", Object.FindObjectsByType<ItemGiver>(FindObjectsSortMode.None).All(g => !g.gameObject.activeInHierarchy));
        }
        var remote = Object.FindFirstObjectByType<RemoteKart>();
        if (remote != null && !sawRemote) { sawRemote = true; remoteStart = remote.transform.position; Check("the bot has a remote kart on the grid", true); }
        if (sawRemote && !remoteMoved && remote != null && Vector3.Distance(remote.transform.position, remoteStart) > 1f)
        { remoteMoved = true; Check("remote kart moves from network states", true); }
        var show = PodiumShow.Instance;
        if (!sawIntro && show != null && show.Active && race.state == RaceManager.State.Idle)
        {
            sawIntro = true;
            Check("the prize shot is up while the grid waits", true);
            Check("a free room races for gold and bragging rights", Texts().Contains("RACE FOR GOLD") && Texts().Contains("BRAGGING RIGHTS"), string.Join(" | ", Texts()));
        }
        if (!sawCountdown && race.state == RaceManager.State.Countdown)
        {
            sawCountdown = true;
            Check("countdown started from the room clock", true);
            Check("the prize shot came first and gives way to the 3-2-1", sawIntro && (show == null || !show.Active));
        }
        if (!sawRacing && race.state == RaceManager.State.Racing) { sawRacing = true; Check("GO", true); }
        // The room refuses a race clock more than a second ahead of its own. The Worker runs on this machine, so the
        // system clock IS the server clock: log how far our race clock (and the clock fit behind it) is from the truth.
        if (race.state == RaceManager.State.Racing && client.Room?.startAt is long startAt && (int)(t * 2) != lastClockLog)
        {
            lastClockLog = (int)(t * 2);
            double truth = (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startAt) / 1000.0;
            var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var sw = (System.Diagnostics.Stopwatch)typeof(RoomClient).GetField("clock", f).GetValue(client);
            double offset = (double)typeof(RoomClient).GetField("offsetMs", f).GetValue(client);
            long bestRtt = client.PingMs;   // (RoomClient keeps a window of recent pings now, no single best)
            double trueOffset = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - sw.ElapsedMilliseconds;
            float err = (float)(NetRace.RaceSeconds - truth);
            maxClockErr = Mathf.Max(maxClockErr, err);
            if ((int)t % 10 == 0 || err > 0.5f)
                Debug.Log($"[roomsmoke] clock: race clock {NetRace.RaceSeconds:F2} s vs truth {truth:F2} s (err {err:+0.00;-0.00}) · fit offset err {offset - trueOffset:+0;-0} ms · ping {bestRtt} ms · unscaled-vs-stopwatch {Time.unscaledTimeAsDouble * 1000 - sw.ElapsedMilliseconds:F0} ms");
        }
        int tenSeconds = (int)((t - stageAt) / 10);
        if (tenSeconds >= 2 && tenSeconds != lastLogged && race.Player != null)
        {
            lastLogged = tenSeconds;
            Debug.Log($"[roomsmoke] t={t - stageAt:F0}s state={race.state} lap={race.Player.lap} cp={race.Player.checkpointIndex} speed={race.Player.GetComponent<Kart>().velMag:F1} remote={(remote != null ? remote.transform.position.ToString() : "-")}");
        }

        if (client.LastResults != null) { stage = 4; stageAt = t; return; }
        if (t - stageAt > 170) Fail("no results after 170 s");
    }

    static List<string> Texts() => Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Select(x => x.text).ToList();

    static void Ceremony(RoomClient client, double t)
    {
        var show = PodiumShow.Instance;
        if (podiumAt < 0 && show != null && show.Active)
        {
            podiumAt = t;
            podiumRacers = Object.FindObjectsByType<PodiumPose>(FindObjectsSortMode.None).Length;
            podiumTitle = Texts().FirstOrDefault(x => x.EndsWith("!"));
        }
        var dim = Object.FindFirstObjectByType<ResultsPanel>()?.transform.Find("Dim");
        bool card = dim != null && dim.gameObject.activeInHierarchy;
        if (podiumAt > 0 && card && t - podiumAt > 3) { Verify(client); return; }
        if (t - stageAt > 20) Verify(client);
    }

    static void Verify(RoomClient client)
    {
        Check("the podium played with both racers", podiumRacers == 2, $"racers {podiumRacers}");
        Check("the podium says YOU WIN!", podiumTitle == "YOU WIN!", podiumTitle ?? "<none>");
        var r = client.LastResults;
        Check("results arrived", r?.results != null);
        if (r?.results != null)
        {
            Check("single race is final", r.results.final && r.results.races == 1);
            Check("two racers in the order", r.results.order.Count == 2);
            var me = r.results.order.Find(o => o.id == client.MyId);
            Check("our finish was accepted by the room (not DNF)", me != null && !me.dnf && me.ms > 20000);
            Check("we won: the bot waited for our finish", me != null && me.place == 1);
            Check("the bot is second", r.results.order.Any(o => o.id != client.MyId && o.place == 2));
        }
        var headline = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Headline");
        var status = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Status");
        Check("results card headline reads YOU WIN", headline != null && headline.text == "YOU WIN", headline != null ? headline.text : "<none>");
        Check("results card status names our place", status != null && status.text.StartsWith("You finished 1st"), status != null ? status.text : "<none>");
        var primary = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "AgainLabel");
        Check("host sees RACE AGAIN", primary != null && primary.text == "RACE AGAIN", primary != null ? primary.text : "<none>");
        Debug.Log($"[roomsmoke] order: {string.Join(" | ", r?.results?.order.Select(o => $"{o.place}. {o.name} {(o.dnf ? "DNF" : o.ms + " ms")}") ?? new string[0])}");
        Finish();
    }

    static void Check(string what, bool ok, string extra = "")
    {
        if (!ok) failures++;
        Debug.Log($"[roomsmoke] {(ok ? "pass" : "FAIL")}  {what}{(ok || string.IsNullOrEmpty(extra) ? "" : "  → " + extra)}");
    }

    static void Fail(string why) { Check(why, false); Finish(); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        try { RoomClient.Instance?.Leave(); } catch { }
        try { if (bot != null && !bot.HasExited) bot.Kill(); } catch { }
        Debug.Log(failures == 0 ? "[roomsmoke] PASS — all checks" : $"[roomsmoke] {failures} FAILURE(S)");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
