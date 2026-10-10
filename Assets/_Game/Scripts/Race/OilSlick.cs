using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// A slick of oil on the road: drive through it and the kart spins (once; the same kart again only after 2 s).
    /// A solo race lays a few at its start, on the straights and corner exits, off to one side or the other, so there
    /// is always a way round; a harder circuit gets more. The rivals see them and steer round (KartAi.Traffic).
    /// The prefab is Resources/OilSlick (ItemSetup): a dark iridescent puddle, a faint sheen, a low trigger.
    public sealed class OilSlick : MonoBehaviour
    {
        public static readonly List<OilSlick> All = new();
        readonly Dictionary<Kart, float> last = new();

        void OnEnable() => All.Add(this);
        /// The kart that just dropped it (Maroo's ability) is not caught by it for a while.
        public void Spare(Kart k, float seconds) => last[k] = Time.time + seconds - 2f;
        void OnDisable() => All.Remove(this);

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<Kart>();
            if (kart == null || kart.spinningOut) return;
            if (last.TryGetValue(kart, out float at) && Time.time - at < 2f) return;
            last[kart] = Time.time;
            kart.SpinOut(Kart.SpinAxis.Yaw, 1);          // spinOutEvent: the player's hooks shake the camera and yelp
            Debug.Log($"[race] {kart.name} spun on oil");
        }

        /// Lays this race's slicks along the waypoints: 1 + the circuit's difficulty (1-3) + the Tour cup's hazard level
        /// (RaceMode.Hazard, 0-4), one more on a long lap, never on the grid, near a turbo pad or near each other.
        public static void Spawn(int seed, int difficulty)
        {
            var prefab = Resources.Load<GameObject>("OilSlick");
            var root = GameObject.Find("Waypoints");
            if (prefab == null || root == null || root.transform.childCount < 16 || GameObject.Find("OilSlicks") != null) return;
            var wp = root.transform; int n = wp.childCount;
            float lap = 0f;
            for (int i = 0; i < n; i++) lap += Vector3.Distance(wp.GetChild(i).position, wp.GetChild((i + 1) % n).position);
            var rnd = new System.Random(seed);
            int count = 1 + Mathf.Clamp(difficulty, 1, 7) + (lap > 1200f ? 1 : 0);

            var pads = GameObject.Find("TurboPads");
            var open = new List<int>();
            for (int i = 7; i < n - 3; i++)
            {
                if (Vector3.Angle(wp.GetChild(i - 1).forward, wp.GetChild(i + 1).forward) > 35f) continue;   // straights and corner exits, not hairpins
                bool nearPad = false;
                if (pads != null) foreach (Transform p in pads.transform) if (Vector3.Distance(p.position, wp.GetChild(i).position) < 30f) nearPad = true;
                if (!nearPad) open.Add(i);
            }
            var parent = new GameObject("OilSlicks").transform;
            while (parent.childCount < count && open.Count > 0)
            {
                int i = open[rnd.Next(open.Count)];
                open.RemoveAll(j => Mathf.Abs(j - i) < 5);
                var at = wp.GetChild(i);
                // to one side of the middle: there is always a lane past it
                float side = (rnd.Next(2) == 0 ? -1f : 1f) * (1.5f + (float)rnd.NextDouble() * 2.5f);
                if (!Physics.Raycast(at.position + at.right * side + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var fwd = Vector3.ProjectOnPlane(at.forward, hit.normal).normalized;
                var spin = Quaternion.AngleAxis((float)rnd.NextDouble() * 360f, hit.normal);
                Instantiate(prefab, hit.point + hit.normal * 0.04f, spin * Quaternion.LookRotation(fwd, hit.normal), parent);
            }
            Debug.Log($"[race] {parent.childCount} oil slick(s) on a {lap:F0} m lap");
        }
    }
}
