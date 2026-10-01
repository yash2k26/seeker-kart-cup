using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Radiants asked for "a sun bomb like the bomb in Mario Kart": lobbed ahead, it rolls to a stop and burns for a
    /// moment, then goes off in a ball of light that spins out every kart near it — the thrower too, if they hang about.
    /// Powerslide's projectile does the throw, the arc, the bounces and the caster grace period; this adds the fuse and
    /// the area burst in place of the one-kart hit.
    public sealed class SunBomb : SpawnedProjectileItem
    {
        public float fuse = 2.4f, radius = 8f, knock = 7f;
        public GameObject burst;             // the solar burst: core, sun-shaped shockwave, embers, glitter, gold light
        public ParticleSystem warn;          // the amber ring that pulses on the road once it has landed

        float born; bool done, landed; Kart thrower; Vector3 groundUp = Vector3.up; Transform ball; Vector3 ballScale;

        public override void Initialize(ItemCastProperties props) { base.Initialize(props); born = Time.time; thrower = props.castKart; ball = transform.Find("Ball"); if (ball != null) ballScale = ball.localScale; }

        // The burn: the ball throbs, faster as the fuse runs down.
        void Update()
        {
            if (ball == null) return;
            float k = Mathf.Clamp01((Time.time - born) / fuse);
            ball.localScale = ballScale * (1f + 0.16f * k * Mathf.Sin(Time.time * Mathf.Lerp(14f, 40f, k)));
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!done && Time.time - born > fuse) Burst();
        }

        // Any kart that touches it sets it off; the thrower only once it has had casterIgnoreTime to get clear (it spawns
        // inside their collider, and the physics step can report that contact before the base class ignores it).
        protected override void OnCollisionEnter(Collision hit)
        {
            if (hit.collider.IsKart())
            {
                if (done) return;
                bool own = thrower != null && hit.collider.transform.GetTopmostParentComponent<Kart>() == thrower;
                if (!own || Time.time - born > casterIgnoreTime) Burst();
                return;   // never the base class's one-kart hit: it would spin the thrower on a collider it does not ignore
            }
            // landed: the ring lies on this slope, and so will the burst. Only ground counts: the side of a kerb or a
            // rock is a contact too, and a ring laid on that stands upright.
            if (!landed && hit.contactCount > 0 && hit.GetContact(0).normal.y > 0.7f)
            {
                landed = true; groundUp = hit.GetContact(0).normal;
                if (warn != null)
                {
                    warn.transform.SetPositionAndRotation(transform.position - groundUp * 0.38f, Quaternion.FromToRotation(Vector3.up, groundUp) * Quaternion.Euler(90, 0, 0));
                    warn.Play();
                }
            }
            base.OnCollisionEnter(hit);
        }

        void Burst()
        {
            done = true;
            var seen = new HashSet<Kart>();
            foreach (var c in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c.IsKart()) continue;
                var kart = c.transform.GetTopmostParentComponent<Kart>();
                if (kart == null || !seen.Add(kart)) continue;
                kart.SpinOut(kartSpin, kartSpinCount);
                var away = kart.transform.position - transform.position; away.y = 0;
                if (kart.rb != null) kart.rb.AddForce((away.normalized + Vector3.up * 0.7f).normalized * knock, ForceMode.VelocityChange);
            }
            if (burst != null) Instantiate(burst, transform.position, Quaternion.FromToRotation(Vector3.up, groundUp));
            Destroy(gameObject);
        }
    }

    /// On the burst effect: the gold light that turns the road to day for a moment, then the clean-up once the last
    /// glitter has gone out.
    public sealed class SunFlash : MonoBehaviour
    {
        public Light glow;
        public float fade = 0.9f, life = 4f;
        float t, start;

        void Start() { if (glow != null) start = glow.intensity; Destroy(gameObject, life); }

        void Update()
        {
            t += Time.deltaTime;
            if (glow != null) glow.intensity = start * Mathf.Pow(Mathf.Clamp01(1f - t / fade), 2f);
        }
    }
}
