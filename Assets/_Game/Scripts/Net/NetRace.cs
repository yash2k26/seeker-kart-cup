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
        double goAt = -1;
        bool countdownStarted, finishSent;
        float sendTimer;

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

            race.Hold();
            // A late `countdown` (we loaded after it went out) is recovered from the snapshot the room keeps sending.
            if (client.Room?.startAt is long startAt && client.Room.state == "countdown") OnCountdown(client.ServerToLocalUnscaled(startAt));
            client.Send(new { t = "loaded" });
        }

        void OnDestroy()
        {
            RoomClient.CountdownAt -= OnCountdown;
            RoomClient.StateReceived -= OnState;
            RoomClient.RemoteFinished -= OnRemoteFinished;
            RoomClient.ResultsReceived -= OnResults;
            RoomClient.Closed -= OnClosed;
            HitStop.Online = false;
            if (Instance == this) Instance = null;
        }

        void OnCountdown(double localGo) { goAt = localGo; }

        void Update()
        {
            if (race == null || client == null) return;
            if (goAt > 0 && !countdownStarted && Time.unscaledTimeAsDouble >= goAt - race.countdownSeconds)
            {
                countdownStarted = true;
                race.BeginCountdownAt(goAt);
            }
            if (!countdownStarted) return;

            sendTimer -= Time.unscaledDeltaTime;
            if (sendTimer <= 0f && myKart != null && me != null && !me.finished)
            {
                sendTimer = 1f / SendHz;
                var rb = myKart.rb;
                var rot = myKart.rotator != null ? myKart.rotator.rotation : myKart.transform.rotation;
                var v = rb.linearVelocity;
                client.Send(new
                {
                    t = "s",
                    p = new[] { R(rb.position.x), R(rb.position.y), R(rb.position.z) },
                    r = new[] { R(rot.x, 4), R(rot.y, 4), R(rot.z, 4), R(rot.w, 4) },
                    v = new[] { R(v.x), R(v.y), R(v.z) },
                    st = R(myKart.GetVisualSteer(), 2),
                    lap = me.lap, cp = me.checkpointIndex, sc = R(me.score, 3),
                    tm = R(RaceSeconds, 3),
                });
            }

            if (me != null && me.finished && !finishSent)
            {
                finishSent = true;
                client.Send(new { t = "fin", ms = Mathf.RoundToInt(me.finishTime * 1000f), laps = me.lapTimes.Select(l => Mathf.RoundToInt(l * 1000f)).ToArray() });
            }
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
