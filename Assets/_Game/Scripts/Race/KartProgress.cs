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
        public int characterIndex = -1;   // which roster entry this kart is, so the cup can score it
        public bool remote;               // another phone's kart: its lap, gate and score arrive over the network

        public int lap;                       // 0 until the first crossing of the start line
        public int checkpointIndex = -1;      // last gate crossed
        public float score;                   // lap * gates + gate + fraction toward the next gate
        public bool finished;
        public float finishTime;
        public readonly List<float> lapTimes = new();

        Kart kart;
        float lapStart;
        float offTrackSince = -1f, crawlingSince = -1f;

        void Awake() { kart = GetComponent<Kart>(); }

        public void Pass(Checkpoint cp)
        {
            var race = RaceManager.Instance;
            if (remote || race == null || finished || race.state == RaceManager.State.Countdown) return;
            int count = race.checkpoints.Length;
            int expected = (checkpointIndex + 1) % count;
            if (cp.index != expected) return;                      // cutting the course or driving backwards

            checkpointIndex = cp.index;
            // The Worker counts the intermediate gates per lap; the start line is reported through OnLap instead.
            if (cp.index != 0) { if (isPlayer) RaceSession.Instance?.OnCheckpoint(); return; }

            if (lap > 0) lapTimes.Add(race.Now - lapStart);         // crossing the line completes a lap
            if (lap > 0 && isPlayer) RaceSession.Instance?.OnLap();
            lapStart = race.Now;
            lap++;
            if (isPlayer && lap > 1 && lap <= race.totalLaps) AudioDirector.Instance?.Lap(lap == race.totalLaps);
            if (lap > race.totalLaps) race.Finish(this);
        }

        void Update()
        {
            var race = RaceManager.Instance;
            if (race == null || remote) return;
            score = Score(race);

            // Recovery. Two separate cases, because they fail differently:
            //   airborne//fallen  - the old test, still needed for a kart that leaves the world
            //   wedged            - jammed against a rock or another kart. It stays GROUNDED, so the test above
            //                       never fires and the racer is out of the race for good.
            bool lost = transform.position.y < -8f;
            bool racing = race.state == RaceManager.State.Racing && !finished;

            if (!lost && kart.velMag < 1.5f && !kart.grounded) { if (offTrackSince < 0) offTrackSince = Time.time; }
            else offTrackSince = -1f;

            if (racing && kart.velMag < 2f) { if (crawlingSince < 0) crawlingSince = Time.time; }
            else crawlingSince = -1f;

            // An AI that is not moving is stuck, so free it quickly. A PLAYER may simply have stopped — parking to
            // look around must not teleport you — so they get a much longer rope before the game intervenes.
            float patience = isPlayer ? 10f : 5f;

            if (lost || (offTrackSince > 0 && Time.time - offTrackSince > 3f)
                     || (crawlingSince > 0 && Time.time - crawlingSince > patience))
            {
                crawlingSince = -1f;
                Respawn(race);
            }
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
