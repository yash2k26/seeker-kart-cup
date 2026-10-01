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
        public Material flashMaterial;       // the burst: a ball of light that swells and fades

        float born; bool burst; Kart thrower; Transform ball;

        public override void Initialize(ItemCastProperties props) { base.Initialize(props); born = Time.time; thrower = props.castKart; ball = transform.Find("Ball"); }

        // The burn: the ball throbs, faster as the fuse runs down.
        void Update()
        {
            if (ball == null) return;
            float k = Mathf.Clamp01((Time.time - born) / fuse);
            ball.localScale = Vector3.one * (0.9f + 0.18f * Mathf.Sin(Time.time * Mathf.Lerp(14f, 40f, k)));
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!burst && Time.time - born > fuse) Burst();
        }

        // Any kart that touches it sets it off; the thrower only once it has had casterIgnoreTime to get clear (it spawns
        // inside their collider, and the physics step can report that contact before the base class ignores it).
        protected override void OnCollisionEnter(Collision hit)
        {
            if (hit.collider.IsKart())
            {
                if (burst) return;
                bool own = thrower != null && hit.collider.transform.GetTopmostParentComponent<Kart>() == thrower;
                if (!own || Time.time - born > casterIgnoreTime) Burst();
                return;   // never the base class's one-kart hit: it would spin the thrower on a collider it does not ignore
            }
            base.OnCollisionEnter(hit);
        }

        void Burst()
        {
            burst = true;
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
            SunFlash.At(transform.position, radius, flashMaterial);
            Destroy(gameObject);
        }
    }

    /// A ball of light that swells past the blast radius and fades: the burst itself.
    sealed class SunFlash : MonoBehaviour
    {
        float radius, t; Material mat; Light light;

        public static void At(Vector3 p, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SunFlash"; go.transform.position = p;
            Destroy(go.GetComponent<Collider>());
            var f = go.AddComponent<SunFlash>();
            f.radius = radius;
            f.mat = material != null ? new Material(material) : go.GetComponent<Renderer>().material;
            go.GetComponent<Renderer>().sharedMaterial = f.mat;
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(go.transform, false); l.transform.localPosition = Vector3.up * 0.3f;
            l.color = new Color(1f, 0.75f, 0.35f); l.intensity = 40f; l.range = radius * 2.5f;
            f.light = l;
        }

        void Update()
        {
            t += Time.deltaTime / 0.45f;
            float k = Mathf.Clamp01(t), ease = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, radius * 2.2f, ease);
            var c = Color.Lerp(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.45f, 0.1f, 0f), k);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            if (light != null) light.intensity = 40f * (1f - k);
            if (k >= 1f) Destroy(gameObject);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
