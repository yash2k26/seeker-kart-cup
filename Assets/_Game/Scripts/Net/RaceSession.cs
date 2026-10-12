using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace KartCup
{
    /// Records lap and checkpoint times for the server-validated leaderboard, and the drive itself: the player's
    /// position five times a second, which the Worker's referee must believe before a time counts (worker/src/referee.ts).
    /// RaceManager calls BeginRace at GO; KartProgress calls OnCheckpoint/OnLap; RaceManager's finish calls OnFinish(position).
    public sealed class RaceSession : MonoBehaviour
    {
        public static RaceSession Instance { get; private set; }

        [SerializeField] string trackId = "beach";
        [SerializeField] UnityEvent<bool> onResultSubmitted = new();   // isPersonalBest
        public UnityEvent<bool> ResultSubmitted => onResultSubmitted;

        public string RaceId { get; private set; }
        public string RaceToken { get; private set; }
        public string PbMemo { get; private set; }
        public bool Finished { get; private set; }
        /// The Worker's verdict on the last result sent: "accepted", or why not (the harnesses read it).
        public string LastSubmit { get; private set; }

        readonly List<int> lapMs = new(), checkpointMs = new();
        readonly List<float[]> path = new();
        const float SampleEvery = 0.2f;       // 5 Hz: ~11 m apart at top speed, well inside a gate's width
        const int MaxSamples = 3500;           // the Worker refuses more than 4000; a stuck race stops recording here
        float raceStart, lapStart, nextSample;
        bool recording;
        KartProgress player;

        void Awake()
        {
            // The claim path runs from the Menu (WalletFlow.Login -> ClaimAfterLogin), so this cannot die with the Race
            // scene or a guest's finished race can never be bound to a wallet. BeginRace clears per-race state, so reuse is safe.
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async void BeginRace()
        {
            // This object outlives the race scene, so the serialized id is only ever the first scene's; the room or the
            // cup knows which track is actually loaded. Without this every later race scored on the beach board, and
            // an online race's drive was judged against another circuit and refused.
            if (RoomClient.InRaceRoom && RoomClient.Instance.Room is { } room && room.raceIndex >= 0 && room.raceIndex < room.tracks.Count) trackId = room.tracks[room.raceIndex];
            else if (Cup.Active) trackId = Cup.TrackId + (RaceMode.Long ? "-long" : "");   // a long layout is its own track to the Worker
            lapMs.Clear(); checkpointMs.Clear(); path.Clear(); Finished = false; PbMemo = null; LastSubmit = null;
            raceStart = lapStart = Time.time;
            nextSample = 0f; recording = true; player = null;
            KartApi.Event("race_start", new { trackId });
            // A reversed lap, a second lap, a bubble full of nitro or a Smash-Up is not the circuit's lap: it races off the
            // board (the referee would refuse it anyway) and keeps its own personal best (RaceMode.BestKey).
            if (!RoomClient.InRaceRoom && !Weekly.Racing && !RaceMode.Ranked) { RaceToken = null; return; }
            try
            {
                var s = await KartApi.StartRace(trackId, Weekly.Racing ? "weekly" : null);
                RaceId = s.raceId; RaceToken = s.raceToken;
            }
            catch (System.Exception e) { Debug.LogWarning("[race] offline start: " + e.Message); RaceToken = null; }
        }

        void Update()
        {
            if (!recording) return;
            if (player == null) { player = FindObjectsByType<KartProgress>(FindObjectsSortMode.None).FirstOrDefault(k => k.isPlayer && !k.remote); if (player == null) return; }
            float t = Time.time - raceStart;
            if (t >= nextSample) { Sample(t); nextSample = t + SampleEvery; }
        }

        void Sample(float t)
        {
            if (player == null || path.Count >= MaxSamples) return;
            var p = player.transform.position;
            path.Add(new[] { R(t, 2), R(p.x, 1), R(p.y, 1), R(p.z, 1) });
        }

        static float R(float v, int digits) => (float)System.Math.Round(v, digits);

        public void OnCheckpoint() => checkpointMs.Add(Mathf.RoundToInt((Time.time - raceStart) * 1000f));

        public void OnLap()
        {
            lapMs.Add(Mathf.RoundToInt((Time.time - lapStart) * 1000f));
            lapStart = Time.time;
        }

        public async void OnFinish(int position)
        {
            if (Finished) return;
            Finished = true;
            int totalMs = Mathf.RoundToInt((Time.time - raceStart) * 1000f);
            Sample(Time.time - raceStart);                           // the position at the line
            // A little of the drive past the line, on the unscaled clock (the results card may slow time), so the
            // referee sees the finish crossed; then stop recording and send it all.
            float until = Time.unscaledTime + 0.6f;
            while (Time.unscaledTime < until) await Awaitable.NextFrameAsync();
            recording = false;
            KartApi.Event("race_finish", new { trackId, totalMs, position });
            if (RaceToken == null) { LastSubmit = "offline"; onResultSubmitted?.Invoke(false); return; }
            try
            {
                var r = await KartApi.FinishRace(RaceToken, totalMs, lapMs, checkpointMs, position, path);
                PbMemo = r.memo;
                LastSubmit = r.accepted ? "accepted" : "not accepted";
                onResultSubmitted?.Invoke(r.isPersonalBest);
            }
            catch (System.Exception e) { LastSubmit = e.Message; Debug.LogWarning("[race] submit failed: " + e.Message); onResultSubmitted?.Invoke(false); }
        }

        /// After a guest race the player signs in: bind the result to the wallet.
        public async Awaitable<bool> ClaimAfterLogin()
        {
            if (RaceToken == null || !KartApi.HasSession) return false;
            try { var r = await KartApi.ClaimRace(RaceToken); PbMemo = r.memo; return r.isPersonalBest; }
            catch (System.Exception e) { Debug.LogWarning("[race] claim failed: " + e.Message); return false; }
        }
    }
}
