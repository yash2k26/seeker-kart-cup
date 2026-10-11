using System.Collections.Generic;
using KartCup;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEngine;

// Beach Buggy's shortcuts (research/reports "Beach Buggy career and track design"): every BBR1 track but one hides a
// path off the road, and the lesson from players is that one only earns its place if it pays reliably on a clean line
// (a second or so), is shown in the intro flythrough, and the AI takes it only once the field is good.
//
// A shortcut here is a dirt track cut straight across the inside of the tightest bend there is, from the road before
// it to the road after it. The plan is measured, not drawn: every span of the lap is tried, and one is kept only if
//   - it cuts a real corner (its middle stands clear of the kerb) and pays off on dirt at the field's speed;
//   - the Worker's referee (worker/src/referee.ts) would still believe the lap: it stays inside the corridor the
//     referee allows past the kerb, it crosses every gate it skips round inside that gate's box, and the lap progress
//     it gains is under the referee's per-second cap. A shortcut that voided a Kartel Weekly lap would be worse than none;
//   - it never comes near another leg of the circuit, the start, or the tunnel, and its grade is drivable.
// A circuit with no such bend (Palm Bay's sweepers, like Beach Buggy's Crab Cove) gets none.
public static partial class TrackSetup
{
    struct Cut { public int a, b; public Vector3[] path; public float gain, road, chord, maxOff, midOff; }
    static Cut? shortcut;
    const float CutHalf = 4.5f;                       // the dirt track's half width: one kart and a bit, not a second road
    const float DirtSpeed = 0.9f;                      // Powerslide's Dirt surface, which the track wears
    const float ReferenceSpeed = 40f;                  // the field's top speed, m/s, for the time a cut saves
    const float MaxTurn = 46f;                         // at either mouth; ShortcutPath.MaxTurnIn keeps the AI off anything sharper

    /// Every circuit's best cut, measured and logged without building a scene:  tools/unity.sh TrackSetup.ShortcutProbe
    public static void ShortcutProbe()
    {
        for (int i = 0; i < Tracks.Count; i++)
        {
            V = System.Array.Find(Themes, t => t.id == Tracks.At(i).id); Index = i;
            Sample(); Rise(); Analyse();
            PlanShortcut(log: 3);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void PlanShortcut(int log = 1)
    {
        shortcut = null;
        PlanTunnel();
        int n = centre.Length;
        float m = Length() / n, edge = HalfWidth + Kerb;
        float gateHalf = HalfWidth + Kerb + GateSlack;
        int gates = GateCount;
        var found = new List<Cut>();
        // the road's own time through each sample: flat out on the straights, down to 0.6 of it in the tightest corner
        // (KartAi's corner model), so a cut is credited with the corner it skips and not just the metres
        var pace = new float[n];
        for (int i = 0; i < n; i++) pace[i] = ReferenceSpeed * Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(8f, 55f, Mathf.Abs(Turn(i, 5)) * 1.6f));

        int lo = Samples(70f), hi = n - Samples(60f), kMin = Samples(45f), kMax = Samples(200f), end = n - Samples(45f), pad = Samples(30f);
        for (int a = lo; a < hi; a += 2)
            for (int k = kMin; k <= kMax && a + k < end; k += 2)
            {
                int b = a + k;
                bool tunnel = false; for (int i = a - Samples(25f); i <= b + Samples(25f) && !tunnel; i++) tunnel = InTunnel(Wrap(i, n));
                if (tunnel) continue;
                Vector3 pa = centre[a], pb = centre[b];
                var flat = new Vector3(pb.x - pa.x, 0, pb.z - pa.z);
                float chord = flat.magnitude;
                if (chord < 25f || Mathf.Abs(pb.y - pa.y) / chord > 0.12f) continue;
                // drivable both ways round: the turn off the road into it, and onto the road out of it (the way in when the
                // circuit runs reversed), at most MaxTurn. Pine Ridge's first plan opened at 73 and 110 degrees, and the
                // field spun off the hairpin it made.
                Vector3 fa = Forward(a), fb = Forward(b); fa.y = 0f; fb.y = 0f;
                if (Vector3.Angle(fa, flat) > MaxTurn || Vector3.Angle(fb, flat) > MaxTurn) continue;
                float road = 0f, roadTime = 0f;
                for (int i = a; i < b; i++) { float s = Vector3.Distance(centre[i], centre[i + 1]); road += s; roadTime += s / pace[i]; }
                float gain = roadTime - chord / (ReferenceSpeed * DirtSpeed);
                if (gain < 0.6f) continue;
                // the referee's progress cap: over the cut, lap progress (the road's metres) per second on the dirt
                if (road / (chord / (ReferenceSpeed * DirtSpeed)) > 80f) continue;

                // along the chord: how far off the road it runs, and whether it nears any other leg of the lap
                float maxOff = 0f, midOff = 0f; bool ok = true;
                int steps = Mathf.CeilToInt(chord / 3f);
                for (int s = 1; s < steps && ok; s++)
                {
                    float u = s / (float)steps;
                    var p = Vector3.Lerp(pa, pb, u);
                    float near = float.MaxValue, other = float.MaxValue;
                    for (int i = Mathf.Max(0, a - pad); i <= Mathf.Min(n - 1, b + pad); i++) near = Mathf.Min(near, Flat2(centre[i] - p));
                    for (int i = 0; i < n; i += 2) if (i < a - pad || i > b + pad) other = Mathf.Min(other, Flat2(centre[i] - p));
                    near = Mathf.Sqrt(near); other = Mathf.Sqrt(other);
                    if (near > edge + 21f) ok = false;
                    maxOff = Mathf.Max(maxOff, near);
                    if (u > 0.3f && u < 0.7f) midOff = Mathf.Max(midOff, near);
                    if (other < edge * 2f + 12f) ok = false;                   // another leg of the circuit: not a cut, a crossing
                }
                if (!ok || maxOff > edge + 21f || midOff < edge + CutHalf + 4f) continue;   // the referee's corridor is 25 m past the kerb

                // every gate the road takes between a and b must be crossed by the cut inside its box (with margin)
                for (int g = 1; g < gates && ok; g++)
                {
                    int gs = g * n / gates;
                    if (gs <= a || gs >= b) continue;
                    var gp = centre[gs]; var gr = right[gs];
                    // where the chord crosses the gate's line
                    float best = float.MaxValue;
                    for (int s = 0; s <= steps; s++)
                    {
                        var p = Vector3.Lerp(pa, pb, s / (float)steps);
                        float along = Mathf.Abs(Vector3.Dot(new Vector3(p.x - gp.x, 0, p.z - gp.z), Forward(gs)));
                        if (along < 2f) best = Mathf.Min(best, Mathf.Abs(Vector3.Dot(new Vector3(p.x - gp.x, 0, p.z - gp.z), gr)));
                    }
                    if (best > gateHalf - 3f) ok = false;
                }
                if (!ok) continue;
                found.Add(new Cut { a = a, b = b, gain = gain, road = road, chord = chord, maxOff = maxOff, midOff = midOff });
            }

        // the best pays, but not too well: Beach Buggy's "mild reward for a massive risk" on one side, a lap that is all
        // shortcut on the other. Ties to the one nearer 1.5 s.
        found.Sort((x, y) => Score(y).CompareTo(Score(x)));
        static float Score(Cut c) => -Mathf.Abs(Mathf.Min(c.gain, 2.4f) - 1.6f) + Mathf.Min(c.gain, 2.4f) * 0.2f;
        for (int i = 0; i < Mathf.Min(log, found.Count); i++)
        {
            var c = found[i];
            Debug.Log($"[track] {V.id} cut {i}: samples {c.a}-{c.b} ({c.a * m:0}-{c.b * m:0} m), road {c.road:0} m vs dirt {c.chord:0} m, saves ~{c.gain:0.0} s, " +
                      $"off the line {c.midOff:0}-{c.maxOff:0} m (kerb at {edge:0.0}), grade {(centre[c.b].y - centre[c.a].y) / c.chord * 100f:0}%");
        }
        if (found.Count == 0) { Debug.Log($"[track] {V.id}: no bend worth cutting"); return; }
        var cut = found[0];
        int count = Mathf.CeilToInt(cut.chord / 4f);
        cut.path = new Vector3[count + 1];
        for (int s = 0; s <= count; s++)
        {
            float u = s / (float)count;
            var p = Vector3.Lerp(centre[cut.a], centre[cut.b], u);
            p.y = Mathf.Lerp(centre[cut.a].y, centre[cut.b].y, Mathf.SmoothStep(0f, 1f, u));   // eases off the road and onto it
            cut.path[s] = p;
        }
        shortcut = cut;
    }

    static float Flat2(Vector3 d) => d.x * d.x + d.z * d.z;

    // ---------- building it ----------

    static bool keepOnCut;   // the gateway, and the crates there to be hit, may stand on the dirt
    static Material terrainMat;   // Environment's: the ribbon is lit the way the ground round it is

    /// Whether `p` (in XZ) is within `pad` metres of the dirt track's edge.
    static bool OnCut(Vector3 p, float pad)
    {
        if (shortcut == null) return false;
        var path = shortcut.Value.path;
        for (int i = 0; i < path.Length - 1; i++) if (SegmentDistance(p, path[i], path[i + 1]) < CutHalf + pad) return true;
        return false;
    }

    /// TrackDressing.Prop asks: a prop that would stand on the dirt is not placed.
    static bool Clears(GameObject go)
    {
        if (keepOnCut || shortcut == null) return false;
        var b = Bounds(go);
        return OnCut(b.center, Mathf.Max(b.extents.x, b.extents.z) + 1f);
    }

    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        float abx = b.x - a.x, abz = b.z - a.z, len = abx * abx + abz * abz;
        float t = len > 1e-4f ? Mathf.Clamp01(((p.x - a.x) * abx + (p.z - a.z) * abz) / len) : 0f;
        float dx = p.x - (a.x + abx * t), dz = p.z - (a.z + abz * t);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// The dirt track, its waypoints and a banner gateway at each mouth. After the road and the circuit's waypoints,
    /// before the dressing (which keeps off it).
    static void BuildShortcut()
    {
        if (shortcut == null) return;
        var cut = shortcut.Value;
        var root = new GameObject("Shortcut").transform;
        int n = centre.Length;
        float edge = HalfWidth + Kerb;

        // The ribbon: every 2 m along the chord, five columns across (edge, rut, middle, rut, edge), each vertex on the
        // terrain's own triangles under it (a raycast, so it never cuts through a facet) and a few centimetres up; where
        // it crosses into the road it drops under the asphalt, so the road draws over the join and there is no lip. It
        // wears the terrain's own material (its lighting, its wrap), coloured by vertex: worn ruts, a softer middle, and
        // edges that fade toward the ground around it, so it reads as a track worn into the place, not a strip laid on it.
        Physics.SyncTransforms();
        float len = Vector3.Distance(Flat(cut.path[0]), Flat(cut.path[^1]));
        int steps = Mathf.CeilToInt(len / 2f);
        var dir = (Flat(cut.path[^1]) - Flat(cut.path[0])).normalized;
        var side = Vector3.Cross(Vector3.up, dir);
        var (rut, middle, rim) = V.id switch
        {
            "alpine" => (new Color(0.58f, 0.62f, 0.74f), new Color(0.74f, 0.78f, 0.88f), new Color(0.84f, 0.87f, 0.94f)),   // packed snow
            _ => (new Color(0.33f, 0.24f, 0.16f), new Color(0.47f, 0.36f, 0.24f), new Color(0.42f, 0.40f, 0.24f)),          // earth into the grass
        };
        float[] across = { -1f, -0.42f, 0f, 0.42f, 1f };
        Color[] tone = { rim, rut, middle, rut, rim };
        var verts = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        float RoadDistance(Vector3 p) { float best = float.MaxValue; for (int i = Mathf.Max(0, cut.a - Samples(30f)); i <= Mathf.Min(n - 1, cut.b + Samples(30f)); i++) best = Mathf.Min(best, Flat2(centre[i] - p)); return Mathf.Sqrt(best); }
        float Ground(Vector3 p)
        {
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 400f, p.z), Vector3.down, 800f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.root.name == "Terrain") return hit.point.y;
            return TerrainGen.Height(p.x, p.z);
        }
        for (int s = 0; s <= steps; s++)
        {
            float u = s / (float)steps;
            var mid = Vector3.Lerp(cut.path[0], cut.path[^1], u);
            for (int c = 0; c < across.Length; c++)
            {
                var p = mid + side * (across[c] * CutHalf);
                p.y = Ground(p) + Mathf.Lerp(-0.07f, 0.06f, Mathf.InverseLerp(HalfWidth - 0.5f, HalfWidth + 1f, RoadDistance(p)));
                verts.Add(p); cols.Add(tone[c].linear); uvs.Add(new Vector2((across[c] + 1f) * 0.5f, u * len / 7f));
            }
            if (s == 0) continue;
            int v = (s - 1) * across.Length, w = s * across.Length;
            for (int c = 0; c < across.Length - 1; c++) tris.AddRange(new[] { v + c, w + c, v + c + 1, v + c + 1, w + c, w + c + 1 });
        }
        var mesh = new Mesh { name = "Shortcut" };
        mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (Vector3.Dot(mesh.normals[0], Vector3.up) < 0f) { for (int t = 0; t < tris.Count; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}/Shortcut.asset");
        var ribbon = new GameObject("Dirt");
        ribbon.transform.SetParent(root, false);
        ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
        ribbon.AddComponent<MeshRenderer>().sharedMaterial = terrainMat;
        ribbon.AddComponent<MeshCollider>().sharedMesh = mesh;
        ribbon.AddComponent<GroundSurface>().props = CutSurface();
        GameObjectUtility.SetStaticEditorFlags(ribbon, StaticEditorFlags.BatchingStatic);

        // its waypoints, every ~12 m from a few metres in, and the circuit's either side of it
        var main = GameObject.Find("Waypoints").transform;
        int every = Mathf.Max(1, Mathf.RoundToInt(n / 48f)), count = main.childCount;
        var from = main.GetChild(Mathf.Clamp(cut.a / every, 0, count - 1)).GetComponent<BasicWaypoint>();
        var to = main.GetChild(Mathf.CeilToInt(cut.b / (float)every) % count).GetComponent<BasicWaypoint>();
        var path = new List<BasicWaypoint>();
        int points = Mathf.Max(2, Mathf.RoundToInt((len - 12f) / 12f) + 1);
        for (int k = 0; k < points; k++)
        {
            float u = Mathf.Lerp(6f, len - 6f, k / (float)(points - 1)) / len;
            var p = Vector3.Lerp(cut.path[0], cut.path[^1], u);
            var go = new GameObject($"Cut{k:00}");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3(p.x, TerrainGen.Height(p.x, p.z) + 0.5f, p.z), Quaternion.LookRotation(dir, Vector3.up));
            var wp = go.AddComponent<BasicWaypoint>(); wp.radius = 7f;
            path.Add(wp);
        }
        for (int k = 0; k < path.Count; k++) path[k].nextPoint = k + 1 < path.Count ? path[k + 1] : to;
        var sc = root.gameObject.AddComponent<ShortcutPath>();
        sc.from = from; sc.to = to; sc.path = path.ToArray();

        // a banner gateway at each mouth, where the dirt leaves the kerb: what the intro's drone flies through
        keepOnCut = true;
        foreach (float end in new[] { 0f, 1f })
        {
            float u = end;
            for (int k = 0; k <= steps; k++)
            {
                float t = end == 0f ? k / (float)steps : 1f - k / (float)steps;
                if (RoadDistance(Vector3.Lerp(cut.path[0], cut.path[^1], t)) > edge + 3f) { u = t; break; }
            }
            var mouth = Vector3.Lerp(cut.path[0], cut.path[^1], u);
            var facing = end == 0f ? -dir : dir;
            for (int e = -1; e <= 1; e += 2)
            {
                var at = mouth + side * (e * (CutHalf + 1.4f));
                Prop(root, "Racing", "bannerTowerGreen", At(at.x, at.z, 0f), Yaw(facing), 6.5f);
            }
        }
        keepOnCut = false;
        Debug.Log($"[track] shortcut: {len:0} m of dirt from waypoint {from.name} to {to.name}, {path.Count} waypoints, saves ~{cut.gain:0.0} s");
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    /// Powerslide's Dirt, at 0.9 of the road's speed, but with its grip: Dirt always slides, and the turn into a cut is
    /// half a corner's worth of steering, so on it the AI spun into the grass beside the track (Pine Ridge, reversed).
    static GroundSurfacePreset CutSurface()
    {
        const string path = "Assets/_Game/Data/Shortcut.asset";
        var dirt = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(PKP + "Prefabs/GroundSurfacePresets/Dirt.asset");
        var cut = AssetDatabase.LoadAssetAtPath<GroundSurfacePreset>(path);
        if (cut == null) { cut = Object.Instantiate(dirt); AssetDatabase.CreateAsset(cut, path); }
        cut.speed = DirtSpeed; cut.friction = 0.9f; cut.alwaysSlide = false;
        cut.tireMarkMaterial = dirt.tireMarkMaterial; cut.tireSnd = dirt.tireSnd;
        EditorUtility.SetDirty(cut);
        return cut;
    }
}
