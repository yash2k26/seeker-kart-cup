using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// Kartel Tour's later series lay roadworks on the straights (13 Oct 2026, the user: "in every series the tracks are
    /// same only, the tracks should get difficult or long when the user reaches higher level"): slaloms of the circuit's
    /// own crates, two rows a waypoint apart, each row across the road with one gap at an edge, the gap switching sides
    /// from row to row. They are Smashables, so a kart (the AI's too) drives through at a cost in speed, never stops dead.
    public static class Roadworks
    {
        public static void Spawn(int seed, int slaloms)
        {
            var root = GameObject.Find("Waypoints");
            if (slaloms <= 0 || root == null || root.transform.childCount < 20 || GameObject.Find("Roadworks") != null) return;
            var props = Object.FindObjectsByType<Smashable>(FindObjectsSortMode.None).Where(p => p.transform.parent == null || p.transform.parent.name != "Roadworks").ToArray();
            // the circuit's biggest smashable prop (a crate or barrel reads from further than a flower pot), cones last
            static float Size(Smashable p) { var r = p.GetComponentInChildren<Renderer>(); return r != null ? r.bounds.size.magnitude : 0f; }
            var crate = props.Where(p => !p.name.StartsWith("item-cone")).OrderByDescending(Size).FirstOrDefault() ?? props.FirstOrDefault();
            if (crate == null) return;

            var wp = root.transform; int n = wp.childCount;
            Vector3 P(int i) => wp.GetChild(((i % n) + n) % n).position;
            Vector3 Dir(int i) { var d = P(i + 1) - P(i); d.y = 0f; return d.normalized; }
            // clear of everything else on the road: the grid, pads, oil, ramps, critter crossings, item rows, the shortcut's mouth
            var taken = new List<Vector3> { P(0) };
            foreach (var name in new[] { "TurboPads", "OilSlicks", "Ramps", "Critters", "ItemBoxes" })
            {
                var g = GameObject.Find(name);
                if (g != null) foreach (Transform c in g.transform) taken.Add(c.position);
            }
            var cut = ShortcutPath.Instance;
            if (cut != null && cut.from != null) taken.Add(cut.from.transform.position);
            if (cut != null && cut.to != null) taken.Add(cut.to.transform.position);
            bool Clear(int i, float r) => taken.All(t => (t - P(i)).sqrMagnitude > r * r && (t - P(i + 1)).sqrMagnitude > r * r);
            bool Straight(int i) => Vector3.Angle(Dir(i - 1), Dir(i + 2)) < 20f;

            var rnd = new System.Random(seed);
            var range = Enumerable.Range(10, Mathf.Max(0, n - 18)).ToList();
            // straights first; a tight circuit (Pine Ridge) has few, so then any stretch with room round it
            var open = range.Where(i => Straight(i) && Clear(i, 24f)).OrderBy(_ => rnd.Next())
                .Concat(range.Where(i => Clear(i, 18f)).OrderBy(_ => rnd.Next())).Distinct().ToList();
            float half = HalfWidth();
            var parent = new GameObject("Roadworks").transform;
            var used = new List<int>();
            foreach (int i in open)
            {
                if (used.Count >= slaloms) break;
                if (used.Any(u => Mathf.Abs(u - i) < 6)) continue;
                used.Add(i);
                bool left = rnd.Next(2) == 0;
                for (int row = 0; row < 2; row++, left = !left)
                {
                    var at = P(i + row); var dir = Dir(i + row); var side = Vector3.Cross(Vector3.up, dir);
                    for (int lane = 0; lane < 4; lane++)
                    {
                        if (lane == (left ? 0 : 3)) continue;                       // the way through
                        var p = at + side * ((lane - 1.5f) * 0.44f * half);
                        if (Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                        var go = Object.Instantiate(crate.gameObject, p, Quaternion.LookRotation(dir), parent);
                        go.name = crate.name;
                    }
                }
            }
            Debug.Log($"[race] {used.Count} roadworks slalom(s) of {crate.name}");
        }

        /// The road's half-width, read off the start line's gate (TrackSetup: (halfW + kerb 1.4 + 6) * 2 wide).
        static float HalfWidth()
        {
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if (cp.index == 0 && cp.TryGetComponent<BoxCollider>(out var box)) return Mathf.Max(5f, box.size.x * cp.transform.lossyScale.x * 0.5f - 7.4f);
            return 8f;
        }
    }
}
