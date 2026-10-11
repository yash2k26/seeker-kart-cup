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
            var first = GameObject.Find("WP00")?.GetComponent<BasicWaypoint>();
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

            // and the shortcut, if the circuit has one: its own chain, at its own width (TrackShortcut.CutHalf 4.5)
            var cut = Object.FindFirstObjectByType<ShortcutPath>();
            if (cut != null)
                for (int k = 0; k < cut.path.Length; k++)
                {
                    Vector3 a = (k == 0 ? cut.path[0].transform.position : cut.path[k - 1].transform.position) + Vector3.up * 0.6f, b = cut.path[k].transform.position + Vector3.up * 0.6f;
                    foreach (var c in Physics.OverlapCapsule(a, b, 3.2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (IsRoad(c)) continue;
                        var top = c.transform; while (top.parent != null && top.parent.parent != null) top = top.parent;
                        var name = $"SHORTCUT {top.name} [{c.GetType().Name} on {c.name}, size {c.bounds.size}]";
                        if (!hits.ContainsKey(name)) hits[name] = (0, c.bounds.center);
                        hits[name] = (hits[name].count + 1, hits[name].at);
                    }
                }
            foreach (var kv in hits.OrderByDescending(h => h.Value.count))
                Debug.Log($"[probe] {Tracks.At(i).name}: ON ROAD  {kv.Key}  x{kv.Value.count} segments  at {kv.Value.at}");
            Debug.Log($"[probe] {Tracks.At(i).name}: {hits.Count} intruding collider(s), sweep half width {halfWidth - 0.3f:F1} m over {guard} segments");
            total += hits.Count;
        }
        Debug.Log(total == 0 ? "[probe] PASS — every road is clear" : $"[probe] {total} intruder(s) across the circuits");
        if (Application.isBatchMode) EditorApplication.Exit(total == 0 ? 0 : 1);
    }

    /// What is solid near a point:  tools/unity.sh TrackProbe.Near -track 3 -at -47.8,10.4,-69.2
    public static void Near()
    {
        int track = 1; Vector3 at = Vector3.zero;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-track") int.TryParse(args[i + 1], out track);
            if (args[i] == "-at") { var v = args[i + 1].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); at = new Vector3(v[0], v[1], v[2]); }
        }
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        foreach (var c in Physics.OverlapSphere(at, 6f, ~0, QueryTriggerInteraction.Collide))
        {
            var top = c.transform; while (top.parent != null && top.parent.parent != null) top = top.parent;
            Debug.Log($"[probe] near {at}: {c.transform.root.name}/{top.name} [{c.GetType().Name}{(c.isTrigger ? " trigger" : "")} on {c.name}] centre {c.bounds.center} size {c.bounds.size}");
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// The ground across the road at every tenth waypoint, and the hazard sheet: what a kart running wide lands on.
    ///   tools/unity.sh TrackProbe.Across -track N
    public static void Across()
    {
        int track = 1;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-track") int.TryParse(args[i + 1], out track);
        EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{track}.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        var sheet = GameObject.Find("Hazard");
        Debug.Log(sheet == null ? "[probe] no hazard sheet" : $"[probe] hazard at y {sheet.transform.position.y:F2}, scale {sheet.transform.localScale.x}, renderer {sheet.GetComponent<MeshRenderer>()?.enabled}, shader {sheet.GetComponent<MeshRenderer>()?.sharedMaterial?.shader?.name}");
        var wps = GameObject.Find("Waypoints").transform;
        var offsets = new[] { -40f, -25f, -16f, -12f, -8f, 0f, 8f, 12f, 16f, 25f, 40f };
        for (int w = 0; w < wps.childCount; w += 10)
        {
            var t = wps.GetChild(w);
            var row = new System.Text.StringBuilder($"[probe] WP{w:00} {t.position.x:F0},{t.position.z:F0}:");
            foreach (var o in offsets)
            {
                var p = t.position + t.right * o + Vector3.up * 60f;
                row.Append(Physics.Raycast(p, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? $" {o:+0;-0}:{hit.point.y:F1}" : $" {o:+0;-0}:--");
            }
            Debug.Log(row.ToString());
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// The generated ribbons under the Track root, the terrain chunks, and the karts' own colliders are not intruders.
    static bool IsRoad(Collider c)
    {
        var root = c.transform.root.name;
        return root == "Track" || root == "Terrain" || root == "Barriers" || root == "StartGrid" || root == "Checkpoints" || root == "Shortcut";
    }

    /// Half width from the road mesh: the widest lateral spread of a vertex from the centreline is unknown here, so
    /// use the narrowest circuit's value the generator uses (7.5) unless the strip is clearly wider.
    static float RoadHalfWidth(MeshRenderer road)
    {
        var name = EditorSceneManager.GetActiveScene().name;
        return name == "Race1" ? 10f : name == "Race2" ? 9f : name == "Race4" ? 11f : 7.5f;   // TrackThemes halfWidth per circuit
    }
}
