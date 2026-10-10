using System.Collections.Generic;
using UnityEngine;

namespace KartCup
{
    /// Beach Buggy's jumps: a striped kicker on one side of a straight, with a lane left beside it for anyone who would
    /// rather not. A solo race gets one or two, laid when it starts (like the turbo pads), only where the road runs on
    /// straight for about 66 m (a jump covers ~35), so a kart lands on road and lined up.
    public sealed class Ramp : MonoBehaviour
    {
        // The kit's air gravity is about twice the world's, so a 0.8 m lip alone gave a 0.35 s hop. A trigger over the
        // lip sets the climb instead: about a second in the air at any speed, AI karts too. The kit's airLandBoost
        // pays out on the landing.
        [SerializeField] float kick = 11f;

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<PowerslideKartPhysics.Kart>();
            if (kart == null || kart.rb == null) return;
            var v = kart.rb.linearVelocity;
            if (Vector3.Dot(v, transform.forward) < 8f || v.y >= kick) return;    // driven up it, and not already kicked
            kart.rb.linearVelocity = new Vector3(v.x, kick, v.z);
            if (RaceManager.Instance != null && kart == RaceManager.Instance.Player) Haptics.Play(Haptics.Kind.Tick);
        }

        public static void Spawn(int seed)
        {
            var prefab = Resources.Load<GameObject>("Ramp");
            var root = GameObject.Find("Waypoints");
            if (prefab == null || root == null || root.transform.childCount < 20 || GameObject.Find("Ramps") != null) return;
            var wp = root.transform; int n = wp.childCount;
            var rnd = new System.Random(seed);
            var avoid = new List<Vector3>();
            foreach (var name in new[] { "TurboPads", "OilSlicks" })
                if (GameObject.Find(name) is { } g) foreach (Transform t in g.transform) avoid.Add(t.position);
            var open = new List<int>();
            for (int i = 8; i < n - 4; i++)
            {
                if (Vector3.Angle(wp.GetChild(i - 1).forward, wp.GetChild(i + 2).forward) > 12f) continue;   // ~66 m: a jump is ~35
                bool near = false; foreach (var p in avoid) if (Vector3.Distance(p, wp.GetChild(i).position) < 30f) near = true;
                if (!near && !Roofed(wp.GetChild(i))) open.Add(i);
            }
            var parent = new GameObject("Ramps").transform;
            int count = rnd.Next(1, 3);
            while (parent.childCount < count && open.Count > 0)
            {
                int i = open[rnd.Next(open.Count)];
                open.RemoveAll(j => Mathf.Abs(j - i) < 10);
                Place(prefab, wp.GetChild(i).position + wp.GetChild(i).right * ((rnd.Next(2) == 0 ? -1f : 1f) * 3f), wp.GetChild(i).forward, parent);
            }
            Debug.Log($"[race] {parent.childCount} ramp(s)");
        }

        /// Palm Bay's tunnel over the approach or the jump's 40 m: no ramp there. Its tube has no collider, only its
        /// walls do, so their bounds stand for it. (Gantries and overhead signs clear 12 m; a jump peaks under 4.)
        static bool Roofed(Transform w)
        {
            var walls = GameObject.Find("TunnelWalls")?.GetComponent<MeshFilter>();   // (its collider's bounds read zero this early)
            if (walls == null || walls.sharedMesh == null) return false;
            var b = walls.sharedMesh.bounds; b.center = walls.transform.TransformPoint(b.center); b.Expand(6f);
            for (float d = -15f; d <= 40f; d += 5f) if (b.Contains(w.position + w.forward * d)) return true;
            return false;
        }

        /// On the road at `at`, facing `forward` along it, sunk a few centimetres so the foot is not a step.
        public static GameObject Place(GameObject prefab, Vector3 at, Vector3 forward, Transform parent)
        {
            if (!Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return null;
            var fwd = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
            return Instantiate(prefab, hit.point - hit.normal * 0.04f, Quaternion.LookRotation(fwd, hit.normal), parent);
        }
    }
}
