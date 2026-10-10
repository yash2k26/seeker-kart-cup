using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Resources/Ramp: Beach Buggy's kicker. A wedge 5 m wide and 7 m long rising to a 1 m lip, striped across in the
/// barriers' red and white (unlit, so it reads in the dark), with its own collider: the wheels' ground casts take any
/// Default-layer collider as road. Ramp.cs lays one or two on the straights when a solo race starts.
///     tools/unity.sh RampSetup.Build
public static class RampSetup
{
    const float W = 5f, L = 7f, H = 1f;
    const int Strips = 7;

    [MenuItem("Build/Ramp")]
    public static void Build()
    {
        var white = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Barrier.mat");
        var red = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/BarrierRed.mat");
        var mesh = Wedge();
        const string meshPath = "Assets/_Game/Art/Models/Ramp.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var root = new GameObject("Ramp");
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = root.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { white, red };
        root.AddComponent<MeshCollider>().sharedMesh = mesh;
        var lip = root.AddComponent<BoxCollider>();                     // Ramp.cs kicks whatever drives through it upward
        lip.isTrigger = true; lip.center = new Vector3(0, H + 1f, L / 2f - 0.8f); lip.size = new Vector3(W, 2f, 1.6f);
        root.AddComponent<KartCup.Ramp>();
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Resources/Ramp.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[items] ramp prefab written (materials {(white != null && red != null ? "set" : "MISSING")})");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// Along +Z from the foot (z = -L/2, flush with the road) to the lip (z = +L/2, H up). Submesh 0 white, 1 red:
    /// the slope in alternating strips, the sides white, the drop behind the lip red.
    static Mesh Wedge()
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var white = new List<int>(); var red = new List<int>();
        float Y(float z) => (z + L / 2f) / L * H;
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> into)   // a b c d round the face, front toward its normal
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;   // from the first three: on a side, d is a again
            int k = v.Count; v.AddRange(new[] { a, b, c, d }); n.AddRange(new[] { normal, normal, normal, normal });
            into.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
        }
        for (int s = 0; s < Strips; s++)
        {
            float z0 = -L / 2f + L * s / Strips, z1 = -L / 2f + L * (s + 1) / Strips;
            Quad(new Vector3(-W / 2f, Y(z0), z0), new Vector3(-W / 2f, Y(z1), z1), new Vector3(W / 2f, Y(z1), z1), new Vector3(W / 2f, Y(z0), z0), s % 2 == 0 ? white : red);
        }
        float zb = -L / 2f, zf = L / 2f;
        // the sides: a triangle each, written as a quad with two corners together
        Quad(new Vector3(-W / 2f, 0, zb), new Vector3(-W / 2f, 0, zf), new Vector3(-W / 2f, H, zf), new Vector3(-W / 2f, 0, zb), white);
        Quad(new Vector3(W / 2f, 0, zb), new Vector3(W / 2f, H, zf), new Vector3(W / 2f, 0, zf), new Vector3(W / 2f, 0, zb), white);
        Quad(new Vector3(-W / 2f, 0, zf), new Vector3(W / 2f, 0, zf), new Vector3(W / 2f, H, zf), new Vector3(-W / 2f, H, zf), red);
        var m = new Mesh { name = "Ramp" };
        m.SetVertices(v); m.SetNormals(n);
        m.subMeshCount = 2; m.SetTriangles(white, 0); m.SetTriangles(red, 1);
        m.RecalculateBounds();
        return m;
    }
}
