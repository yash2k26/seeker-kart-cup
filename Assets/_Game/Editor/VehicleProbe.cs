using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Measures the Car Kit vehicles (parts, colour cells with bounds, roof profile) and the seated driver, so the
// vehicle table in ContentSetup is set from geometry instead of eyeballed. Run: tools/unity.sh VehicleProbe.Run
public static class VehicleProbe
{
    const string Art = "Assets/_Game/Art/";
    static readonly string[] Names = { "race-future", "tractor", "taxi", "van", "hatchback-sports", "police", "firetruck", "garbage-truck" };

    public static void Run()
    {
        var cm = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(cm, File.ReadAllBytes(Art + "Textures/Colormaps/carkit_colormap.png"));
        var sb = new System.Text.StringBuilder();
        foreach (var n in Names)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Karts/{n}.fbx");
            if (fbx == null) { sb.AppendLine($"missing {n}"); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); inst.transform.localScale = Vector3.one;
            sb.AppendLine($"== {n}");
            foreach (var t in inst.GetComponentsInChildren<Transform>())
            {
                var r = t.GetComponent<Renderer>();
                sb.AppendLine($"  node {t.name} pos {t.localPosition} scale {t.localScale}" + (r ? $" bounds c{r.bounds.center} s{r.bounds.size}" : ""));
            }
            var parts = inst.GetComponentsInChildren<MeshFilter>().Where(m => !m.name.StartsWith("wheel") && m.sharedMesh != null).ToArray();
            var combine = parts.Select(m => new CombineInstance { mesh = m.sharedMesh, transform = m.transform.localToWorldMatrix }).ToArray();
            var mesh = new Mesh(); mesh.CombineMeshes(combine, true, true); mesh.RecalculateBounds();
            var b = mesh.bounds; sb.AppendLine($"  body min {b.min} max {b.max} size {b.size}");
            var v = mesh.vertices; var uv = mesh.uv; var tris = mesh.triangles;
            var cells = new Dictionary<int, (int n, Bounds b, Color32 c)>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var p = uv[tris[i]]; Color32 c = cm.GetPixelBilinear(p.x, p.y);
                int key = (c.r / 16 << 8) | (c.g / 16 << 4) | (c.b / 16);
                var bb = new Bounds(v[tris[i]], Vector3.zero); bb.Encapsulate(v[tris[i + 1]]); bb.Encapsulate(v[tris[i + 2]]);
                if (cells.TryGetValue(key, out var e)) { e.b.Encapsulate(bb); cells[key] = (e.n + 1, e.b, e.c); } else cells[key] = (1, bb, c);
            }
            foreach (var kv in cells.OrderByDescending(k => k.Value.n))
                sb.AppendLine($"  cell #{kv.Value.c.r:X2}{kv.Value.c.g:X2}{kv.Value.c.b:X2} tris {kv.Value.n} y[{kv.Value.b.min.y:F2},{kv.Value.b.max.y:F2}] z[{kv.Value.b.min.z:F2},{kv.Value.b.max.z:F2}] x[{kv.Value.b.min.x:F2},{kv.Value.b.max.x:F2}]");
            const int S = 20; var maxY = new float[S]; var maxX = new float[S];
            for (int i = 0; i < v.Length; i++)
            {
                int s = Mathf.Clamp((int)((v[i].z - b.min.z) / b.size.z * S), 0, S - 1);
                maxY[s] = Mathf.Max(maxY[s], v[i].y); maxX[s] = Mathf.Max(maxX[s], Mathf.Abs(v[i].x));
            }
            sb.AppendLine("  z    : " + string.Join(" ", Enumerable.Range(0, S).Select(s => $"{b.min.z + (s + 0.5f) * b.size.z / S,5:F2}")));
            sb.AppendLine("  maxY : " + string.Join(" ", maxY.Select(y => $"{y,5:F2}")));
            sb.AppendLine("  maxX : " + string.Join(" ", maxX.Select(x => $"{x,5:F2}")));
            Object.DestroyImmediate(inst);
        }
        var drv = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Drivers/Driver_Toly.prefab");
        if (drv != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(drv);
            var anim = go.GetComponent<Animation>();
            if (anim != null && anim.clip != null) anim.clip.SampleAnimation(go, 0f);
            sb.AppendLine("== Driver_Toly seated (clip t=0), root at origin");
            foreach (var t in go.GetComponentsInChildren<Transform>()) sb.AppendLine($"  {t.name} world {t.position} lossy {t.lossyScale.x:F3}");
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled) sb.AppendLine($"  renderer {r.name} c{r.bounds.center} s{r.bounds.size}");
            Object.DestroyImmediate(go);
        }
        File.WriteAllText("Logs/VehicleProbe.txt", sb.ToString());
        Debug.Log(sb.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
