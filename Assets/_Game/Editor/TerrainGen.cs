using System.Collections.Generic;
using System.IO;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEngine;

// A low-poly heightfield under the circuit, flattened to the road so nothing pokes through it. Flat-shaded
// (unique vertices per face, face normals, one colour per face) because a smooth-shaded 8 m grid reads as a
// rubber sheet; faceted terrain is what makes the Kenney props and the terrain look like one world.
public static class TerrainGen
{
    public const int Cells = 128;           // 128 x 8 m = 1024 m across, 32k faces: within the 45k budget
    public const float Cell = 8f;
    const int ChunkCells = 32;              // 4x4 chunks so the frustum culls the far side of the map
    public const float Half = Cells * Cell * 0.5f;

    /// What a palette gets to decide a face's colour.
    public struct Face { public Vector3 p; public float slope, road, jitter; }   // centroid, slope in degrees, distance to the centreline, per-face hash 0..1
    public delegate Color Palette(Face f);

    static readonly float[,] h = new float[Cells + 1, Cells + 1];
    static readonly float[,] roadDist = new float[Cells + 1, Cells + 1];
    public static float MaxGrade, MaxKink;                                   // logged per map

    /// Ground height anywhere, from the flattened grid — props sit on this.
    public static float Height(float x, float z)
    {
        float fx = Mathf.Clamp((x + Half) / Cell, 0, Cells - 0.001f), fz = Mathf.Clamp((z + Half) / Cell, 0, Cells - 0.001f);
        int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz;
        return Mathf.Lerp(Mathf.Lerp(h[ix, iz], h[ix + 1, iz], tx), Mathf.Lerp(h[ix, iz + 1], h[ix + 1, iz + 1], tx), tz);
    }

    /// Road profile: the terrain sampled under the centreline, then smoothed until a kart can take every crest
    /// flat out. Writes centre[i].y, so everything derived from the centreline inherits the elevation.
    public static void Profile(Vector3[] centre, System.Func<float, float, float> shape, float lapLength)
    {
        int n = centre.Length;
        float ds = lapLength / n;
        var y = new float[n];
        for (int i = 0; i < n; i++) y[i] = shape(centre[i].x, centre[i].z);

        // Box blur ±40 m, repeated. Each pass lowers the grade and the crest curvature; the limits are where a kart
        // at 30 m/s stays planted: 9 % grade, and a grade change per sample that keeps the crest radius > 250 m.
        int w = Mathf.Max(1, Mathf.RoundToInt(40f / ds));
        for (int pass = 0; pass < 60; pass++)
        {
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int k = -w; k <= w; k++) sum += y[((i + k) % n + n) % n];
                s[i] = sum / (2 * w + 1);
            }
            y = s;
            MaxGrade = 0; MaxKink = 0;
            for (int i = 0; i < n; i++)
            {
                float g0 = (y[i] - y[(i - 1 + n) % n]) / ds, g1 = (y[(i + 1) % n] - y[i]) / ds;
                MaxGrade = Mathf.Max(MaxGrade, Mathf.Abs(g1));
                MaxKink = Mathf.Max(MaxKink, Mathf.Abs(g1 - g0));
            }
            if (MaxGrade <= 0.09f && MaxKink <= 0.012f) break;
        }
        // KartProgress calls y < -8 "lost": keep the road comfortably above the sea and any basin.
        float min = Mathf.Min(y);
        float lift = Mathf.Max(0f, 0.5f - min);
        for (int i = 0; i < n; i++) centre[i].y = y[i] + lift;
    }

    /// Builds the heightfield as 4x4 chunk meshes with colliders. `roadHalf` is the half width that must be level
    /// with the road (road + kerb + run-off); beyond it the terrain blends back to the theme's shape.
    public static Transform Build(Vector3[] centre, System.Func<float, float, float> shape, float roadHalf,
                                  Palette palette, Material mat, GroundSurfacePreset surface, string dir, out int tris)
    {
        int n = centre.Length;
        for (int ix = 0; ix <= Cells; ix++)
            for (int iz = 0; iz <= Cells; iz++)
            {
                float x = ix * Cell - Half, z = iz * Cell - Half;
                float terrain = shape(x, z);

                // Nearest centreline sample by brute force (16k nodes x 384 samples is nothing in the editor), then the
                // exact distance to the two segments around it so the road band has no scalloping between samples.
                int best = 0; float bestSq = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float dx = centre[i].x - x, dz = centre[i].z - z, sq = dx * dx + dz * dz;
                    if (sq < bestSq) { bestSq = sq; best = i; }
                }
                float d = float.MaxValue, roadY = centre[best].y;
                for (int s = best - 1; s <= best; s++)
                {
                    Vector3 a = centre[(s + n) % n], b = centre[(s + 1) % n];
                    float abx = b.x - a.x, abz = b.z - a.z, len = abx * abx + abz * abz;
                    float t = len > 0.001f ? Mathf.Clamp01(((x - a.x) * abx + (z - a.z) * abz) / len) : 0f;
                    float qx = a.x + abx * t, qz = a.z + abz * t;
                    float dist = Mathf.Sqrt((x - qx) * (x - qx) + (z - qz) * (z - qz));
                    if (dist < d) { d = dist; roadY = Mathf.Lerp(a.y, b.y, t); }
                }
                roadDist[ix, iz] = d;

                // Level with the road (5 cm under it, so the flat strips never z-fight the terrain's coarser
                // linearisation on a crest), then a bank out to free terrain. The bank widens with the height it has
                // to cover so a cutting through a hill is a slope, not a wall.
                float drop = Mathf.Abs(terrain - roadY);
                float band = 16f + Mathf.Clamp(drop * 0.8f, 0f, 30f);
                float t2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - roadHalf) / band));
                h[ix, iz] = Mathf.Lerp(roadY - 0.05f, terrain, t2);
            }

        Directory.CreateDirectory(dir);
        var root = new GameObject("Terrain").transform;
        tris = 0;
        int chunks = Cells / ChunkCells;
        for (int cx = 0; cx < chunks; cx++)
            for (int cz = 0; cz < chunks; cz++)
                tris += Chunk(root, cx, cz, palette, mat, surface, dir);
        return root;
    }

    static int Chunk(Transform root, int cx, int cz, Palette palette, Material mat, GroundSurfacePreset surface, string dir)
    {
        var verts = new List<Vector3>(ChunkCells * ChunkCells * 6);
        var norms = new List<Vector3>(verts.Capacity);
        var cols = new List<Color>(verts.Capacity);
        var index = new List<int>(verts.Capacity);

        for (int ix = cx * ChunkCells; ix < (cx + 1) * ChunkCells; ix++)
            for (int iz = cz * ChunkCells; iz < (cz + 1) * ChunkCells; iz++)
            {
                Vector3 a = Node(ix, iz), b = Node(ix + 1, iz), c = Node(ix, iz + 1), d = Node(ix + 1, iz + 1);
                // Alternate the split diagonal per cell: one fixed diagonal draws visible stripes across every slope.
                bool flip = ((ix + iz) & 1) == 0;
                float rd = (roadDist[ix, iz] + roadDist[ix + 1, iz] + roadDist[ix, iz + 1] + roadDist[ix + 1, iz + 1]) * 0.25f;
                if (flip) { Tri(a, c, d, rd); Tri(a, d, b, rd); }
                else      { Tri(a, c, b, rd); Tri(b, c, d, rd); }
            }

        void Tri(Vector3 p0, Vector3 p1, Vector3 p2, float rd)
        {
            var normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;    // wound so the face points up
            var centroid = (p0 + p1 + p2) / 3f;
            uint hash = (uint)(centroid.x * 7.13f + centroid.z * 3.71f + centroid.y * 11.9f) * 2654435761u;
            var face = new Face { p = centroid, slope = Vector3.Angle(normal, Vector3.up), road = rd, jitter = (hash >> 8) / 16777216f };
            // Palettes are authored as sRGB (what you see); the project renders in linear and Unity does not
            // colour-manage vertex colours, so without this every face came out a pastel ~1.5x too light.
            var colour = palette(face).linear;
            int v = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            for (int k = 0; k < 3; k++) { norms.Add(normal); cols.Add(colour); index.Add(v + k); }
        }

        var mesh = new Mesh { name = $"Terrain_{cx}{cz}" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetColors(cols);
        mesh.SetTriangles(index, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, $"{dir}/{mesh.name}.asset");

        var go = new GameObject(mesh.name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<GroundSurface>().props = surface;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        return index.Count / 3;
    }

    static Vector3 Node(int ix, int iz) => new(ix * Cell - Half, h[ix, iz], iz * Cell - Half);

    // ---------- noise for the theme shapes ----------
    /// Perlin octaves in -1..1. Offset away from the integer lattice at the origin, where PerlinNoise is flat.
    public static float Fbm(float x, float z, float freq, int octaves, float seed = 0f)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * (Mathf.PerlinNoise(x * freq + 517.3f + seed, z * freq + 231.7f + seed * 1.7f) * 2f - 1f);
            norm += amp; amp *= 0.5f; freq *= 2.1f;
        }
        return sum / norm;
    }

    /// 0..1 with sharp crests: mountain ridges rather than rolling hills.
    public static float Ridged(float x, float z, float freq, float seed = 0f) => 1f - Mathf.Abs(Fbm(x, z, freq, 2, seed));

    public static float Step(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));
}
