using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Per-kart race state: which checkpoint it last crossed, which lap it is on, and a single float the
    /// race manager sorts positions by. Also owns falling-off-the-track recovery.
    [RequireComponent(typeof(Kart))]
    public sealed class KartProgress : MonoBehaviour
    {
        public string racerName = "Racer";
        public bool isPlayer;

        public int lap;                       // 0 until the first crossing of the start line
        public int checkpointIndex = -1;      // last gate crossed
        public float score;                   // lap * gates + gate + fraction toward the next gate
        public bool finished;
        public float finishTime;
        public readonly List<float> lapTimes = new();

        Kart kart;
        float lapStart;
        float offTrackSince = -1f;

        void Awake() { kart = GetComponent<Kart>(); }

        public void Pass(Checkpoint cp)
        {
            var race = RaceManager.Instance;
            if (race == null || finished || race.state == RaceManager.State.Countdown) return;
            int count = race.checkpoints.Length;
            int expected = (checkpointIndex + 1) % count;
            if (cp.index != expected) return;                      // cutting the course or driving backwards

            checkpointIndex = cp.index;
            if (cp.index != 0) return;

            if (lap > 0) lapTimes.Add(race.raceTime - lapStart);    // crossing the line completes a lap
            lapStart = race.raceTime;
            lap++;
            if (isPlayer && lap > 1 && lap <= race.totalLaps) AudioDirector.Instance?.Lap(lap == race.totalLaps);
            if (lap > race.totalLaps) race.Finish(this);
        }

        void Update()
        {
            var race = RaceManager.Instance;
            if (race == null) return;
            score = Score(race);

            // Recovery: below the world, or stuck off the track for 3 s -> back on the last gate.
            bool lost = transform.position.y < -8f;
            if (!lost && kart.velMag < 1.5f && !kart.grounded) { if (offTrackSince < 0) offTrackSince = Time.time; }
            else offTrackSince = -1f;
            if (lost || (offTrackSince > 0 && Time.time - offTrackSince > 3f)) Respawn(race);
        }

        float Score(RaceManager race)
        {
            int count = race.checkpoints.Length;
            if (checkpointIndex < 0) return 0f;
            var here = race.checkpoints[checkpointIndex].transform.position;
            var next = race.checkpoints[(checkpointIndex + 1) % count].transform.position;
            float segment = Vector3.Distance(here, next);
            float fraction = segment > 0.01f ? Mathf.Clamp01(1f - Vector3.Distance(transform.position, next) / segment) : 0f;
            return Mathf.Max(0, lap - 1) * count + checkpointIndex + fraction;
        }

        public void Respawn(RaceManager race)
        {
            offTrackSince = -1f;
            var cp = race.checkpoints[Mathf.Max(0, checkpointIndex)];
            kart.rb.position = cp.RespawnPosition;
            kart.rb.rotation = cp.RespawnRotation;
            kart.rb.linearVelocity = Vector3.zero;
            kart.rb.angularVelocity = Vector3.zero;
            if (kart.rotator != null) kart.rotator.rotation = cp.RespawnRotation;
            Haptics.Play(Haptics.Kind.Hit);
        }
    }
}
