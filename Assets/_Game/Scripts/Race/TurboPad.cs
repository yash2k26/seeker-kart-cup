using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// A turbo strip on the road: drive over it and the kart is kicked forward (Powerslide's BoostPad, on the same
    /// object, does the kick; the KartCup/TurboPad shader runs the chevrons). A race gets one, sometimes two, and up to three on a long lap, laid at random on its
    /// straights when the race starts; an online room seeds the draw, so everyone sees the same ones.
    public sealed class TurboPad : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<Kart>();
            if (kart != null && RaceManager.Instance != null && kart == RaceManager.Instance.Player)
            {
                AudioDirector.Instance?.UseItem("Turbo");
                Haptics.Play(Haptics.Kind.Tick);
            }
        }

        /// Lays this race's pads along the waypoints (TrackSetup's, ~26 m apart).
        public static void Spawn(int seed)
        {
            var prefab = Resources.Load<GameObject>("TurboPad");
            var root = GameObject.Find("Waypoints");
            if (prefab == null || root == null || root.transform.childCount < 16 || GameObject.Find("TurboPads") != null) return;
            var wp = root.transform; int n = wp.childCount;
            float lap = 0f;
            for (int i = 0; i < n; i++) lap += Vector3.Distance(wp.GetChild(i).position, wp.GetChild((i + 1) % n).position);
            var rnd = new System.Random(seed);
            int most = Mathf.Clamp(Mathf.RoundToInt(lap / 550f), 1, 5), count = rnd.Next(Mathf.Max(1, most - 1), most + 1);   // 3-4 on a 2 km lap

            // on the straights, clear of the grid and of each other
            var open = new List<int>();
            for (int i = 5; i < n - 3; i++)
                if (Vector3.Angle(wp.GetChild(i - 1).forward, wp.GetChild(i + 1).forward) < 9f) open.Add(i);
            var parent = new GameObject("TurboPads").transform;
            for (int k = 0; k < count && open.Count > 0; k++)
            {
                int i = open[rnd.Next(open.Count)];
                open.RemoveAll(j => Mathf.Abs(j - i) < 8);
                var at = wp.GetChild(i);
                float side = ((float)rnd.NextDouble() * 2f - 1f) * 3.5f;
                if (!Physics.Raycast(at.position + at.right * side + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var fwd = Vector3.ProjectOnPlane(at.forward, hit.normal).normalized;
                Instantiate(prefab, hit.point + hit.normal * 0.06f, Quaternion.LookRotation(fwd, hit.normal), parent);
            }
            Debug.Log($"[race] {parent.childCount} turbo pad(s) on a {lap:F0} m lap");
        }
    }
}
