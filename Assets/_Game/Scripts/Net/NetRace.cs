using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// The race scene's tie to the room: tells the room we loaded, starts the countdown on the room's clock, streams
    /// our kart 15 times a second, feeds the others into their proxies, reports our finish, and hands the room's
    /// verdict to the results card. Created by RaceSpawner only when a room is live.
    public sealed class NetRace : MonoBehaviour
    {
        public static NetRace Instance { get; private set; }
        /// Seconds since GO on this phone's unscaled clock; every phone's GO is the same server instant.
        public static float RaceSeconds => Instance != null && Instance.goAt > 0 ? (float)(Time.unscaledTimeAsDouble - Instance.goAt) : 0f;

        const float SendHz = 15f;

        RoomClient client;
        RaceManager race;
        Kart myKart;
        KartProgress me;
        readonly Dictionary<string, RemoteKart> remotes = new();
        double goAt = -1, finishedAt = -1;
        PodiumShow intro;
        bool countdownStarted, finishSent;
        float sendTimer;
        int boundRound = -1, boundRace = -1;                        // the room race this scene is
        // The last few seconds of states: sent again after a reconnect, so the room's referee still sees the stretch
        // (the finish line, often) that went by while the socket was down. The room ignores any it already has.
        readonly Queue<(float tm, object msg)> recent = new();
        const float KeepSeconds = 30f;             // a reconnect within this leaves the referee no gap at all

        public void Bind(RoomClient c, RaceManager r, Kart mine, KartProgress myProgress, IEnumerable<RemoteKart> others)
        {
            Instance = this;
            client = c; race = r; myKart = mine; me = myProgress;
            foreach (var o in others) remotes[o.Id] = o;

            // Skill only online: no item boxes, and no time-scale tricks that would let one phone's clock drift from the room's.
            foreach (var giver in FindObjectsByType<ItemGiver>(FindObjectsSortMode.None)) giver.gameObject.SetActive(false);
            HitStop.Online = true;

            RoomClient.CountdownAt += OnCountdown;
            RoomClient.StateReceived += OnState;
            RoomClient.RemoteFinished += OnRemoteFinished;
            RoomClient.ResultsReceived += OnResults;
            RoomClient.Closed += OnClosed;
            RoomClient.Welcomed += OnWelcomed;

            race.Hold();
            boundRound = client.Room?.round ?? -1; boundRace = client.Room?.raceIndex ?? -1;
            // What this race is for, while everyone's phone loads the track and until the 3-2-1: the prize pool,
            // whatever it is, and what each place takes home.
            // the same turbo pads on every phone: the draw is seeded by the room and which of its races this is
            TurboPad.Spawn(Animator.StringToHash((client.Room?.code ?? "") + ":" + boundRound + ":" + boundRace));
            intro = PodiumShow.Ensure();
            ShowPrizes();
            // A late `countdown` (we loaded after it went out, or it went out while the socket was down) is recovered
            // from the snapshot the room keeps sending.
            RecoverCountdown();
            client.Send(new { t = "loaded" });
        }

        void OnDestroy()
        {
            RoomClient.CountdownAt -= OnCountdown;
            RoomClient.StateReceived -= OnState;
            RoomClient.RemoteFinished -= OnRemoteFinished;
            RoomClient.ResultsReceived -= OnResults;
            RoomClient.Closed -= OnClosed;
            RoomClient.Welcomed -= OnWelcomed;
            HitStop.Online = false;
            if (Instance == this) Instance = null;
        }

        void OnCountdown(double localGo) { goAt = localGo; }

        void ShowPrizes()
        {
            var room = client.Room;
            if (intro == null || room == null) return;
            // Fair shares: each place takes its share of every stake it matched, so what a place pays depends on who
            // takes it. The cards show the shares (winner takes all with two stakes); the pot is the money on the race.
            var shares = FairShares.Places(room.players.Count(p => p.paid));
            var places = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var label = RaceHud.Ordinal(i + 1).ToUpperInvariant();
                places.Add(!room.IsPaid ? label : i >= shares.Length ? label + "\n—" : label + "\n" + (shares.Length == 1 ? "ALL" : shares[i] + "%"));
            }
            intro.Prizes(room.IsPaid ? "PRIZE POOL" : "RACE FOR GOLD", room.IsPaid ? WalletFlow.Amount(room.pool, room.currency, true) : "BRAGGING RIGHTS", places);
        }

        void RecoverCountdown()
        {
            var room = client.Room;
            if (goAt < 0 && room?.startAt is long startAt && (room.state == "countdown" || room.state == "racing")) OnCountdown(client.ServerToLocalUnscaled(startAt));
        }

        void Update()
        {
            if (race == null || client == null) return;
            if (goAt > 0 && !countdownStarted && Time.unscaledTimeAsDouble >= goAt - race.countdownSeconds)
            {
                countdownStarted = true;
                if (intro != null && intro.Active) intro.End();   // the prize shot gives way to the grid for 3-2-1
                race.BeginCountdownAt(goAt);
            }
            if (!countdownStarted) return;

            // The room's referee follows our kart from these states, so it must see the line crossed: send the position
            // the moment we finish, keep streaming for a second, and report the finish half a second after the line.
            if (me != null && me.finished && finishedAt < 0) { finishedAt = Time.unscaledTimeAsDouble; SendState(); sendTimer = 1f / SendHz; }
            bool streaming = me != null && (!me.finished || Time.unscaledTimeAsDouble < finishedAt + 1.0);
            sendTimer -= Time.unscaledDeltaTime;
            if (sendTimer <= 0f && myKart != null && streaming) { sendTimer = 1f / SendHz; SendState(); }

            if (me != null && me.finished && !finishSent && Time.unscaledTimeAsDouble >= finishedAt + 0.5)
            {
                finishSent = true;
                SendFin();
            }
        }

        void SendFin() => client.Send(new { t = "fin", ms = Mathf.RoundToInt(me.finishTime * 1000f), laps = me.lapTimes.Select(l => Mathf.RoundToInt(l * 1000f)).ToArray() });

        /// Back after a reconnect: what the room may have missed while the socket was down goes again. (Send drops
        /// frames on a closed socket, so a `loaded`, the `countdown`, states or a `fin` in the gap never arrived.) Only for
        /// the room race this scene is: a results card still up from the last race must not say `loaded` for the next.
        void OnWelcomed()
        {
            var room = client.Room;
            if (room == null || room.round != boundRound || room.raceIndex != boundRace) return;
            if (room.state == "loading") client.Send(new { t = "loaded" });
            RecoverCountdown();
            if (room.state != "racing" && room.state != "countdown") return;
            // Everything missed, oldest first: every third state beyond the last 3 s (5 Hz is plenty for the referee),
            // every state of the last 3 s.
            float newest = recent.Count > 0 ? recent.Last().tm : 0f; int i = 0;
            foreach (var (tm, msg) in recent) if (i++ % 3 == 0 || tm > newest - 3f) client.Send(msg);
            var mine = client.Me;
            if (finishSent && mine != null && mine.finishMs == null && !mine.dnf) SendFin();
        }

        void SendState()
        {
            if (myKart == null || me == null) return;
            var rb = myKart.rb;
            var rot = myKart.rotator != null ? myKart.rotator.rotation : myKart.transform.rotation;
            var v = rb.linearVelocity;
            float tm = RaceSeconds;
            var msg = new
            {
                t = "s",
                p = new[] { R(rb.position.x), R(rb.position.y), R(rb.position.z) },
                r = new[] { R(rot.x, 4), R(rot.y, 4), R(rot.z, 4), R(rot.w, 4) },
                v = new[] { R(v.x), R(v.y), R(v.z) },
                st = R(myKart.GetVisualSteer(), 2),
                lap = me.lap, cp = me.checkpointIndex, sc = R(me.score, 3),
                tm = R(tm, 3),
            };
            // Only on a socket the room has welcomed: a state it cannot place is dropped, and one taken ahead of the
            // re-send after a reconnect would hide the states the re-send carries.
            if (client.Ready) client.Send(msg);
            recent.Enqueue((tm, msg));
            while (recent.Count > 0 && recent.Peek().tm < tm - KeepSeconds) recent.Dequeue();
        }

        static float R(float v, int digits = 2) => (float)System.Math.Round(v, digits);

        void OnState(JObject m)
        {
            var id = (string)m["id"];
            if (id == null || !remotes.TryGetValue(id, out var remote)) return;
            var p = (JArray)m["p"]; var r = (JArray)m["r"]; var v = (JArray)m["v"];
            if (p == null || r == null || p.Count < 3 || r.Count < 4) return;
            remote.Push(
                new Vector3((float)p[0], (float)p[1], (float)p[2]),
                new Quaternion((float)r[0], (float)r[1], (float)r[2], (float)r[3]),
                v != null && v.Count >= 3 ? new Vector3((float)v[0], (float)v[1], (float)v[2]) : Vector3.zero,
                (float)(m["st"] ?? 0f), (float)(m["tm"] ?? 0f),
                (int)(m["lap"] ?? 0), (int)(m["cp"] ?? -1), (float)(m["sc"] ?? 0f));
        }

        void OnRemoteFinished(string id, long ms, bool dnf)
        {
            if (!remotes.TryGetValue(id, out var remote)) return;
            var progress = remote.GetComponent<KartProgress>();
            if (progress == null || progress.finished) return;
            progress.finished = true;
            progress.finishTime = dnf ? float.MaxValue : ms / 1000f;
        }

        void OnResults(RoomClient.ResultsMsg r)
        {
            // Timed out or disconnected mid-race: the room has decided, so bring the card up even though we never crossed the line.
            if (me != null && !me.finished && race != null) race.Finish(me);
        }

        void OnClosed(string reason)
        {
            if (me != null && !me.finished && race != null) race.Finish(me);
        }
    }
}
