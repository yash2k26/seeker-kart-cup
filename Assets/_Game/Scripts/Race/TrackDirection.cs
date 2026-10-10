using System.Collections.Generic;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Two levers on a circuit that cost no art: the lap driven the other way round, and mist.
    ///
    /// Reverse (Beach Buggy Racing 2: Island Adventure runs its circuits mirrored or reversed): everything that knows which
    /// way the lap runs reads it from the gates and the waypoints, so turning those round turns the race round.
    ///   - the waypoints keep WP00 on the start line and run backwards from it, each facing the new way: the AI's line,
    ///     the drone's lap, where the pads, ramps and crossings go and which way they face;
    ///   - the gates are numbered from the line the other way and turned round: the lap count, the respawn's heading and
    ///     WRONG WAY;
    ///   - the grid moves to the other side of the line, each slot as far up the road as it stood behind it.
    /// Solo only (RaceSpawner, before the grid is filled): the referee's tracks.json knows one direction, so a reversed
    /// race is never ranked (RaceMode.Ranked).
    public static class TrackDirection
    {
        /// Turns the circuit round; returns where the AI sets off (the start line), or null if the scene has no waypoints.
        public static BasicWaypoint Reverse(Transform[] grid, RaceManager race)
        {
            var root = GameObject.Find("Waypoints");
            if (root == null || root.transform.childCount < 8) return null;
            var wps = root.transform.Cast<Transform>().ToList();
            int n = wps.Count;
            var road = wps.Select(w => w.position).ToList();               // the lap as it ran, to lay the grid up it
            var line = race != null ? race.checkpoints.FirstOrDefault(c => c != null && c.index == 0) : null;
            Vector3 linePos = line != null ? line.transform.position : road[0];
            Vector3 lineFwd = Flat(line != null ? line.transform.forward : wps[0].forward);
            Vector3 lineRight = Vector3.Cross(Vector3.up, lineFwd);

            if (grid != null)
                foreach (var slot in grid)
                {
                    if (slot == null) continue;
                    float back = Vector3.Dot(linePos - slot.position, lineFwd), side = Vector3.Dot(slot.position - linePos, lineRight);
                    float lift = slot.position.y - Ground(slot.position).y;
                    var (at, dir) = Along(road, back);
                    var pos = at + Vector3.Cross(Vector3.up, -dir) * side;     // the same side of the kart, facing the new way
                    pos.y = Ground(pos).y + lift;
                    slot.SetPositionAndRotation(pos, Quaternion.LookRotation(-dir, Vector3.up));
                }

            var order = new List<Transform> { wps[0] };
            for (int i = n - 1; i >= 1; i--) order.Add(wps[i]);
            for (int k = 0; k < n; k++) { order[k].SetSiblingIndex(k); Turn(order[k]); }
            for (int k = 0; k < n; k++) order[k].GetComponent<BasicWaypoint>().nextPoint = order[(k + 1) % n].GetComponent<BasicWaypoint>();

            ShortcutPath.Instance?.Reverse();                 // entered from its other end
            if (race != null)
            {
                int count = race.checkpoints.Length;
                foreach (var c in race.checkpoints) { if (c == null) continue; c.index = (count - c.index) % count; Turn(c.transform); }
                race.checkpoints = race.checkpoints.Where(c => c != null).OrderBy(c => c.index).ToArray();
            }
            Debug.Log($"[race] the circuit runs the other way round ({n} waypoints, {race?.checkpoints.Length ?? 0} gates)");
            return wps[0].GetComponent<BasicWaypoint>();
        }

        /// Beach Buggy Racing 2's weather, the cheap way: the fog the circuit already has, drawn in close. The far side of
        /// a corner goes before the corner does.
        public static void Mist()
        {
            RenderSettings.fogStartDistance = Mathf.Min(RenderSettings.fogStartDistance, 4f);
            RenderSettings.fogEndDistance = Mathf.Max(70f, RenderSettings.fogEndDistance * 0.3f);
            // haze is the sky's colour; mist is paler and greyer than the light it hangs in
            var c = RenderSettings.fogColor; float grey = Mathf.Clamp01(c.grayscale * 1.5f + 0.06f);
            RenderSettings.fogColor = Color.Lerp(c, new Color(grey, grey, grey * 1.04f), 0.4f);
            Debug.Log($"[race] mist: fog {RenderSettings.fogStartDistance:0}-{RenderSettings.fogEndDistance:0} m");
        }

        static void Turn(Transform t) => t.rotation = Quaternion.LookRotation(-t.forward, t.up);
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        /// The point `metres` up the road from the first waypoint, and the road's heading there.
        static (Vector3 at, Vector3 dir) Along(List<Vector3> road, float metres)
        {
            metres = Mathf.Max(0f, metres);
            for (int i = 0; i < road.Count; i++)
            {
                Vector3 a = road[i], b = road[(i + 1) % road.Count];
                float len = Vector3.Distance(a, b);
                if (metres <= len || i == road.Count - 1) return (Vector3.Lerp(a, b, len > 0f ? Mathf.Clamp01(metres / len) : 0f), Flat(b - a));
                metres -= len;
            }
            return (road[0], Vector3.forward);
        }

        static Vector3 Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.IsKart() ? hit.point : p;
    }
}
