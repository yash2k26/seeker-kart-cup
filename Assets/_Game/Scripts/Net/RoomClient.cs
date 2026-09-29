using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KartCup
{
    /// The phone's end of a room: one WebSocket to the Worker's Durable Object, the room snapshot everybody shares,
    /// and a server clock offset so every phone starts its 3-2-1 on the same instant. Lives across scene loads
    /// (lobby → track → results → next track) and is torn down by Leave().
    /// Transport is System.Net.WebSockets, which ships with the player: no package to import.
    public sealed class RoomClient : MonoBehaviour
    {
        public static RoomClient Instance { get; private set; }
        public static bool InRoom => Instance != null && Instance.Room != null && Instance.Connected;
        /// The race scene reads this to decide between the solo grid and the room's roster.
        public static bool InRace => InRoom && (Instance.Room.state == "loading" || Instance.Room.state == "countdown" || Instance.Room.state == "racing");
        /// In a room's race whether or not the socket is up this instant: a scene that starts during a reconnect is still
        /// the room's race, not a solo one.
        public static bool InRaceRoom => Instance != null && Instance.Room is { } r && (r.state == "loading" || r.state == "countdown" || r.state == "racing");
        /// A member of a room (lobby, race or results), socket up or not.
        public static bool Member => Instance != null && Instance.Room != null;

        // ---- events (main thread) ----
        public static event Action<RoomState> RoomChanged;
        public static event Action<string, int, int> LoadRequested;         // trackId, race number, races
        public static event Action<double> CountdownAt;                       // GO, in Time.unscaledTimeAsDouble terms
        public static event Action<JObject> StateReceived;
        public static event Action<string, long, bool> RemoteFinished;        // id, ms, dnf
        public static event Action<ResultsMsg> ResultsReceived;
        public static event Action<List<Payout>> PayoutsChanged;
        public static event Action<string> Error;
        public static event Action<string> Closed;
        public static event Action Welcomed;                                  // after every welcome, a reconnect's included

        public string Code { get; private set; }
        public string MyId { get; private set; }
        public RoomState Room { get; private set; }
        public ResultsMsg LastResults { get; private set; }
        public bool Connected => ws != null && ws.State == WebSocketState.Open;
        /// Connected and welcomed on this socket: only then does the room know who we are.
        public bool Ready => Connected && welcomed == true;
        public bool IsHost => Room != null && MyId != null && Room.hostId == MyId;
        public PlayerState Me => Room?.players?.Find(p => p.id == MyId);
        public int PingMs { get; private set; } = -1;

        ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly ConcurrentQueue<(string text, long at)> inbox = new();   // each message with the stopwatch time it arrived
        readonly SemaphoreSlim sendLock = new(1, 1);
        readonly Stopwatch clock = Stopwatch.StartNew();
        double offsetMs = double.NaN;                 // serverMs - localMs
        // The last few pings: the least-delayed one's offset is the truest. Recent ones only, so the fit follows the
        // network as it changes and recovers after the phone sleeps (its stopwatch may stop while the server's runs on).
        readonly Queue<(long rtt, double sample)> pings = new();
        const int PingWindow = 15;
        // Stopwatch ms minus Time.unscaledTime ms. unscaledTime is the time the frame STARTED; in a heavy frame (the race
        // scene's first) a handler runs most of a second later, so this reads too large by that delay. Its minimum over
        // frames is the true relation, and every server time is mapped through that.
        double swMinusUnscaled = double.PositiveInfinity;
        float pingTimer;
        bool wantReconnect, reconnecting;
        int helloCharacter; string helloKart;

        // ---- DTOs ----
        [Serializable] public class PlayerState { public string id, name, wallet, kart; public int character, points; public bool host, connected, ready, paid, paying, loaded, racing, dnf; public bool canPay = true; public long? finishMs; public int? place; }
        [Serializable] public class Standing { public string id, name; public int points, place; public long totalMs; }
        [Serializable] public class OrderRow { public string id, name; public long? ms; public int place, points; public bool dnf; }
        [Serializable] public class RaceResult { public int race, races; public string track; public bool final; public List<OrderRow> order = new(); }
        [Serializable] public class Payout { public string id, wallet, kind, amount, currency, status, signature; public int? place; }
        [Serializable] public class RoomState
        {
            public string code, state, currency, fee = "0", hostId, pool = "0", hostReward = "0";
            public int round, raceIndex, cutBps, feeBps;
            public bool isPublic;
            public List<string> tracks = new(), prizes = new();
            public long? startAt; public long serverNow;
            public List<PlayerState> players = new();
            public List<Standing> standings; public RaceResult lastResults; public List<Payout> payouts = new();
            public bool IsPaid => fee != "0";
        }
        [Serializable] public class ResultsMsg { public RaceResult results; public List<Standing> standings; public List<Payout> payouts; public string pool, cut; public RoomState room; }

        // ---- lifecycle ----
        public static RoomClient Get()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RoomClient");
            DontDestroyOnLoad(go);
            return go.AddComponent<RoomClient>();
        }

        void Awake() { Instance = this; }

        /// Connect to a room and introduce ourselves. Resolves once the room has answered (welcome) or refused (error).
        public async Awaitable<bool> Join(string code, int character, string kart)
        {
            Code = code.ToUpperInvariant(); helloCharacter = character; helloKart = kart;
            wantReconnect = true;
            return await Connect();
        }

        async Awaitable<bool> Connect()
        {
            Close();
            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();
            var url = KartApi.BaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + $"/v1/rooms/{Code}/ws";
            try { await ws.ConnectAsync(new Uri(url), cts.Token); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[room] connect failed: " + e.Message); Error?.Invoke("Can't reach the room. Check your connection."); return false; }
            _ = Task.Run(() => ReceiveLoop(ws, cts.Token));
            Send(new { t = "hello", device = KartApi.DeviceId, session = KartApi.HasSession ? KartApi.SessionToken : null, character = helloCharacter, kart = helloKart });
            Send(new { t = "ping", c = clock.ElapsedMilliseconds });

            // wait for welcome or error, up to 8 s
            var until = Time.unscaledTimeAsDouble + 8.0;
            welcomed = null;
            while (welcomed == null && Time.unscaledTimeAsDouble < until && Connected) await Awaitable.NextFrameAsync();
            return welcomed == true;
        }
        bool? welcomed;

        async Task ReceiveLoop(ClientWebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();
            try
            {
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var r = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (r.MessageType == WebSocketMessageType.Close) { closeReason = r.CloseStatusDescription ?? ""; break; }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    if (!r.EndOfMessage) continue;
                    var text = sb.ToString(); sb.Clear();
                    // The room timing our round trip: answer from this thread, before any frame can delay it.
                    if (text.StartsWith("{\"t\":\"sp\",")) { _ = SendBytes(Encoding.UTF8.GetBytes("{\"t\":\"spr\"," + text.Substring(10))); continue; }
                    inbox.Enqueue((text, clock.ElapsedMilliseconds));
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException)) { UnityEngine.Debug.LogWarning("[room] socket: " + e.Message); }
            inbox.Enqueue(("\u0000closed|" + closeReason, clock.ElapsedMilliseconds));
        }
        volatile string closeReason = "";

        public void Send(object o)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(o, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            _ = SendBytes(bytes);
        }

        async Task SendBytes(byte[] bytes)
        {
            await sendLock.WaitAsync();
            try { if (ws != null && ws.State == WebSocketState.Open) await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[room] send: " + e.Message); }
            finally { sendLock.Release(); }
        }

        void Update()
        {
            swMinusUnscaled = Math.Min(swMinusUnscaled, clock.ElapsedMilliseconds - Time.unscaledTimeAsDouble * 1000.0);
            while (inbox.TryDequeue(out var item))
            {
                if (item.text.StartsWith("\u0000closed|")) { OnSocketClosed(item.text.Substring(8)); continue; }
                try { Dispatch(JObject.Parse(item.text), item.at); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[room] bad message: " + e.Message); }
            }
            if (Connected)
            {
                pingTimer -= Time.unscaledDeltaTime;
                if (pingTimer <= 0f) { pingTimer = 2f; Send(new { t = "ping", c = clock.ElapsedMilliseconds }); }
            }
        }

        void Dispatch(JObject m, long at)
        {
            switch ((string)m["t"])
            {
                case "welcome":
                    MyId = (string)m["id"];
                    NoteServerTime((long)m["serverNow"], at, -1);
                    SetRoom(m["room"]);
                    welcomed = true;
                    _ = PendingEntries.Flush();          // any entry payment the room may not have heard of yet
                    Welcomed?.Invoke();
                    break;
                case "room": SetRoom(m["room"]); break;
                case "pong":
                {
                    long c = (long)m["c"], s = (long)m["s"], rtt = at - c;
                    PingMs = (int)rtt;
                    NoteServerTime(s, c + rtt / 2, rtt);
                    break;
                }
                case "load":
                    LastResults = null;
                    // The snapshot saying 'loading' comes a moment later; the race scene decides online or solo from the
                    // snapshot as it starts, so it must already say so.
                    if (Room != null) { Room.state = "loading"; Room.raceIndex = (int)m["race"] - 1; Room.startAt = null; }
                    LoadRequested?.Invoke((string)m["track"], (int)m["race"], (int)m["races"]);
                    break;
                case "countdown":
                    NoteServerTime((long)m["serverNow"], at, -1);
                    CountdownAt?.Invoke(ServerToLocalUnscaled((long)m["startAt"]));
                    break;
                case "s": StateReceived?.Invoke(m); break;
                case "finished": RemoteFinished?.Invoke((string)m["id"], m["ms"]?.Type == JTokenType.Null ? 0 : (long)(m["ms"] ?? 0), (bool)(m["dnf"] ?? false)); break;
                case "results":
                    LastResults = m.ToObject<ResultsMsg>();
                    if (LastResults.room != null) { Room = LastResults.room; RoomChanged?.Invoke(Room); }
                    ResultsReceived?.Invoke(LastResults);
                    break;
                case "payouts":
                    var list = m["payouts"]?.ToObject<List<Payout>>() ?? new List<Payout>();
                    if (Room != null) Room.payouts = list;
                    if (LastResults != null) LastResults.payouts = list;
                    PayoutsChanged?.Invoke(list);
                    break;
                case "error":
                    // refused before any welcome (kicked, started without us, full): trying again would be refused again
                    if (welcomed == null) { welcomed = false; wantReconnect = false; }
                    Error?.Invoke((string)m["msg"]);
                    break;
                case "closed":
                    wantReconnect = false;
                    Closed?.Invoke((string)m["reason"]);
                    break;
            }
        }

        void SetRoom(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return;
            Room = token.ToObject<RoomState>();
            if (Room.state == "loading") LastResults = null;
            RoomChanged?.Invoke(Room);
            // Back after the verdict went out (a reconnect): the room's snapshot carries it, so the results card gets it.
            if ((Room.state == "results" || Room.state == "finished") && Room.lastResults != null
                && (LastResults?.results == null || LastResults.results.race != Room.lastResults.race || LastResults.room?.round != Room.round))
            {
                LastResults = new ResultsMsg { results = Room.lastResults, standings = Room.standings, payouts = Room.payouts, room = Room };
                ResultsReceived?.Invoke(LastResults);
            }
        }

        // ---- clocks: the server's ms epoch mapped onto this phone's stopwatch ----
        // The room times every race by it, and refuses a race clock that runs ahead of its own or lags behind by
        // more than the network explains. Every sample is stamped when it ARRIVED (the receive thread), not when a
        // frame got round to it: a scene load held messages for seconds and skewed the old fit by as much.
        void NoteServerTime(long serverMs, long localMs, long rtt)
        {
            double sample = serverMs - localMs;
            if (rtt >= 0)
            {
                // A ping's round trip bounds the error to half of it; the quickest recent round trip is the truest.
                pings.Enqueue((rtt, sample));
                while (pings.Count > PingWindow) pings.Dequeue();
                offsetMs = pings.Aggregate((a, b) => b.rtt <= a.rtt ? b : a).sample;
            }
            else if (pings.Count == 0)
            {
                // One-way (welcome, countdown), until a ping lands: the server's stamp is older than it looks by the
                // unknown delay, so the largest sample is the one with the least delay in it.
                offsetMs = double.IsNaN(offsetMs) ? sample : Math.Max(offsetMs, sample);
            }
        }

        /// A server timestamp as a Time.unscaledTimeAsDouble value on this phone.
        public double ServerToLocalUnscaled(long serverMs)
        {
            double localMs = serverMs - (double.IsNaN(offsetMs) ? 0 : offsetMs);   // on the stopwatch
            double rel = Math.Min(swMinusUnscaled, clock.ElapsedMilliseconds - Time.unscaledTimeAsDouble * 1000.0);
            return (localMs - rel) / 1000.0;
        }

        // ---- leaving and dying ----
        void OnSocketClosed(string reason)
        {
            if (Connected) return;                                   // an old socket's goodbye, after a reconnect
            if (reason == "replaced") { wantReconnect = false; Error?.Invoke("This room was opened on another device."); return; }
            if (reason == "refused") { wantReconnect = false; return; }
            if (!wantReconnect) return;
            // Dropped mid-room (scene load hiccup, a tunnel): come back as the same player.
            _ = Reconnect();
        }

        /// Keep trying, backing off, while we still belong to a room: one failed attempt (no network yet after a
        /// handover, a busy moment) must not sit a racer out of a paid race.
        async Awaitable Reconnect()
        {
            if (reconnecting) return;
            reconnecting = true;
            try
            {
                float wait = 1.5f;
                for (int attempt = 0; wantReconnect && Room != null && !Connected; attempt++)
                {
                    await Awaitable.WaitForSecondsAsync(wait);
                    if (!wantReconnect || Room == null || Connected) break;
                    if (await Connect()) break;
                    if (attempt == 2) Error?.Invoke("Reconnecting to the room…");
                    wait = Mathf.Min(wait * 2f, 10f);
                }
            }
            finally { reconnecting = false; }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) return;
            Refit();
            if (wantReconnect && Room != null && !Connected) _ = Reconnect();
        }

        void OnApplicationFocus(bool focus) { if (focus) Refit(); }

        /// Back from the background (the wallet app, a locked screen): the phone's clocks may have stopped while the
        /// server's ran on, so the ping window starts afresh and a ping goes out at once. The old offset stands until a
        /// new sample replaces it (none at all would map GO to 1970). A race already under way keeps its GO.
        void Refit()
        {
            pings.Clear(); swMinusUnscaled = double.PositiveInfinity; pingTimer = 0f;
        }

        public void Leave()
        {
            wantReconnect = false;
            var socket = ws; ws = null;
            Room = null; Code = null; MyId = null; LastResults = null;
            _ = SayGoodbye(socket, cts);
            Destroy(gameObject);
        }

        /// The leave frame goes out and the socket closes cleanly before it is disposed: a disposed socket can lose a
        /// frame still queued, and the room would take a leave for a drop.
        static async Task SayGoodbye(ClientWebSocket socket, CancellationTokenSource source)
        {
            if (socket == null) return;
            try
            {
                using var soon = new CancellationTokenSource(1500);
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"t\":\"leave\"}")), WebSocketMessageType.Text, true, soon.Token);
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "left", soon.Token);
                }
            }
            catch { /* gone already */ }
            finally { try { source?.Cancel(); } catch { } try { socket.Dispose(); } catch { } }
        }

        void Close()
        {
            try { cts?.Cancel(); } catch { }
            try { ws?.Dispose(); } catch { }
            ws = null;
        }

        void OnDestroy()
        {
            wantReconnect = false;
            Close();
            if (Instance == this) Instance = null;
        }
    }
}
