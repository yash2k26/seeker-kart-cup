using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Is the start grid actually on the road? Renders lie in edit mode, so ask the geometry.
public static class RoadProbe
{
    [MenuItem("Build/Probe Road")]
    public static void Run()
    {
        for (int i = 1; i <= 3; i++)
        {
            EditorSceneManager.OpenScene($"Assets/_Game/Scenes/Race{i}.unity", OpenSceneMode.Single);
            var road = GameObject.Find("Track")?.transform.Find("Road")?.GetComponent<MeshRenderer>()
                       ?? Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Road");
            var grid = GameObject.Find("StartGrid");
            if (road == null || grid == null) { Debug.Log($"[road] Race{i}: road={road != null} grid={grid != null}"); continue; }

            var slot = grid.transform.GetChild(0);
            var b = road.bounds;
            // Nearest road vertex to the pole slot, in world space.
            var mesh = road.GetComponent<MeshFilter>().sharedMesh;
            float best = float.MaxValue;
            foreach (var v in mesh.vertices)
            {
                float d = Vector3.Distance(road.transform.TransformPoint(v), slot.position);
                if (d < best) best = d;
            }
            Debug.Log($"[road] Race{i}: slot0={slot.position} roadBounds=c{b.center} s{b.size} verts={mesh.vertexCount} nearestRoadVertexToPole={best:F2}m mat={road.sharedMaterial?.name}");
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
