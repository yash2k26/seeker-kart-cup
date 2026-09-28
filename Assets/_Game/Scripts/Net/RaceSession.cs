using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace KartCup
{
    /// Records lap and checkpoint times for the server-validated leaderboard. Wire UKR's RaceManager.OnRaceStarted → BeginRace,
    /// KartRaceProgress checkpoint/lap events → OnCheckpoint/OnLap, RaceManager onRaceFinish → OnFinish(position).
    public sealed class RaceSession : MonoBehaviour
    {
        public static RaceSession Instance { get; private set; }

        [SerializeField] string trackId = "beach";
        [SerializeField] UnityEvent<bool> onResultSubmitted = new();   // isPersonalBest

        public string RaceId { get; private set; }
        public string RaceToken { get; private set; }
        public string PbMemo { get; private set; }
        public bool Finished { get; private set; }

        readonly List<int> lapMs = new(), checkpointMs = new();
        float raceStart, lapStart;

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
            // This object outlives the race scene, so the serialized id is only ever the first scene's; the cup knows
            // which track is actually loaded. Without this every later race scored on the beach board.
            if (Cup.Active) trackId = Cup.TrackId;
            lapMs.Clear(); checkpointMs.Clear(); Finished = false; PbMemo = null;
            raceStart = lapStart = Time.time;
            KartApi.Event("race_start", new { trackId });
            try
            {
                var s = await KartApi.StartRace(trackId);
                RaceId = s.raceId; RaceToken = s.raceToken;
            }
            catch (System.Exception e) { Debug.LogWarning("[race] offline start: " + e.Message); RaceToken = null; }
        }

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
            KartApi.Event("race_finish", new { trackId, totalMs, position });
            if (RaceToken == null) { onResultSubmitted?.Invoke(false); return; }
            try
            {
                var r = await KartApi.FinishRace(RaceToken, totalMs, lapMs, checkpointMs, position);
                PbMemo = r.memo;
                onResultSubmitted?.Invoke(r.isPersonalBest);
            }
            catch (System.Exception e) { Debug.LogWarning("[race] submit failed: " + e.Message); onResultSubmitted?.Invoke(false); }
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
