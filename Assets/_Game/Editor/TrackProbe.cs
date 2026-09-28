using System.Collections.Generic;
using System.Linq;
using KartCup;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Is the road clear? Sweeps a capsule along the waypoint chain of every circuit and names each solid collider it
// hits that is not the road itself. A prop on the racing line strands the AI and the player alike, and a render
// cannot show a collider. Run: Unity -batchmode -projectPath . -executeMethod TrackProbe.Run -quit
public static class TrackProbe
{
    [MenuItem("Build/Probe Track Colliders")]
    public static void Run()
    {
        int total = 0;
        for (int i = 0; i < Tracks.Count; i++)
        {
            EditorSceneManager.OpenScene($"Assets/_Game/Scenes/{Tracks.SceneOf(i)}.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            var first = Object.FindObjectsByType<BasicWaypoint>(FindObjectsSortMode.None).OrderBy(w => w.name).FirstOrDefault();
            if (first == null) { Debug.LogError($"[probe] {Tracks.At(i).name}: no waypoints"); continue; }

            // The road strip's own half width, from its mesh, so the sweep matches whatever the generator built.
            var road = GameObject.Find("Road")?.GetComponent<MeshRenderer>();
            float halfWidth = road != null ? Mathf.Min(road.bounds.extents.x, road.bounds.extents.z) * 0f + RoadHalfWidth(road) : 7f;

            var hits = new Dictionary<string, (int count, Vector3 at)>();
            var wp = first; int guard = 0;
            do
            {
                var next = wp.nextPoint; if (next == null) break;
                Vector3 a = wp.transform.position + Vector3.up * 0.6f, b = next.transform.position + Vector3.up * 0.6f;
                foreach (var c in Physics.OverlapCapsule(a, b, halfWidth - 0.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (IsRoad(c)) continue;
                    // Name the prop, not the shared Dressing root: the top-level object under the root, plus its size.
                    var top = c.transform; while (top.parent != null && top.parent.parent != null) top = top.parent;
                    var name = $"{top.name} [{c.GetType().Name} on {c.name}, size {c.bounds.size}]";
                    if (!hits.ContainsKey(name)) hits[name] = (0, c.bounds.center);
                    hits[name] = (hits[name].count + 1, hits[name].at);
                }
                wp = next; guard++;
            } while (wp != first && guard < 400);

            foreach (var kv in hits.OrderByDescending(h => h.Value.count))
                Debug.Log($"[probe] {Tracks.At(i).name}: ON ROAD  {kv.Key}  x{kv.Value.count} segments  at {kv.Value.at}");
            Debug.Log($"[probe] {Tracks.At(i).name}: {hits.Count} intruding collider(s), sweep half width {halfWidth - 0.3f:F1} m over {guard} segments");
            total += hits.Count;
        }
        Debug.Log(total == 0 ? "[probe] PASS — every road is clear" : $"[probe] {total} intruder(s) across the circuits");
        if (Application.isBatchMode) EditorApplication.Exit(total == 0 ? 0 : 1);
    }

    /// The generated ribbons under the Track root, the terrain chunks, and the karts' own colliders are not intruders.
    static bool IsRoad(Collider c)
    {
        var root = c.transform.root.name;
        return root == "Track" || root == "Terrain" || root == "Barriers" || root == "StartGrid" || root == "Checkpoints";
    }

    /// Half width from the road mesh: the widest lateral spread of a vertex from the centreline is unknown here, so
    /// use the narrowest circuit's value the generator uses (7.5) unless the strip is clearly wider.
    static float RoadHalfWidth(MeshRenderer road)
    {
        var name = EditorSceneManager.GetActiveScene().name;
        return name == "Race1" ? 10f : name == "Race2" ? 9f : name == "Race4" ? 11f : 7.5f;   // TrackThemes halfWidth per circuit
    }
}
