using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Powerslide's drifting waypoint follower plus the two things a race needs: a difficulty dial and a
    /// gentle rubber band, so the pack stays on screen without ever out-driving the player outright.
    public sealed class KartAi : BasicWaypointFollowerDrift
    {
        [Header("Race")]
        [Range(0.75f, 1.05f)] public float skill = 0.95f;   // fraction of the kart's own top speed it will use
        public float catchUpPerMetre = 0.004f;              // added speed when behind the player
        public float maxCatchUp = 0.12f;
        public float itemInterval = 6f;

        Kart myKart;
        KartProgress progress;
        float baseSpeed, nextItem;

        public override void Awake()
        {
            base.Awake();
            myKart = GetComponent<Kart>();
            progress = GetComponent<KartProgress>();
            baseSpeed = myKart.maxSpeed;
            nextItem = Random.Range(2f, itemInterval);
        }

        /// The base class caches the following point in Awake, which has already run by the time a spawner
        /// assigns the target — so re-run it rather than duplicate its bookkeeping.
        public void SetTarget(BasicWaypoint waypoint) { targetPoint = waypoint; Awake(); }

        public override void Update()
        {
            base.Update();
            var race = RaceManager.Instance;
            if (race == null || race.Player == null || myKart == null) return;

            float behind = Mathf.Max(0, race.Player.score - progress.score);
            float band = Mathf.Min(behind * catchUpPerMetre, maxCatchUp);
            myKart.maxSpeed = baseSpeed * (skill + band);

            if (race.state != RaceManager.State.Racing) return;
            nextItem -= Time.deltaTime;
            if (nextItem <= 0f) { nextItem = itemInterval * Random.Range(0.7f, 1.4f); PressItem(); }
        }
    }
}
