using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public string Code { get; private set; }
        public string MyId { get; private set; }
        public RoomState Room { get; private set; }
        public ResultsMsg LastResults { get; private set; }
        public bool Connected => ws != null && ws.State == WebSocketState.Open;
        public bool IsHost => Room != null && MyId != null && Room.hostId == MyId;
        public PlayerState Me => Room?.players?.Find(p => p.id == MyId);
        public int PingMs { get; private set; } = -1;

        ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new();
        readonly SemaphoreSlim sendLock = new(1, 1);
        readonly Stopwatch clock = Stopwatch.StartNew();
        double offsetMs = double.NaN;                 // serverMs - localMs
        float pingTimer;
        bool wantReconnect;
        int helloCharacter; string helloKart;

        // ---- DTOs ----
        [Serializable] public class PlayerState { public string id, name, wallet, kart; public int character, points; public bool host, connected, ready, paid, paying, loaded, racing, dnf; public long? finishMs; public int? place; }
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
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    if (!r.EndOfMessage) continue;
                    inbox.Enqueue(sb.ToString()); sb.Clear();
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException)) { UnityEngine.Debug.LogWarning("[room] socket: " + e.Message); }
            inbox.Enqueue("\u0000closed");
        }

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
            while (inbox.TryDequeue(out var text))
            {
                if (text == "\u0000closed") { OnSocketClosed(); continue; }
                try { Dispatch(JObject.Parse(text)); }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[room] bad message: " + e.Message); }
            }
            if (Connected)
            {
                pingTimer -= Time.unscaledDeltaTime;
                if (pingTimer <= 0f) { pingTimer = 2f; Send(new { t = "ping", c = clock.ElapsedMilliseconds }); }
            }
        }

        void Dispatch(JObject m)
        {
            switch ((string)m["t"])
            {
                case "welcome":
                    MyId = (string)m["id"];
                    NoteServerTime((long)m["serverNow"], 0);
                    SetRoom(m["room"]);
                    welcomed = true;
                    break;
                case "room": SetRoom(m["room"]); break;
                case "pong":
                {
                    long c = (long)m["c"], s = (long)m["s"], now = clock.ElapsedMilliseconds;
                    PingMs = (int)(now - c);
                    NoteServerTime(s, (now - c) / 2);
                    break;
                }
                case "load": LastResults = null; LoadRequested?.Invoke((string)m["track"], (int)m["race"], (int)m["races"]); break;
                case "countdown":
                    NoteServerTime((long)m["serverNow"], PingMs > 0 ? PingMs / 2 : 0);
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
                    if (welcomed == null) welcomed = false;
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
            RoomChanged?.Invoke(Room);
        }

        // ---- clocks: the server's ms epoch mapped onto this phone's stopwatch ----
        void NoteServerTime(long serverMs, long halfRtt)
        {
            double sample = serverMs - (clock.ElapsedMilliseconds - halfRtt);
            // Keep the smallest-latency estimate biased: a late sample can only make the offset look larger.
            offsetMs = double.IsNaN(offsetMs) ? sample : Math.Min(offsetMs, sample) * 0.7 + sample * 0.3;
        }

        /// A server timestamp as a Time.unscaledTimeAsDouble value on this phone.
        public double ServerToLocalUnscaled(long serverMs)
        {
            double localMs = serverMs - (double.IsNaN(offsetMs) ? 0 : offsetMs);
            return Time.unscaledTimeAsDouble + (localMs - clock.ElapsedMilliseconds) / 1000.0;
        }

        // ---- leaving and dying ----
        void OnSocketClosed()
        {
            if (!wantReconnect) return;
            // Dropped mid-room (scene load hiccup, a tunnel): come back as the same player.
            _ = Reconnect();
        }

        async Awaitable Reconnect()
        {
            await Awaitable.WaitForSecondsAsync(1.5f);
            if (!wantReconnect || Room == null) return;
            if (!await Connect()) Error?.Invoke("Lost the room connection.");
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && wantReconnect && Room != null && !Connected) _ = Reconnect();
        }

        public void Leave()
        {
            wantReconnect = false;
            Send(new { t = "leave" });
            Close();
            Room = null; Code = null; MyId = null; LastResults = null;
            Destroy(gameObject);
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
