using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Beach Buggy's critters: a few wander across the road at a crossing, back and forth, bobbing, pausing at each kerb.
    /// A kart that hits one sends it flying in a burst of feathers with a squawk, and loses a little speed; it is back at
    /// its crossing a few seconds later for the next lap. A trigger, not a body (Smashable has the reason). Solo races
    /// only, like the oil (RaceSpawner): rooms never get them.
    ///
    /// One animal per circuit, Beach Buggy's way of making a map read at a glance (crabs on the beach, penguins on the
    /// ice): Resources/Critter_<track id> (CritterSetup), the chicken where a circuit has none of its own.
    public sealed class Critter : MonoBehaviour
    {
        public static readonly List<Critter> All = new();
        public static event System.Action<Critter, Kart> Hit;   // Smash-Up counts them
        const float Flight = 1.2f, Back = 7f, Pause = 1.1f;
        [SerializeField] AudioClip squawk;
        [SerializeField] float pitch = 1f;      // the same squawk, pitched for the animal
        [SerializeField] bool sideways;         // a crab crosses the road side-on
        [SerializeField] Color puffColor = new(1f, 0.98f, 0.94f);   // feathers, or sand
        Vector3 a, b, vel, spin, homeScale;
        float offset, speed = 1.4f, hitAt = -1f, downUntil = -1f;
        Renderer[] shown;

        void Awake() { shown = GetComponentsInChildren<Renderer>(); homeScale = transform.localScale; }
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// Walk between `from` and `to` (ground points either side of the road), starting `phase` seconds into the loop.
        public void Walk(Vector3 from, Vector3 to, float phase, float pace) { a = from; b = to; offset = phase; speed = pace; transform.position = from; }

        void Update()
        {
            if (hitAt >= 0f)
            {
                float t = Time.time - hitAt;
                vel += Physics.gravity * 0.55f * Time.deltaTime;   // a chicken flaps: it falls slower than a crate
                transform.position += vel * Time.deltaTime;
                transform.Rotate(spin * Time.deltaTime, Space.World);
                if (t > Flight) { hitAt = -1f; downUntil = Time.time + Back; Show(false); }
                return;
            }
            if (downUntil >= 0f) { if (Time.time < downUntil) return; downUntil = -1f; transform.localScale = homeScale; Show(true); }
            float len = Vector3.Distance(a, b), cross = Mathf.Max(0.5f, len / Mathf.Max(0.2f, speed)), period = 2f * (cross + Pause);
            float u = Mathf.Repeat(Time.time + offset, period);
            Vector3 pos, dir; bool walking = true;
            if (u < cross) { pos = Vector3.Lerp(a, b, u / cross); dir = b - a; }
            else if (u < cross + Pause) { pos = b; dir = a - b; walking = false; }
            else if (u < 2f * cross + Pause) { pos = Vector3.Lerp(b, a, (u - cross - Pause) / cross); dir = a - b; }
            else { pos = a; dir = b - a; walking = false; }
            dir.y = 0f;
            float bob = walking ? Mathf.Abs(Mathf.Sin((Time.time + offset) * 13f)) * 0.14f : 0f;
            transform.position = pos + Vector3.up * bob;
            if (sideways) dir = Vector3.Cross(Vector3.up, dir);
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (!walking) transform.localRotation *= Quaternion.Euler(Mathf.Max(0f, Mathf.Sin((Time.time + offset) * 9f)) * 30f, 0f, 0f);   // pecking at the kerb
        }

        void OnTriggerEnter(Collider other)
        {
            if (hitAt >= 0f || downUntil >= 0f || !other.IsKart()) return;
            var kart = other.transform.GetTopmostParentComponent<Kart>();
            if (kart == null || kart.rb == null) return;
            var v = kart.rb.linearVelocity;
            var away = transform.position - kart.transform.position; away.y = 0f;
            vel = v * 0.7f + away.normalized * 4f + Vector3.up * (7f + v.magnitude * 0.15f);
            spin = Random.onUnitSphere * Random.Range(540f, 900f);
            hitAt = Time.time;
            Debug.Log($"[race] {kart.name} hit a {name.Replace("(Clone)", "").ToLowerInvariant()}");
            Hit?.Invoke(this, kart);
            if (!kart.rb.isKinematic) kart.rb.linearVelocity = v * 0.88f;          // feathers in the grille: a little speed gone
            var puff = Resources.Load<GameObject>("FX/SmashPuff");
            if (puff != null)
            {
                var p = Instantiate(puff, transform.position + Vector3.up * 0.6f, Quaternion.identity);
                foreach (var ps in p.GetComponentsInChildren<ParticleSystem>()) { var m = ps.main; m.startColor = puffColor; m.startSizeMultiplier *= 1.4f; }
            }
            var prog = kart.GetComponent<KartProgress>();
            bool player = prog != null && prog.isPlayer;
            if (squawk != null && AudioDirector.Instance != null && (player || Camera.main == null || (Camera.main.transform.position - transform.position).sqrMagnitude < 30f * 30f))
                AudioDirector.Instance.Play(squawk, player ? 0.9f : 0.5f, pitch * Random.Range(0.92f, 1.12f));
            if (player) { Haptics.Play(Haptics.Kind.Hit); CameraFeel.Instance?.Kick(-kart.transform.forward, 0.35f); }
        }

        void Show(bool on) { foreach (var r in shown) if (r != null) r.enabled = on; }

        /// This race's crossings: three on straights along the waypoints, away from the grid, the turbo pads and the oil,
        /// two animals each, at different paces so they never walk in step.
        public static void Spawn(int seed, string trackId)
        {
            var prefab = Resources.Load<GameObject>("Critter_" + trackId) ?? Resources.Load<GameObject>("Chicken");
            var root = GameObject.Find("Waypoints");
            if (prefab == null || root == null || root.transform.childCount < 20 || GameObject.Find("Critters") != null) return;
            var wp = root.transform; int n = wp.childCount;
            var rnd = new System.Random(seed);
            var avoid = new List<Vector3>();
            foreach (var name in new[] { "TurboPads", "OilSlicks", "Ramps" })
                if (GameObject.Find(name) is { } g) foreach (Transform t in g.transform) avoid.Add(t.position);
            var open = new List<int>();
            for (int i = 10; i < n - 4; i++)
            {
                if (Vector3.Angle(wp.GetChild(i - 1).forward, wp.GetChild(i + 1).forward) > 20f) continue;   // straights only: a chicken in a hairpin is a wall
                bool near = false; foreach (var p in avoid) if (Vector3.Distance(p, wp.GetChild(i).position) < 25f) near = true;
                if (!near) open.Add(i);
            }
            var parent = new GameObject("Critters").transform;
            int crossings = 0;
            while (crossings < 3 && open.Count > 0)
            {
                int i = open[rnd.Next(open.Count)];
                open.RemoveAll(j => Mathf.Abs(j - i) < 8);
                var at = wp.GetChild(i);
                Vector3 Ground(Vector3 p) => Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
                var from = Ground(at.position - at.right * 6f); var to = Ground(at.position + at.right * 6f);
                for (int k = 0; k < 2; k++)
                {
                    var c = Instantiate(prefab, parent).GetComponent<Critter>();
                    c.Walk(k == 0 ? from : to, k == 0 ? to : from, (float)rnd.NextDouble() * 6f, 1.1f + (float)rnd.NextDouble() * 0.8f);
                }
                crossings++;
            }
            Debug.Log($"[race] {parent.childCount} {prefab.name.ToLowerInvariant()}(s) at {crossings} crossing(s)");
        }
    }
}
