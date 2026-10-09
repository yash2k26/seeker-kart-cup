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
        float outAt = -1f;                       // out of bounds: the words show, then the kart is put back

        void Awake() { kart = GetComponent<Kart>(); }

        public void Pass(Checkpoint cp)
        {
            var race = RaceManager.Instance;
            if (remote || race == null || finished || race.state == RaceManager.State.Countdown) return;
            int count = race.checkpoints.Length;
            // How far ahead of the last gate this one is (1 = the next). A gate behind you, or the same one again, is
            // driving backwards and does nothing. A gate a few ahead means the ones between were missed (a corner cut
            // across the grass past a gate's end, a respawn): they count as passed. Strict order used to drop them, and
            // then refuse every gate after: the lap never completed and the race never ended (10 Oct 2026).
            int ahead = GatesAhead(cp.index, checkpointIndex, count);
            if (ahead == 0 || ahead > Window(count)) return;
            if (ahead > 1 && isPlayer) Debug.Log($"[race] {racerName} skipped {ahead - 1} gate(s) before gate {cp.index}");
            for (int k = 1; k <= ahead && !finished; k++) Advance((checkpointIndex + 1) % count, race);
        }

        /// Gates from `last` forward to `gate` (1 = the next one; 0 = the same gate or one behind it). Before the start
        /// line has been crossed (`last` -1) only the line itself counts.
        public static int GatesAhead(int gate, int last, int count) => last < 0 ? (gate == 0 ? 1 : 0) : ((gate - last) % count + count) % count;
        /// How many gates ahead still counts: a cut corner, not a lap's worth of shortcut.
        public static int Window(int count) => Mathf.Max(3, count / 6);

        /// One gate, in order: an intermediate gate is reported to the session; the start line completes a lap.
        void Advance(int gate, RaceManager race)
        {
            checkpointIndex = gate;
            // The Worker counts the intermediate gates per lap; the start line is reported through OnLap instead.
            if (gate != 0) { if (isPlayer) RaceSession.Instance?.OnCheckpoint(); return; }

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

            // Beach Buggy's out of bounds: in deep water (or off the world) the words come up while the kart goes under,
            // and a moment later it is back on the road. The shallows are drivable: WheelDust throws spray there.
            bool sunk = WaterBody.SurfaceAt(transform.position, out float water) && transform.position.y < water - 0.7f;
            if (outAt < 0f && ((racing && sunk) || lost))
            {
                outAt = Time.time;
                if (isPlayer && racing) RaceHud.Instance?.Callout("OUT OF BOUNDS", 1.3f);
                if (isPlayer && sunk) Achievements.Add(Achievements.Stat.Swims);
                Debug.Log($"[race] {racerName} out of bounds at {transform.position} ({(lost ? "fell" : "in the water")})");
            }
            if (outAt > 0f)
            {
                if (Time.time - outAt < 0.9f && !lost) return;          // under the words for a beat (falling: at once)
                outAt = -1f; crawlingSince = -1f;
                Respawn(race);
                return;
            }

            if (!lost && kart.velMag < 1.5f && !kart.grounded) { if (offTrackSince < 0) offTrackSince = Time.time; }
            else offTrackSince = -1f;

            if (racing && kart.velMag < 2f) { if (crawlingSince < 0) crawlingSince = Time.time; }
            else crawlingSince = -1f;

            // An AI that is not moving is stuck, so free it quickly. A PLAYER may simply have stopped — parking to
            // look around must not teleport you — so they get a much longer rope before the game intervenes.
            float patience = isPlayer ? 10f : 5f;

            if ((offTrackSince > 0 && Time.time - offTrackSince > 3f)
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
            // not on top of a kart put back there a moment ago: step across the road until the spot is clear
            var at = cp.RespawnPosition;
            for (int k = 1; k <= 4 && Crowded(race, at); k++) at = cp.RespawnPosition + cp.transform.right * (k % 2 == 1 ? 3f : -3f) * ((k + 1) / 2);
            kart.rb.position = at;
            kart.rb.rotation = cp.RespawnRotation;
            kart.rb.linearVelocity = Vector3.zero;
            kart.rb.angularVelocity = Vector3.zero;
            if (kart.rotator != null) kart.rotator.rotation = cp.RespawnRotation;
            if (isPlayer) Haptics.Play(Haptics.Kind.Hit);
            // back with a sparkle where it lands, blinking, and untouchable for a moment (Beach Buggy's)
            var sparkle = Resources.Load<GameObject>("FX/BubblePop");
            if (sparkle != null) Instantiate(sparkle, at, Quaternion.identity);
            kart.invulnerableUntil = Time.time + 1.6f;
            StopCoroutine(nameof(Blink)); StartCoroutine(nameof(Blink));
        }

        bool Crowded(RaceManager race, Vector3 at)
        {
            foreach (var r in race.racers) if (r != null && r != this && (r.transform.position - at).sqrMagnitude < 9f) return true;
            return false;
        }

        System.Collections.IEnumerator Blink()
        {
            var shown = GetComponentsInChildren<Renderer>();
            for (float t = 0f; t < 1.4f; t += 0.08f)
            {
                bool on = ((int)(t / 0.08f)) % 2 == 0;
                foreach (var r in shown) if (r != null && r is not ParticleSystemRenderer && r is not TrailRenderer) r.enabled = on;
                yield return new WaitForSeconds(0.08f);
            }
            foreach (var r in shown) if (r != null && r is not ParticleSystemRenderer && r is not TrailRenderer) r.enabled = true;
        }
    }
}
