using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// A racer, not a waypoint chaser. Powerslide's follower drives the centreline flat out and ignores everyone
    /// else, which is why the field used to run in single file and bump through each other. This one:
    ///   - carries its own racing line, so six karts do not stack on one string of points;
    ///   - reads the corner ahead and slows for it, then drifts through;
    ///   - sees the karts around it, picks a side to pass on, and lifts rather than ramming;
    ///   - drives to its character's stats, with a personality seeded from the character so the same rival
    ///     behaves the same way every time you meet them;
    ///   - makes occasional small mistakes, because a field that never errs is not beatable, it is a wall.
    public sealed class KartAi : KartInput
    {
        [Header("Route")]
        public BasicWaypoint targetPoint;
        BasicWaypoint nextPoint;

        [Header("Personality (seeded from the character)")]
        [Range(0.75f, 1.05f)] public float skill = 0.95f;     // share of its own top speed it will commit to
        [Range(0f, 1f)] public float aggression = 0.5f;       // brakes later, fights harder for a line
        [Range(-1f, 1f)] public float lineBias;               // preferred side of the road
        public float catchUpPerMetre = 0.004f, maxCatchUp = 0.12f;

        [Header("Feel")]
        public float steerAmount = 9f;
        public float lateralRange = 5.2f;                     // how far off the centreline a line may sit
        public float lookAhead = 0.05f;

        Transform tr;
        Rigidbody rb;
        Kart kart;
        KartProgress progress;
        float baseSpeed, nextItem, mistakeUntil, mistakeSteer, nextMistake;
        float smoothedLateral, trafficLift = 1f;
        bool reversing; float reverseTime, reverseSteer;

        public override void Awake()
        {
            base.Awake();
            tr = transform;
            rb = GetComponent<Rigidbody>();
            kart = GetComponent<Kart>();
            progress = GetComponent<KartProgress>();
            baseSpeed = kart != null ? kart.maxSpeed : 30f;
            if (targetPoint != null) nextPoint = targetPoint.nextPoint;
        }

        /// Called by the spawner. The seed is the character index, so a rival's habits are stable across races.
        public void Configure(CharacterDef def, BasicWaypoint start, int seed)
        {
            targetPoint = start;
            nextPoint = start != null ? start.nextPoint : null;
            var rnd = new System.Random(seed * 7919 + 13);
            float r(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // Stats shape the driver: grip buys corner speed, weight buys the confidence to lean on people.
            skill = Mathf.Clamp(r(0.88f, 1.0f) + (def != null ? def.speed * 0.012f : 0f), 0.8f, 1.03f);
            aggression = Mathf.Clamp01(r(0.25f, 0.85f) + (def != null ? def.weight * 0.05f : 0f));
            lineBias = Mathf.Clamp(r(-0.75f, 0.75f) + (def != null ? def.handling * 0.05f : 0f), -0.85f, 0.85f);
            nextItem = r(2f, 6f);
            nextMistake = r(6f, 16f);
            Awake();
        }

        /// Kept for callers that only have a waypoint.
        public void SetTarget(BasicWaypoint waypoint) { targetPoint = waypoint; Awake(); }

        public override void Update()
        {
            if (targetPoint == null || nextPoint == null || kart == null || kart.rotator == null) return;
            float dt = Time.deltaTime;
            var race = RaceManager.Instance;

            // ---- advance along the chain ----
            if ((targetPoint.transform.position - tr.position).sqrMagnitude <= targetPoint.radius * targetPoint.radius)
            {
                targetPoint = targetPoint.nextPoint;
                if (targetPoint != null) nextPoint = targetPoint.nextPoint;
                if (targetPoint == null || nextPoint == null) return;
            }

            // ---- where the corner goes ----
            Vector3 here = targetPoint.transform.position, ahead = nextPoint.transform.position;
            Vector3 legIn = (here - tr.position).normalized, legOut = (ahead - here).normalized;
            // Measured on the ground plane: the tracks have hills now, and a crest between two waypoints read as a
            // corner, so the whole field lifted on every rise.
            Vector3 flatIn = new(legIn.x, 0f, legIn.z), flatOut = new(legOut.x, 0f, legOut.z);
            float turn = Vector3.SignedAngle(flatIn, flatOut, Vector3.up);      // + is a right-hander
            float severity = Mathf.InverseLerp(8f, 55f, Mathf.Abs(turn));

            // ---- pick a line: personal bias, pulled toward the apex through the corner ----
            float apex = Mathf.Sign(turn) * severity * 0.85f;
            float wantLateral = Mathf.Clamp(lineBias * (1f - severity * 0.6f) + apex, -0.9f, 0.9f);
            wantLateral += Traffic(race);                                        // move off a line someone else owns
            smoothedLateral = Mathf.Lerp(smoothedLateral, Mathf.Clamp(wantLateral, -1f, 1f), 1f - Mathf.Exp(-3f * dt));

            Vector3 roadRight = Vector3.Cross(Vector3.up, legOut).normalized;
            Vector3 aim = Vector3.Lerp(here, ahead, 1f - Mathf.Clamp01(Vector3.Distance(tr.position, here) * lookAhead))
                          + roadRight * (smoothedLateral * lateralRange);

            Vector3 toAim = aim - tr.position;
            float forwardDot = Vector3.Dot(toAim.normalized, kart.rotator.forward);
            float rightDot = Vector3.Dot(toAim.normalized, kart.rotator.right);

            // ---- stuck recovery, straight from the kit: it is the only thing that unsticks a beached kart ----
            if (!reversing && kart.velMag < 5f && race != null && race.state == RaceManager.State.Racing)
            {
                reverseTime += dt;
                if (reverseTime > 3f) { reversing = true; reverseTime = 0f; reverseSteer = -Mathf.Sign(rightDot); }
            }
            else if (!reversing) reverseTime = 0f;

            if (reversing)
            {
                reverseTime += dt;
                if (reverseTime > 1f) { reversing = false; reverseTime = 0f; }
                targetAccel = 0f; targetBrake = 1f; targetSteer = reverseSteer; drifting = false;
                base.Update();
                return;
            }

            // ---- rubber band, then corner speed ----
            float band = 0f;
            if (race != null && race.Player != null && progress != null)
                band = Mathf.Min(Mathf.Max(0f, race.Player.score - progress.score) * catchUpPerMetre, maxCatchUp);
            kart.maxSpeed = baseSpeed * (skill + band);

            // A corner is a speed limit. Aggression buys a later lift, never a free pass.
            float corner = Mathf.Lerp(1f, 0.52f, severity) + aggression * 0.12f;
            float wantSpeed = kart.maxSpeed * Mathf.Clamp01(corner);
            float over = kart.velMag - wantSpeed;
            targetAccel = (over > 0f ? Mathf.Clamp01(1f - over / 6f) : 1f) * trafficLift;
            targetBrake = Mathf.Clamp01(over / 9f) * 0.55f;
            targetSteer = Mathf.Clamp(rightDot * steerAmount, -1f, 1f);
            if (forwardDot <= 0f) targetSteer = Mathf.Sign(rightDot);            // pointing away: turn around

            // ---- an occasional human error, so the field is beatable ----
            if (race != null && race.state == RaceManager.State.Racing)
            {
                nextMistake -= dt;
                if (nextMistake <= 0f)
                {
                    nextMistake = Mathf.Lerp(16f, 40f, skill) * Random.Range(0.6f, 1.5f);
                    mistakeUntil = Time.time + Random.Range(0.25f, 0.6f);
                    mistakeSteer = Random.Range(-0.35f, 0.35f);
                }
                if (Time.time < mistakeUntil) { targetAccel *= 0.45f; targetSteer = Mathf.Clamp(targetSteer + mistakeSteer, -1f, 1f); }
            }

            // ---- drift the corner rather than scrub it ----
            float driftWant = Mathf.Abs(rightDot + Mathf.Sign(turn) * severity) * Mathf.Min(kart.velMag * 0.02f, 2f);
            if (!drifting && driftWant > 1f) drifting = true;
            else if (drifting && driftWant < 0.25f) drifting = false;

            Items(race, dt);
            base.Update();
        }

        /// Nudge off the racing line when another kart already owns it, and lift rather than rear-end them.
        float Traffic(RaceManager race)
        {
            if (race == null) return 0f;
            float nudge = 0f;
            trafficLift = 1f;
            var fwd = kart.rotator.forward;
            foreach (var other in race.racers)
            {
                if (other == null || other.transform == tr) continue;
                Vector3 delta = other.transform.position - tr.position;
                float dist = delta.magnitude;
                if (dist > 14f || dist < 0.01f) continue;
                if (Vector3.Dot(delta / dist, fwd) < 0.55f) continue;            // only what is genuinely in front

                float side = Vector3.Dot(delta / dist, kart.rotator.right);
                nudge += -Mathf.Sign(side == 0f ? Random.value - 0.5f : side) * (1f - dist / 14f) * (0.5f + aggression * 0.5f);
                if (dist < 6f) trafficLift = Mathf.Min(trafficLift, 0.65f);      // back off instead of ramming
            }
            return Mathf.Clamp(nudge, -0.8f, 0.8f);
        }

        /// Fire when it will land on someone, not on a metronome.
        void Items(RaceManager race, float dt)
        {
            if (race == null || race.state != RaceManager.State.Racing || caster == null || caster.item == null) return;
            nextItem -= dt;
            if (nextItem > 0f) return;

            bool someoneAhead = false;
            var fwd = kart.rotator.forward;
            foreach (var other in race.racers)
            {
                if (other == null || other.transform == tr) continue;
                Vector3 delta = other.transform.position - tr.position;
                float dist = delta.magnitude;
                if (dist < 45f && Vector3.Dot(delta.normalized, fwd) > 0.75f) { someoneAhead = true; break; }
            }
            if (!someoneAhead) return;
            nextItem = Random.Range(3.5f, 7f);
            PressItem();
        }
    }
}
