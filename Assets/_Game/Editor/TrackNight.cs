using System.Collections.Generic;
using System.IO;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

// Palm Bay after dark: the road furniture and the landmark that make the coast read like the teaser. Sodium lamp
// posts with real spot lights, cyan road studs and a red-and-white banded lighthouse with a turning beam, all
// generated here (proportions from the film's own props, teaser/kit/world/props.py) so nothing is a kit stand-in.
// Everything keys off the theme (TrackThemes.cs): lampEvery, studs, lighthouse.
public static partial class TrackSetup
{
    static readonly Color Sodium = new(1f, 0.62f, 0.28f), Cyan = new(0.10f, 0.75f, 1f), LampWarm = new(1f, 0.9f, 0.72f);
    static Shader SimpleLit => Shader.Find("Universal Render Pipeline/Simple Lit");

    /// An unlit material whose colour sits past 1, so the bloom lifts it: lamp glass, studs, lenses, the beacon.
    static Material Emissive(string name, Color colour, float strength)
    {
        var m = LoadOrCreate(Art + $"Materials/{name}.mat", Shader.Find("Universal Render Pipeline/Unlit"), Color.white);
        m.SetColor("_BaseColor", colour * strength);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Plain(string name, Color colour)
    {
        var m = LoadOrCreate(Art + $"Materials/{name}.mat", SimpleLit, colour);
        m.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localRotation = rot ?? Quaternion.identity; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ---------- sodium lamps ----------

    /// A lamp every V.lampEvery metres: on the land side down the straights, on the outside through the corners
    /// (both sides through a tight one), each a real spot light. The road and kerbs are built in chunks because a
    /// phone lights a renderer by at most eight lights; the lights themselves cast no shadow.
    static readonly List<Vector3> lampHeads = new();

    static void SodiumLamps(Transform root)
    {
        if (V.lampEvery <= 0f) return;
        lampHeads.Clear();
        var pole = Plain("Lamp_Pole", new Color(0.16f, 0.17f, 0.19f));
        var glow = Emissive("Lamp_Glow", Sodium, 3.2f);
        int n = centre.Length, step = Mathf.Max(1, Samples(V.lampEvery)), count = 0;
        for (int i = Samples(9f); i < n; i += step)
        {
            if (InTunnel(i)) continue;
            var c = CornerAt(i);
            float side = c.HasValue ? c.Value.outside : 1f;      // `right` points into the island: +1 is the land side
            Lamp(root, i, side, pole, glow, ref count);
        }
        Glows(root, "LampGlows", lampHeads, Sodium);
        Debug.Log($"[track] {count} sodium lamps");
    }

    static void Lamp(Transform root, int i, float side, Material pole, Material glow, ref int count)
    {
        const float H = 8f, Arm = 2.4f;
        var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var lamp = new GameObject($"Lamp{count++:00}").transform;
        lamp.SetParent(root, false);
        lamp.SetPositionAndRotation(Beside(i, side, HalfWidth + Kerb + 2.4f), Quaternion.LookRotation(-right[i] * side, Vector3.up));   // +Z toward the road
        Part(lamp, "Pole", cylinder, pole, new Vector3(0, H / 2f, 0), new Vector3(0.22f, H / 2f, 0.22f));   // the builtin cylinder is 2 tall
        Part(lamp, "Arm", cube, pole, new Vector3(0, H - 0.1f, Arm / 2f), new Vector3(0.16f, 0.16f, Arm));
        Part(lamp, "Head", cube, pole, new Vector3(0, H - 0.2f, Arm + 0.1f), new Vector3(0.42f, 0.2f, 0.75f));
        Part(lamp, "Glass", quad, glow, new Vector3(0, H - 0.31f, Arm + 0.1f), new Vector3(0.36f, 0.66f, 1f), Quaternion.Euler(-90, 0, 0));   // a quad faces -Z: turned to face down
        var light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(lamp, false);
        light.transform.localPosition = new Vector3(0, H - 0.35f, Arm + 0.1f);
        light.transform.localRotation = Quaternion.Euler(90, 0, 0);
        lampHeads.Add(light.transform.position);
        light.type = LightType.Spot; light.color = Sodium; light.intensity = 340f; light.range = 40f;
        light.spotAngle = 112f; light.innerSpotAngle = 40f; light.shadows = LightShadows.None;   // wide: the pool reaches well onto the road
        var capsule = lamp.gameObject.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0, H / 2f, 0); capsule.height = H; capsule.radius = 0.16f;
        GameObjectUtility.SetStaticEditorFlags(lamp.gameObject, StaticEditorFlags.BatchingStatic);
        props++;
    }

    // ---------- road studs ----------

    /// Cyan studs along both edge lines, every 7 m and every other sample through a corner: the road's own light,
    /// the way the film's coast road wears them. One mesh, two materials, no lights.
    static void Studs(Transform root)
    {
        if (!V.studs) return;
        var mb = new MeshBuilder();
        int n = centre.Length, step = Mathf.Max(1, Samples(7f)), count = 0;
        for (int i = 0; i < n; i++)
        {
            bool corner = CornerAt(i).HasValue;
            if (i % step != 0 && !(corner && i % 2 == 0)) continue;
            var f = Forward(i); var r = right[i];
            foreach (float side in new[] { -1f, 1f })
            {
                var p = centre[i] + r * ((HalfWidth - 0.15f) * side) + Vector3.up * 0.04f;
                mb.Box(0, p + Vector3.up * 0.011f, r, Vector3.up, f, new Vector3(0.15f, 0.022f, 0.12f));
                mb.Box(1, p + Vector3.up * 0.034f, r, Vector3.up, f, new Vector3(0.10f, 0.026f, 0.07f));
                count++;
            }
        }
        var go = mb.Finish("Studs", root, new[] { Plain("Stud_Base", new Color(0.12f, 0.12f, 0.13f)), Emissive("Stud_Glow", Cyan, 3f) });
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        Debug.Log($"[track] {count} road studs");
    }

    // ---------- the lighthouse ----------

    /// The film's banded lighthouse (props.lighthouse, k = height / 28) on a rock islet out to sea off the tightest
    /// corner: a beacon in its lantern room and two shafts of light turning through the bay every eight seconds.
    static void Lighthouse(Transform root)
    {
        if (!V.lighthouse) return;
        var tight = Tightest();
        int apex = tight.Count > 0 ? tight[0].apex : 0;
        var dir = new Vector3(centre[apex].x, 0, centre[apex].z).normalized;    // straight out to sea from the corner
        const float R = 448f;                                                    // past the sand (it ends near 410), inside the buoys
        var site = new Vector3(dir.x * R, 0f, dir.z * R);
        var lh = new GameObject("Lighthouse").transform;
        lh.SetParent(root, false);
        lh.SetPositionAndRotation(site, Quaternion.LookRotation(-dir, Vector3.up));   // local +Z faces the land: the door side

        const float k = 1f;                       // 28 m tall
        var mb = new MeshBuilder();
        const int Stone = 0, Red = 1, White = 2, Trim = 3, Glass = 4, Door = 5;
        // the islet: an octagonal stone plinth from the seabed to just above the tide
        mb.Frustum(Stone, 7.5f * k, 4.1f * k, -18f, 1.3f * k, 8, capTop: true);
        // the tower: six tapered bands, each its own ring so the paint edges stay crisp
        float z0 = 1.3f * k, z1 = 20.2f * k, r0 = 3.3f * k, r1 = 2.35f * k;
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f, b = (i + 1) / 6f;
            mb.Frustum(i % 2 == 0 ? Red : White, Mathf.Lerp(r0, r1, a), Mathf.Lerp(r0, r1, b), Mathf.Lerp(z0, z1, a), Mathf.Lerp(z0, z1, b), 24);
        }
        mb.Frustum(White, r0 + 0.12f * k, r0 + 0.06f * k, z0, z0 + 0.3f * k, 24, capTop: true);                    // lip
        mb.Frustum(White, r1, r1 + 0.9f * k, z1 - 0.9f * k, z1, 24);                                               // corbel
        mb.Frustum(White, r1 + 0.95f * k, r1 + 0.95f * k, z1, z1 + 0.3f * k, 24, capTop: true, capBottom: true);   // gallery deck
        float rg = r1 + 0.85f * k, deck = z1 + 0.3f * k;
        for (int i = 0; i < 20; i++)                                                                                 // railing
        {
            float a = Mathf.PI * 2f * i / 20f;
            mb.Box(Trim, new Vector3(rg * Mathf.Cos(a), deck + 0.525f * k, rg * Mathf.Sin(a)), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.06f, 1.05f, 0.06f) * k);
        }
        mb.Frustum(Trim, rg + 0.03f, rg + 0.03f, deck + 1.0f * k, deck + 1.06f * k, 20);
        mb.Frustum(Trim, rg + 0.03f, rg + 0.03f, deck + 0.5f * k, deck + 0.56f * k, 20);
        float lr = 1.55f * k;                                                                                        // lantern room
        mb.Frustum(Red, lr, lr, deck, deck + 0.9f * k, 12);
        mb.Frustum(Glass, lr * 0.97f, lr * 0.97f, deck + 0.9f * k, deck + 2.8f * k, 12);
        for (int i = 0; i < 12; i++)
        {
            float a = Mathf.PI * 2f * (i + 0.5f) / 12f;
            mb.Box(Trim, new Vector3(lr * Mathf.Cos(a), deck + 1.85f * k, lr * Mathf.Sin(a)), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.08f, 1.9f, 0.08f) * k);
        }
        mb.Frustum(Trim, lr + 0.18f * k, lr + 0.18f * k, deck + 2.8f * k, deck + 3.0f * k, 12, capTop: true, capBottom: true);   // cornice
        float dz = deck + 3.0f * k, pr = lr + 0.1f * k, pz = dz;                                                    // dome
        for (int i = 1; i <= 6; i++)
        {
            float t = i / 6f, rr = (lr + 0.1f * k) * Mathf.Cos(t * Mathf.PI / 2f) + 0.12f * k * t, zz = dz + 1.5f * k * Mathf.Sin(t * Mathf.PI / 2f);
            mb.Frustum(Red, pr, Mathf.Max(rr, 0.05f), pz, zz, 16, capTop: i == 6);
            pr = rr; pz = zz;
        }
        mb.Box(Trim, new Vector3(0, pz + 1.0f * k, 0), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.05f, 1.6f, 0.05f) * k);   // rod
        // door and windows on the land side (+Z), one window lit
        mb.Box(Door, new Vector3(0, z0 + 1.05f * k, r0 - 0.05f * k), Vector3.right, Vector3.up, Vector3.forward, new Vector3(1.1f, 2.1f, 0.3f) * k);
        mb.Box(White, new Vector3(0, z0 + 1.12f * k, r0 - 0.12f * k), Vector3.right, Vector3.up, Vector3.forward, new Vector3(1.35f, 2.35f, 0.25f) * k);
        foreach (var (zf, ang, lit) in new[] { (0.33f, 200f, false), (0.55f, 20f, true), (0.78f, 160f, false) })
        {
            float zz = Mathf.Lerp(z0, z1, zf), rr = Mathf.Lerp(r0, r1, zf) - 0.08f * k, a = ang * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            mb.Box(lit ? Glass : Trim, outward * rr + Vector3.up * zz, Vector3.Cross(Vector3.up, outward), Vector3.up, outward, new Vector3(0.55f, 0.9f, 0.35f) * k);
        }
        var tower = mb.Finish("Lighthouse", lh, new[]
        {
            Plain("LH_Stone", new Color(0.42f, 0.42f, 0.40f)), Plain("LH_Red", new Color(0.62f, 0.05f, 0.04f)), Plain("LH_White", new Color(0.86f, 0.85f, 0.80f)),
            Plain("LH_Trim", new Color(0.05f, 0.055f, 0.06f)), Emissive("LH_Glass", LampWarm, 2.2f), Plain("LH_Door", new Color(0.08f, 0.06f, 0.05f)),
        });
        GameObjectUtility.SetStaticEditorFlags(tower, StaticEditorFlags.BatchingStatic);
        Count(tower);

        // The beacon: a glowing lamp, and on a rotor two cones of light each way (a bright core inside a wide halo).
        float lampY = deck + 1.55f * k;
        Part(lh, "Lamp", Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), Emissive("LH_Lamp", LampWarm, 8f), new Vector3(0, lampY, 0), Vector3.one * 0.84f * k);
        var rotor = new GameObject("Rotor").transform;
        rotor.SetParent(lh, false);
        rotor.localPosition = new Vector3(0, lampY, 0);
        rotor.gameObject.AddComponent<Rotor>().period = 8f;
        var core = SaveMesh(Cone(0.4f * k, 250f, 0.55f), "Beam_Core");
        var halo = SaveMesh(Cone(0.9f * k, 250f, 2.2f), "Beam_Halo");
        var coreMat = BeamMaterial("Beam_Core", 0.7f, 0.12f, 1.6f);
        var haloMat = BeamMaterial("Beam_Halo", 0.08f, 0.2f, 1.2f);
        foreach (float yaw in new[] { 0f, 180f })
        {
            var rot = Quaternion.Euler(1.5f, yaw, 0);      // a touch downward, like the film's
            Part(rotor, "BeamCore", core, coreMat, Vector3.zero, Vector3.one, rot);
            Part(rotor, "BeamHalo", halo, haloMat, Vector3.zero, Vector3.one, rot);
        }
        // floods at the foot on the land side, so the bands read at night
        foreach (float a in new[] { -35f, 35f })
        {
            var pos = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0, Mathf.Cos(a * Mathf.Deg2Rad)) * (r0 + 6f * k) + Vector3.up * 1.5f * k;
            var flood = new GameObject("Flood").AddComponent<Light>();
            flood.transform.SetParent(lh, false);
            flood.transform.localPosition = pos;
            flood.transform.LookAt(lh.TransformPoint(new Vector3(0, 13f * k, 0)));
            flood.type = LightType.Spot; flood.spotAngle = 62f; flood.innerSpotAngle = 30f; flood.range = 48f; flood.intensity = 32f;
            flood.color = new Color(1f, 0.86f, 0.7f); flood.shadows = LightShadows.None;
        }
        // rocks in the surf round the islet
        var rocks = new[] { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        var rnd = new System.Random(V.seed + 7);
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.5f, rr = 5.5f * k + (float)rnd.NextDouble() * 3f;
            Prop(root, "Pirate", rocks[rnd.Next(rocks.Length)], site + new Vector3(Mathf.Sin(a) * rr, -0.8f, Mathf.Cos(a) * rr), (float)rnd.NextDouble() * 360f, 2.5f + (float)rnd.NextDouble() * 3f);
        }
        Debug.Log($"[track] lighthouse {Vector3.Distance(site, centre[apex]):F0} m off the corner at sample {apex}");
    }

    // ---------- the tunnel ----------
    static int tunnelStart = -1, tunnelCount;
    const float TunnelLength = 96f;

    /// The straight that carries the tunnel: the longest run of road clear of the start area, if it is long enough.
    static void PlanTunnel()
    {
        tunnelStart = -1;
        if (!V.tunnel) return;
        int n = centre.Length, need = Samples(TunnelLength) + 6, best = 0, bestStart = -1, run = 0;
        for (int i = 0; i < n * 2; i++)
        {
            int s = i % n;
            bool clear = !CornerAt(s).HasValue && Mathf.Min(s, n - s) > Samples(70f);   // the gantry and the stands own the start
            if (!clear) { run = 0; continue; }
            run++;
            if (run > best) { best = run; bestStart = Wrap(i - run + 1, n); }
        }
        if (best < need) { Debug.Log($"[track] no straight long enough for the tunnel ({best} samples, {need} needed)"); return; }
        tunnelCount = Samples(TunnelLength);
        tunnelStart = Wrap(bestStart + (best - tunnelCount) / 2, n);
    }

    static bool InTunnel(int i) => tunnelStart >= 0 && Wrap(i - tunnelStart, centre.Length) < tunnelCount;

    /// The film's coast tunnel: an arched concrete tube over the straight, sodium light bars down the crown every
    /// twelve metres (real spot lights: eight is what a road piece can take), cyan guidance strips along both walls,
    /// and concrete portals with rock piled over them. The walls stand 2.5 m past the kerbs, so nothing solid is
    /// inside the run-off; whatever the dressing had put in its way is cleared.
    static void Tunnel(Transform root, System.Random rnd)
    {
        if (tunnelStart < 0) return;
        int n = centre.Length;
        float W = HalfWidth + Kerb + 2.5f, WallH = 5.6f, Crown = 3.6f, T = 1.0f;   // half width, straight wall, arch over it, thickness
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var concrete = LoadOrCreate(Art + "Materials/Tunnel_Concrete.mat", lit, Color.white);
        concrete.SetTexture("_BaseMap", Tex("Surfaces/concrete_diff")); concrete.SetTexture("_BumpMap", Tex("Surfaces/concrete_nor"));
        concrete.EnableKeyword("_NORMALMAP"); concrete.SetFloat("_Smoothness", 0.3f); concrete.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.82f));
        concrete.SetTextureScale("_BaseMap", Vector2.one / 2.7f);
        EditorUtility.SetDirty(concrete);
        var rock = Plain("Tunnel_Rock", new Color(0.30f, 0.29f, 0.28f));

        // the profile: vertical walls, then a flattened arch, as (across, up) in metres
        var profile = new List<Vector2> { new(-W, 0f), new(-W, WallH) };
        var outer = new List<Vector2> { new(-W - T, 0f), new(-W - T, WallH) };
        for (int k = 1; k < 12; k++)
        {
            float a = Mathf.PI * (1f - k / 12f);
            profile.Add(new Vector2(Mathf.Cos(a) * W, WallH + Mathf.Sin(a) * Crown));
            outer.Add(new Vector2(Mathf.Cos(a) * (W + T), WallH + Mathf.Sin(a) * (Crown + T)));
        }
        profile.Add(new Vector2(W, WallH)); profile.Add(new Vector2(W, 0f));
        outer.Add(new Vector2(W + T, WallH)); outer.Add(new Vector2(W + T, 0f));
        var along = new float[profile.Count]; for (int k = 1; k < profile.Count; k++) along[k] = along[k - 1] + Vector2.Distance(profile[k], profile[k - 1]);

        var mb = new MeshBuilder();
        const int Inner = 0, Outer = 1;
        Vector3 At(int i, Vector2 p) => centre[i] + right[i] * p.x + Vector3.up * p.y;
        for (int k = 0; k < tunnelCount; k++)
        {
            int i = Wrap(tunnelStart + k, n), j = Wrap(i + 1, n);
            float u0 = arc[i], u1 = arc[i] + Vector3.Distance(centre[i], centre[j]);
            var axisI = centre[i] + Vector3.up * WallH * 0.6f;
            for (int q = 0; q < profile.Count - 1; q++)
            {
                var a = At(i, profile[q]); var b = At(i, profile[q + 1]); var c = At(j, profile[q + 1]); var d = At(j, profile[q]);
                mb.QuadToward(Inner, a, b, c, d, axisI - (a + b + c + d) / 4f, new Vector2(along[q], u0), new Vector2(along[q + 1], u0), new Vector2(along[q + 1], u1), new Vector2(along[q], u1));
                var oa = At(i, outer[q]); var ob = At(i, outer[q + 1]); var oc = At(j, outer[q + 1]); var od = At(j, outer[q]);
                mb.QuadToward(Outer, oa, ob, oc, od, (oa + ob + oc + od) / 4f - axisI);
            }
            // portal faces: the ring between the two profiles at each end
            if (k == 0 || k == tunnelCount - 1)
            {
                int e = k == 0 ? i : j; var toward = Forward(e) * (k == 0 ? -1f : 1f);
                for (int q = 0; q < profile.Count - 1; q++)
                    mb.QuadToward(Outer, At(e, profile[q]), At(e, profile[q + 1]), At(e, outer[q + 1]), At(e, outer[q]), toward);
            }
        }
        var tube = mb.Finish("Tunnel", root, new[] { concrete, rock });
        GameObjectUtility.SetStaticEditorFlags(tube, StaticEditorFlags.BatchingStatic);
        Count(tube);
        // Only the walls collide: a kart never reaches the roof, and TrackProbe's sweep (a kart's height and more) would.
        var walls = new MeshBuilder();
        for (int k = 0; k < tunnelCount; k++)
        {
            int i = Wrap(tunnelStart + k, n), j = Wrap(i + 1, n);
            foreach (float side in new[] { -1f, 1f })
                walls.QuadToward(0, At(i, new Vector2(W * side, 0f)), At(i, new Vector2(W * side, WallH)), At(j, new Vector2(W * side, WallH)), At(j, new Vector2(W * side, 0f)), right[i] * -side);
        }
        var wallGo = walls.Finish("TunnelWalls", root, new Material[0]);
        Object.DestroyImmediate(wallGo.GetComponent<MeshRenderer>());
        wallGo.AddComponent<MeshCollider>().sharedMesh = wallGo.GetComponent<MeshFilter>().sharedMesh;

        // light bars down the crown, and the guidance strips
        var glow = Emissive("Lamp_Glow", Sodium, 3.2f);
        var cyan = Emissive("Stud_Glow", Cyan, 3f);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        int lights = 0;
        for (float m = 6f; m < TunnelLength - 4f; m += 12f)
        {
            int i = Wrap(tunnelStart + Samples(m), n);
            var bar = new GameObject($"TunnelLight{lights++}").transform;
            bar.SetParent(root, false);
            bar.SetPositionAndRotation(centre[i] + Vector3.up * (WallH + Crown - 0.35f), Quaternion.LookRotation(Forward(i), Vector3.up));
            Part(bar, "Glass", quad, glow, Vector3.zero, new Vector3(2.6f, 0.5f, 1f), Quaternion.Euler(-90, 0, 0));
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(bar, false);
            light.transform.localRotation = Quaternion.Euler(90, 0, 0);
            light.type = LightType.Spot; light.color = Sodium; light.intensity = 240f; light.range = 26f;
            light.spotAngle = 145f; light.innerSpotAngle = 60f; light.shadows = LightShadows.None;   // wide, so the walls catch it too
        }
        var strips = new MeshBuilder();
        for (int k = 0; k < tunnelCount; k++)
        {
            int i = Wrap(tunnelStart + k, n), j = Wrap(i + 1, n);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = (W - 0.06f) * side;
                var a = At(i, new Vector2(x, 1.15f)); var b = At(i, new Vector2(x, 1.25f)); var c = At(j, new Vector2(x, 1.25f)); var d = At(j, new Vector2(x, 1.15f));
                strips.QuadToward(0, a, b, c, d, right[i] * -side);
            }
        }
        strips.Finish("TunnelStrips", root, new[] { cyan });

        // clear whatever the dressing had put where the tube now stands
        int cleared = 0;
        foreach (Transform child in root.Cast<Transform>().ToArray())
        {
            if (child.name.StartsWith("Tunnel") || child.name.StartsWith("Studs")) continue;
            var p = child.position; int nearest = 0; float bestSq = float.MaxValue;
            for (int i = 0; i < n; i++) { float sq = (centre[i].x - p.x) * (centre[i].x - p.x) + (centre[i].z - p.z) * (centre[i].z - p.z); if (sq < bestSq) { bestSq = sq; nearest = i; } }
            int k = Wrap(nearest - tunnelStart, n);
            if (k < tunnelCount + 2 && Mathf.Sqrt(bestSq) < W + 9f) { Object.DestroyImmediate(child.gameObject); cleared++; }
        }

        // then bury the tube in rock, so it reads as a cut through a headland: stacks along both flanks, over the
        // crown, and piled highest round the portals
        var rocks = new[] { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        var pile = new GameObject("TunnelRock").transform;
        pile.SetParent(root, false);
        for (int k = 0; k <= tunnelCount; k += 3)
        {
            int i = Wrap(tunnelStart + k, n);
            foreach (float side in new[] { -1f, 1f })
            {
                var p = At(i, new Vector2((W + 3f + (float)rnd.NextDouble() * 4f) * side, 0f));
                Prop(pile, "Pirate", rocks[rnd.Next(rocks.Length)], new Vector3(p.x, -0.6f, p.z), (float)rnd.NextDouble() * 360f, 6f + (float)rnd.NextDouble() * 5f);
            }
            for (int c = 0; c < 2; c++)
            {
                var p = At(i, new Vector2(((float)rnd.NextDouble() - 0.5f) * 1.6f * W, 0f));
                Prop(pile, "Pirate", rocks[rnd.Next(rocks.Length)], new Vector3(p.x, WallH + Crown - 3f, p.z), (float)rnd.NextDouble() * 360f, 5f + (float)rnd.NextDouble() * 5f);
            }
        }
        foreach (int e in new[] { tunnelStart, Wrap(tunnelStart + tunnelCount, n) })
            for (int r = 0; r < 9; r++)
            {
                float t = (r + 0.5f) / 9f;                       // across the portal, left to right
                var p = At(e, new Vector2(Mathf.Lerp(-W - 2f, W + 2f, t), 0f)) + Forward(e) * ((float)rnd.NextDouble() * 6f - 3f);
                bool crown = Mathf.Abs(t - 0.5f) < 0.3f;
                Prop(pile, "Pirate", rocks[rnd.Next(rocks.Length)], new Vector3(p.x, crown ? WallH + Crown - 2f : -0.5f, p.z), (float)rnd.NextDouble() * 360f, 5f + (float)rnd.NextDouble() * 4f + (crown ? 5f : 0f));
            }
        Debug.Log($"[track] tunnel {TunnelLength:F0} m from sample {tunnelStart}, {lights} light bars, {cleared} props cleared from its path");
    }

    static Material BeamMaterial(string name, float strength, float falloff, float edge)
    {
        var shader = Shader.Find("KartCup/Beam") ?? Shader.Find("Universal Render Pipeline/Unlit");
        var m = LoadOrCreate(Art + $"Materials/{name}.mat", shader, Color.white);
        m.shader = shader;
        m.SetColor("_Color", LampWarm); m.SetFloat("_Strength", strength); m.SetFloat("_Falloff", falloff); m.SetFloat("_Edge", edge); m.SetFloat("_Near", 6f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// A hollow cone along +Z, `length` long, opening from radius `r0` by `halfAngle` degrees; uv.y runs 0..1 along it.
    static Mesh Cone(float r0, float length, float halfAngle, int segs = 24)
    {
        float r1 = r0 + length * Mathf.Tan(halfAngle * Mathf.Deg2Rad);
        var v = new Vector3[segs * 2]; var nrm = new Vector3[segs * 2]; var uv = new Vector2[segs * 2]; var tris = new int[segs * 6];
        for (int i = 0; i < segs; i++)
        {
            float a = Mathf.PI * 2f * i / segs; var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            v[i] = radial * r0; v[segs + i] = radial * r1 + Vector3.forward * length;
            nrm[i] = nrm[segs + i] = radial;
            uv[i] = new Vector2(i / (float)segs, 0); uv[segs + i] = new Vector2(i / (float)segs, 1);
            int j = (i + 1) % segs, t = i * 6;
            tris[t] = i; tris[t + 1] = j; tris[t + 2] = segs + j; tris[t + 3] = i; tris[t + 4] = segs + j; tris[t + 5] = segs + i;
        }
        var mesh = new Mesh { name = "Beam", vertices = v, normals = nrm, uv = uv, triangles = tris };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// What the film's haze does round every lamp, without a volume: three additive quads per lamp in ONE mesh (a
    /// star at the glass, a halo, and a wide soft bloom of lit air), opened toward the eye by KartCup/Glow.
    static void Glows(Transform root, string name, List<Vector3> at, Color colour)
    {
        if (at.Count == 0) return;
        var shader = Shader.Find("KartCup/Glow");
        if (shader == null) { Debug.LogWarning("[track] KartCup/Glow missing: no lamp halos"); return; }
        var mat = LoadOrCreate(Art + "Materials/Lamp_Halo.mat", shader, Color.white);
        mat.shader = shader; mat.SetColor("_Color", colour); mat.SetFloat("_Strength", 1f); mat.SetFloat("_Near", 5f);
        EditorUtility.SetDirty(mat);
        // size in metres, falloff power, strength
        (float size, float power, float strength)[] layers = { (30f, 2.0f, 0.26f), (8f, 2.6f, 0.9f), (2.2f, 1.4f, 3.0f) };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
        foreach (var world in at)
        {
            var p = root.InverseTransformPoint(world);
            foreach (var (size, power, strength) in layers)
            {
                int b = v.Count;
                foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                { v.Add(p); uv.Add(c); uv2.Add(new Vector2(size, power)); col.Add(new Color(1, 1, 1, strength)); }
                tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
        }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        var bounds = mesh.bounds; bounds.Expand(30f); mesh.bounds = bounds;   // every vertex sits at a lamp: the quads open in the shader
        var go = Part(root, name, SaveMesh(mesh, name), mat, Vector3.zero, Vector3.one);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        Directory.CreateDirectory(MeshDir);
        mesh.name = name;
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}.asset");
        return mesh;
    }

    /// Flat-shaded rings and boxes into one mesh, a submesh per material. Faces are wound clockwise seen from outside.
    sealed class MeshBuilder
    {
        readonly List<Vector3> v = new(), n = new(); readonly List<Vector2> uv = new(); readonly List<List<int>> subs = new();

        List<int> Sub(int s) { while (subs.Count <= s) subs.Add(new List<int>()); return subs[s]; }

        void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d) => Quad(s, a, b, c, d, new Vector2(0.03f, 0.5f), new Vector2(0.03f, 0.5f), new Vector2(0.03f, 0.5f), new Vector2(0.03f, 0.5f));

        void Quad(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            for (int k = 0; k < 4; k++) n.Add(normal);
            Sub(s).AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        /// A quad wound to face `toward` (a direction from the face), whichever way its corners were listed.
        public void QuadToward(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 toward)
            => QuadToward(s, a, b, c, d, toward, new Vector2(0.03f, 0.5f), new Vector2(0.03f, 0.5f), new Vector2(0.03f, 0.5f), new Vector2(0.03f, 0.5f));

        public void QuadToward(int s, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 toward, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), toward) >= 0f) Quad(s, a, b, c, d, ua, ub, uc, ud);
            else Quad(s, a, d, c, b, ua, ud, uc, ub);
        }

        void Tri(int s, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            for (int k = 0; k < 3; k++) { n.Add(normal); uv.Add(new Vector2(0.03f, 0.5f)); }
            Sub(s).AddRange(new[] { i, i + 1, i + 2 });
        }

        /// A ring wall about the local Y axis from radius r0 at height y0 to r1 at y1.
        public void Frustum(int s, float r0, float r1, float y0, float y1, int sides, bool capTop = false, bool capBottom = false)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides, b = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 p0 = new(r0 * Mathf.Cos(a), y0, r0 * Mathf.Sin(a)), p1 = new(r0 * Mathf.Cos(b), y0, r0 * Mathf.Sin(b));
                Vector3 q0 = new(r1 * Mathf.Cos(a), y1, r1 * Mathf.Sin(a)), q1 = new(r1 * Mathf.Cos(b), y1, r1 * Mathf.Sin(b));
                Quad(s, p0, q0, q1, p1);
                if (capTop) Tri(s, new Vector3(0, y1, 0), q1, q0);
                if (capBottom) Tri(s, new Vector3(0, y0, 0), p0, p1);
            }
        }

        /// A box centred on `c` with unit axes x, y, z and full extents `size`.
        public void Box(int s, Vector3 c, Vector3 x, Vector3 y, Vector3 z, Vector3 size)
        {
            Face(s, c, x, y, size.x * 0.5f, size.z * 0.5f, size.y * 0.5f);
            Face(s, c, -x, y, size.x * 0.5f, size.z * 0.5f, size.y * 0.5f);
            Face(s, c, z, y, size.z * 0.5f, size.x * 0.5f, size.y * 0.5f);
            Face(s, c, -z, y, size.z * 0.5f, size.x * 0.5f, size.y * 0.5f);
            Face(s, c, y, z, size.y * 0.5f, size.x * 0.5f, size.z * 0.5f);
            Face(s, c, -y, z, size.y * 0.5f, size.x * 0.5f, size.z * 0.5f);
        }

        /// One face with outward normal N and up U, its centre extentN out from c, half sizes across and up.
        void Face(int s, Vector3 c, Vector3 N, Vector3 U, float extentN, float halfR, float halfU)
        {
            var R = Vector3.Cross(N, U);
            var f = c + N * extentN;
            Quad(s, f - R * halfR - U * halfU, f - R * halfR + U * halfU, f + R * halfR + U * halfU, f + R * halfR - U * halfU);
        }

        public GameObject Finish(string name, Transform parent, Material[] mats)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = mats.Length;
            for (int s = 0; s < mats.Length; s++) mesh.SetTriangles(s < subs.Count ? subs[s] : new List<int>(), s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();      // for the normal-mapped concrete
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.CreateAsset(mesh, $"{MeshDir}/{name}.asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go;
        }
    }
}
