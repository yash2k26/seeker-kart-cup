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
        [Range(0.75f, 1.08f)] public float skill = 0.98f;     // share of its own top speed it will commit to
        [Range(0f, 1f)] public float aggression = 0.5f;       // brakes later, fights harder for a line
        [Range(-1f, 1f)] public float lineBias;               // preferred side of the road
        // Behind the player it digs in: +0.6% top speed per metre down, up to +16% (6 Oct: the field was too easy to
        // leave behind). Ahead of the player it never eases off: a lead has to be earned.
        public float catchUpPerMetre = 0.006f, maxCatchUp = 0.16f;
        /// Set by the spawner from the event's pace: how much less this driver lifts in a corner, and whether it ever errs
        /// (a Tour boss does not). Never on the autopilot, so the playtest's stand-in driver stays the same driver.
        public float cornerBonus;
        public bool steady;

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

            // Stats shape the driver: grip buys corner speed, weight buys the confidence to lean on people. The field
            // commits to (nearly) all of its pace: 0.95-1.03 before stats (was 0.88-1.0, an easy field to drop).
            skill = Mathf.Clamp(r(0.95f, 1.03f) + (def != null ? def.speed * 0.012f : 0f), 0.9f, 1.06f);
            aggression = Mathf.Clamp01(r(0.4f, 0.95f) + (def != null ? def.weight * 0.05f : 0f));
            lineBias = Mathf.Clamp(r(-0.75f, 0.75f) + (def != null ? def.handling * 0.05f : 0f), -0.85f, 0.85f);
            nextItem = r(1.5f, 4f);
            nextMistake = r(14f, 30f);
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
            // Reached when within its radius, or once past it: across the line through it square to the road, close by. On
            // the radius alone a kart knocked off its line could never get onto a missed point again and circled it (the
            // reversed Speedway's hairpin, 17 s, 11 Oct 2026).
            Vector3 toTarget = targetPoint.transform.position - tr.position;
            Vector3 roadHere = nextPoint.transform.position - targetPoint.transform.position;
            bool passed = toTarget.sqrMagnitude < 40f * 40f && Vector3.Dot(-toTarget, roadHere) > 0f;
            if (toTarget.sqrMagnitude <= targetPoint.radius * targetPoint.radius || passed)
            {
                var reached = targetPoint;
                targetPoint = targetPoint.nextPoint;
                // the circuit's shortcut: a good field's bolder drivers cut the corner (ShortcutPath)
                var cut = ShortcutPath.Instance;
                if (cut != null && reached == cut.from && ShortcutPath.Takes(aggression, steady)) { targetPoint = cut.path[0]; Debug.Log($"[race] {name} takes the shortcut"); }
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
            // Braking reads one bend further at speed: the waypoints are ~28 m apart, and at a boss's pace the lift for the
            // bend after next has to start before this one. Without it a fast AI arrived too hot, ran wide and lost to a
            // slower one on Pine Ridge (10 Oct 2026). Only the speed reads it; the line still aims at this bend's apex.
            float brake = severity;
            var after = nextPoint.nextPoint;
            if (after != null)
            {
                Vector3 flatNext = after.transform.position - ahead; flatNext.y = 0f;
                float next = Mathf.InverseLerp(8f, 55f, Vector3.Angle(flatOut, flatNext));
                brake = Mathf.Max(brake, next * Mathf.InverseLerp(25f, 50f, kart.velMag) * 0.85f);
            }

            // ---- pick a line: personal bias, pulled toward the apex through the corner ----
            float apex = Mathf.Sign(turn) * severity * 0.85f;
            float wantLateral = Mathf.Clamp(lineBias * (1f - severity * 0.6f) + apex, -0.9f, 0.9f);
            wantLateral += Traffic(race);                                        // move off a line someone else owns
            smoothedLateral = Mathf.Lerp(smoothedLateral, Mathf.Clamp(wantLateral, -1f, 1f), 1f - Mathf.Exp(-3f * dt));

            Vector3 roadRight = Vector3.Cross(Vector3.up, legOut).normalized;
            // on the shortcut's dirt (9 m wide) the line stays near its middle
            float lateral = ShortcutPath.Instance != null && ShortcutPath.Instance.Holds(targetPoint) ? lateralRange * 0.45f : lateralRange;
            Vector3 aim = Vector3.Lerp(here, ahead, 1f - Mathf.Clamp01(Vector3.Distance(tr.position, here) * lookAhead))
                          + roadRight * (smoothedLateral * lateral);

            aim = Detour(aim);                                                   // oil in the path: aim beside it until past
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

            // A corner is a speed limit. Aggression buys a later lift, never a free pass. (The floor was 0.52 of top
            // speed in the tightest corner: the field crawled through them; they drift these, so 0.6 holds the road.)
            float corner = Mathf.Lerp(1f, 0.6f + cornerBonus, brake) + aggression * 0.15f;
            float wantSpeed = kart.maxSpeed * Mathf.Clamp01(corner);
            float over = kart.velMag - wantSpeed;
            targetAccel = (over > 0f ? Mathf.Clamp01(1f - over / 6f) : 1f) * trafficLift;
            targetBrake = Mathf.Clamp01(over / 9f) * 0.45f;
            targetSteer = Mathf.Clamp(rightDot * steerAmount, -1f, 1f);
            if (forwardDot <= 0f) targetSteer = Mathf.Sign(rightDot);            // pointing away: turn around

            // ---- an occasional human error, so the field is beatable ----
            if (race != null && race.state == RaceManager.State.Racing && !steady)
            {
                nextMistake -= dt;
                if (nextMistake <= 0f)
                {
                    // rarer and smaller than they were (every 16-40 s for up to 0.6 s): still beatable, not a gift
                    nextMistake = Mathf.Lerp(30f, 70f, Mathf.InverseLerp(0.9f, 1.06f, skill)) * Random.Range(0.7f, 1.4f);
                    mistakeUntil = Time.time + Random.Range(0.2f, 0.45f);
                    mistakeSteer = Random.Range(-0.25f, 0.25f);
                }
                if (Time.time < mistakeUntil) { targetAccel *= 0.6f; targetSteer = Mathf.Clamp(targetSteer + mistakeSteer, -1f, 1f); }
            }

            // ---- drift the corner rather than scrub it ----
            float driftWant = Mathf.Abs(rightDot + Mathf.Sign(turn) * severity) * Mathf.Min(kart.velMag * 0.02f, 2f);
            if (!drifting && driftWant > 1f) drifting = true;
            else if (drifting && driftWant < 0.25f) drifting = false;

            Items(race, dt);
            if (race != null && race.state == RaceManager.State.Racing && Time.time > nextAbility)
            {
                nextAbility = Time.time + Random.Range(0.8f, 2.2f);
                if (ability == null) ability = GetComponent<Ability>();
                ability?.ThinkFor(this, 1f - severity);
            }
            base.Update();
        }

        Ability ability; float nextAbility;

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
                nudge += -Mathf.Sign(side == 0f ? Random.value - 0.5f : side) * (1f - dist / 14f) * (0.7f + aggression * 0.6f);
                if (dist < 4.5f) trafficLift = Mathf.Min(trafficLift, 0.85f);     // only off the throttle when about to touch: go round, not behind
            }
            return Mathf.Clamp(nudge, -0.9f, 0.9f);
        }

        /// Oil in the kart's own path (along where it is travelling, measured from the kart, not the road): aim at a point
        /// beside the nearest slick, on the side the kart already leans to, until it is past; then the line again. Steering
        /// nudges lost the tug of war with the pull to an aim point beyond the slick; a detour point at the slick's own
        /// distance cannot. Seen from 25-40 m: the better the driver, the sooner.
        Vector3 Detour(Vector3 aim)
        {
            float see = 25f + 15f * Mathf.InverseLerp(0.9f, 1.06f, skill);
            const float Clear = 4.2f;                                           // slick radius 1.8 + half a kart + room
            var heading = rb != null && rb.linearVelocity.sqrMagnitude > 25f ? rb.linearVelocity : kart.rotator.forward;
            var fwd = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, fwd);
            OilSlick nearest = null; float best = see, nearestLat = 0f;
            foreach (var slick in OilSlick.All)
            {
                if (slick == null) continue;
                Vector3 d = slick.transform.position - tr.position; d.y = 0f;
                float ahead = Vector3.Dot(d, fwd), lateral = Vector3.Dot(d, right);
                if (ahead < 2f || ahead > best || Mathf.Abs(lateral) > Clear) continue;
                nearest = slick; best = ahead; nearestLat = lateral;
            }
            if (nearest == null) return aim;
            float side = nearestLat > 0f ? -1f : 1f;                            // slick to the right: pass it on the left
            return nearest.transform.position + right * side * Clear + fwd * 2f;
        }

        /// Use each item the way it works, when it will land: a rocket at someone ahead, a homing one at anyone up the
        /// road, a bomb or a trap dropped on whoever is on its tail, nitro on a straight. Held too long, it goes anyway.
        void Items(RaceManager race, float dt)
        {
            if (race == null || race.state != RaceManager.State.Racing || caster == null || caster.item == null || caster.ammo <= 0) return;
            nextItem -= dt;
            if (nextItem > 0f) return;

            string held = caster.item.itemName;
            var fwd = kart.rotator.forward;
            bool ahead = false, behind = false;
            float reach = held == "Homing Item" ? 85f : 60f;
            foreach (var other in race.racers)
            {
                if (other == null || other.transform == tr) continue;
                Vector3 delta = other.transform.position - tr.position;
                float dist = delta.magnitude, dot = Vector3.Dot(delta.normalized, fwd);
                if (dist < reach && dot > 0.6f) ahead = true;
                if (dist < 25f && dot < -0.5f) behind = true;
            }
            heldFor += 1f;                                                       // seconds held, near enough (checked about once a second)
            // RaceMode.Fire, the event's own dial apart from its pace: a keen field takes every chance and lets go sooner;
            // a rookie one lets chances pass and sits on what it holds.
            float fire = Mathf.Max(0.2f, RaceMode.Fire);
            bool use = held switch
            {
                "Nitro" => Mathf.Abs(Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.ProjectOnPlane(nextPoint.transform.position - tr.position, Vector3.up), Vector3.up)) < 12f
                           && kart.velMag > kart.maxSpeed * 0.55f,
                "Bomb" or "Trap" => behind,
                _ => ahead,
            };
            if (use && fire < 1f && Random.value > fire) use = false;
            if (!use && heldFor < 12f / fire) { nextItem = 1f; return; }
            heldFor = 0f;
            nextItem = Random.Range(2.5f, 5f) / fire;
            PressItem();
        }

        float heldFor;
    }
}
