using System.Collections.Generic;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

// Palm Bay on race night. The coast road had its lamps, its palms and its lighthouse, and a great deal of dark in
// between; this fills that dark with a beach town that is up late: lit bars under thatch with painted cabins beside
// them, string lights, parasols and loungers on the sand, surfboards stood in rows, tiki torches down the straights,
// bonfires with logs round them, a lifeguard tower, KARTEL hoardings at the corners and paper lanterns drifting over
// the island. Everything is built here from a few procedural meshes (one mesh a kind, shared by every copy), lit by
// its own glow: a handful of real lights at the bars, and otherwise emissive faces, halos (the lamps' glow shader)
// and warm pools on the ground.
public static partial class TrackSetup
{
    static readonly Color[] Paints =
    {
        new(0.95f, 0.36f, 0.30f), new(0.13f, 0.66f, 0.68f), new(0.98f, 0.76f, 0.22f),
        new(0.36f, 0.62f, 0.95f), new(0.96f, 0.50f, 0.68f), new(0.93f, 0.91f, 0.85f),
    };
    static readonly Color Warm = new(1f, 0.72f, 0.4f), Flame = new(1f, 0.5f, 0.14f);

    /// Lit paint: Simple Lit with a little of its own colour given off, so a hut in the dark between two lamps is a
    /// coloured hut and not a black box.
    static Material Paint(string name, Color c, float lift = 0.07f)
    {
        var m = LoadOrCreate(Art + $"Materials/{name}.mat", SimpleLit, c);
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * lift);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// A soft pool of light lying on the ground (KartCup/Pool): what a fire or a bar does to the sand round it, without
    /// spending one of a renderer's eight lights on it. A small grid laid over the terrain, brightest at its middle.
    static void Pool(Transform parent, string name, Vector3 at, float radius, string material, Color colour)
    {
        var shader = Shader.Find("KartCup/Pool");
        if (shader == null) { Debug.LogWarning("[track] KartCup/Pool missing: no light pools"); return; }
        var m = LoadOrCreate(Art + $"Materials/{material}.mat", shader, Color.white);
        m.shader = shader; m.SetColor("_Color", colour); EditorUtility.SetDirty(m);
        const int N = 12;
        var v = new List<Vector3>(); var col = new List<Color>(); var tri = new List<int>();
        for (int z = 0; z <= N; z++)
            for (int x = 0; x <= N; x++)
            {
                float u = x / (float)N * 2f - 1f, w = z / (float)N * 2f - 1f;
                var p = At(at.x + u * radius, at.z + w * radius, 0.16f);
                v.Add(p - at);
                float fall = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + w * w));
                col.Add(new Color(1f, 1f, 1f, fall * fall));
            }
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int a = z * (N + 1) + x;
                tri.AddRange(new[] { a, a + N + 1, a + 1, a + 1, a + N + 1, a + N + 2 });
            }
        var mesh = new Mesh();
        mesh.SetVertices(v); mesh.SetColors(col); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        var go = Part(parent, name, SaveMesh(mesh, "Beach_" + name), m, at, Vector3.one);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// Another of a prop already built: the same mesh, its own place, and (if given) its own paint.
    static GameObject Copy(GameObject proto, Transform parent, Vector3 pos, float yaw, Material[] mats = null, Vector3? scale = null, float tilt = 0f)
    {
        var go = new GameObject(proto.name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt, yaw, 0f));
        if (scale.HasValue) go.transform.localScale = scale.Value;
        go.AddComponent<MeshFilter>().sharedMesh = proto.GetComponent<MeshFilter>().sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats ?? proto.GetComponent<MeshRenderer>().sharedMaterials;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        props++;
        return go;
    }

    static void BeachTown(Transform root, System.Random rnd)
    {
        var town = new GameObject("BeachTown").transform;
        town.SetParent(root, false);
        int n = centre.Length;
        float edge = HalfWidth + Kerb;
        float R() => (float)rnd.NextDouble();

        // ---- materials ----
        var paints = Paints.Select((c, i) => Paint($"Beach_Paint{i}", c)).ToArray();
        var white = paints[5];
        var wood = Paint("Beach_Wood", new Color(0.36f, 0.24f, 0.14f), 0.05f);
        var bamboo = Paint("Beach_Bamboo", new Color(0.62f, 0.5f, 0.26f), 0.05f);
        var thatch = Paint("Beach_Thatch", new Color(0.58f, 0.44f, 0.2f), 0.06f);
        var dark = Paint("Beach_Dark", new Color(0.06f, 0.06f, 0.08f), 0f);
        var warmGlow = Emissive("Beach_Warm", Warm, 2.4f);
        var flame = Emissive("Beach_Flame", Flame, 3.4f);
        var bulbs = new[] { Emissive("Beach_BulbWarm", new Color(1f, 0.85f, 0.6f), 3f), Emissive("Beach_BulbPink", new Color(1f, 0.3f, 0.55f), 3f),
                            Emissive("Beach_BulbCyan", new Color(0.2f, 0.85f, 1f), 3f), Emissive("Beach_BulbGold", new Color(1f, 0.7f, 0.15f), 3f) };

        // ---- one mesh a kind (built at the origin, facing +Z) ----
        GameObject Proto(string name, System.Action<MeshBuilder> build, params Material[] mats)
        {
            var mb = new MeshBuilder(); build(mb);
            var go = mb.Finish("Beach_" + name, town, mats);
            go.name = name;
            return go;
        }
        Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

        // the bar: a counter in the round under a thatched cone, open-sided, lit from inside
        var bar = Proto("Bar", mb =>
        {
            mb.Frustum(0, 3.7f, 3.7f, -1.5f, 1.7f, 12, capTop: true);                       // 0 counter (painted)
            mb.Frustum(2, 2.7f, 2.7f, 1.7f, 4.4f, 12);                                       // 2 the lit inside
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f;
                mb.Box(3, new Vector3(Mathf.Cos(a) * 3.5f, 3.05f, Mathf.Sin(a) * 3.5f), X, Y, Z, new Vector3(0.3f, 2.7f, 0.3f));   // 3 posts
            }
            mb.Frustum(1, 5.6f, 2.6f, 4.3f, 6.3f, 12, capBottom: true);                      // 1 thatch, two tiers
            mb.Frustum(1, 3.1f, 0.35f, 6.1f, 8.4f, 12, capTop: true);
        }, paints[0], thatch, warmGlow, bamboo);

        // the cabin: a painted changing hut with a striped front, a lean-to roof and a lit window over the door
        var cabin = Proto("Cabin", mb =>
        {
            mb.Box(3, new Vector3(0, -0.75f, 0), X, Y, Z, new Vector3(4.6f, 1.5f, 4.6f));    // 3 the plinth it stands on
            mb.Box(0, new Vector3(0, 2.7f, 0), X, Y, Z, new Vector3(4.2f, 5.4f, 4.2f));      // 0 walls (painted)
            for (int k = -1; k <= 1; k++) mb.Box(1, new Vector3(k * 1.4f, 2.7f, 2.12f), X, Y, Z, new Vector3(0.45f, 5.4f, 0.06f));   // 1 white stripes
            var up = Quaternion.Euler(-11f, 0f, 0f) * Y; var fwd = Quaternion.Euler(-11f, 0f, 0f) * Z;
            mb.Box(1, new Vector3(0, 5.75f, 0.1f), X, up, fwd, new Vector3(5f, 0.3f, 5.4f)); // the roof, tipped forward
            mb.Box(3, new Vector3(0, 1.7f, 2.14f), X, Y, Z, new Vector3(1.7f, 3.4f, 0.1f));  // the door
            mb.Box(2, new Vector3(0, 4.3f, 2.14f), X, Y, Z, new Vector3(1.5f, 0.9f, 0.1f));  // 2 the window over it
        }, paints[1], white, warmGlow, wood);

        // the parasol: eight gores in two colours on a pole
        var parasol = Proto("Parasol", mb =>
        {
            mb.Frustum(2, 0.08f, 0.08f, -0.5f, 4.7f, 6);
            mb.Gores(0, 1, 3.1f, 0.12f, 3.9f, 4.9f, 8);
        }, paints[0], white, bamboo);

        // the lounger: a slatted bed with its back up
        var lounger = Proto("Lounger", mb =>
        {
            mb.Box(0, new Vector3(0, 0.7f, -0.5f), X, Y, Z, new Vector3(1.5f, 0.18f, 2.8f));
            var up = Quaternion.Euler(-38f, 0f, 0f) * Y; var fwd = Quaternion.Euler(-38f, 0f, 0f) * Z;
            mb.Box(0, new Vector3(0, 1.25f, 1.45f), X, up, fwd, new Vector3(1.5f, 0.18f, 1.7f));
            foreach (float z in new[] { -1.6f, 0.6f })
                foreach (float x in new[] { -0.6f, 0.6f }) mb.Box(1, new Vector3(x, 0.3f, z), X, Y, Z, new Vector3(0.14f, 0.8f, 0.14f));
        }, white, bamboo);

        // the surfboard: a long lozenge with a stripe round its middle (flattened where it is stood up)
        var board = Proto("Surfboard", mb =>
        {
            float[] r = { 0.08f, 0.5f, 0.62f, 0.62f, 0.5f, 0.1f }, y = { 0f, 0.7f, 1.9f, 3.1f, 4.4f, 5.2f };
            for (int k = 0; k < 5; k++) mb.Frustum(k == 2 ? 1 : 0, r[k], r[k + 1], y[k], y[k + 1], 10, capTop: k == 4, capBottom: k == 0);
        }, paints[2], white);

        // the tiki torch: a bamboo pole, a bowl, a flame
        var torch = Proto("Torch", mb =>
        {
            mb.Frustum(0, 0.1f, 0.08f, -0.6f, 3.6f, 6);
            mb.Frustum(1, 0.1f, 0.3f, 3.5f, 4f, 8);
            mb.Frustum(2, 0.24f, 0.02f, 3.95f, 4.9f, 6, capBottom: true);
        }, bamboo, dark, flame);

        // the bonfire: a ring of stones, crossed logs, three tongues of flame
        var fire = Proto("Bonfire", mb =>
        {
            for (int k = 0; k < 9; k++)
            {
                float a = k / 9f * Mathf.PI * 2f;
                mb.Box(0, new Vector3(Mathf.Cos(a) * 1.9f, 0.2f, Mathf.Sin(a) * 1.9f), X, Y, Z, new Vector3(0.8f, 0.6f, 0.8f));
            }
            for (int k = 0; k < 3; k++)
            {
                var q = Quaternion.Euler(0f, k * 60f, 24f);
                mb.Box(1, new Vector3(0, 0.75f, 0), q * X, q * Y, q * Z, new Vector3(3f, 0.36f, 0.36f));
            }
            mb.Frustum(2, 0.95f, 0.05f, 0.5f, 3.3f, 7);
            mb.Frustum(3, 0.6f, 0.03f, 0.7f, 4.2f, 6);
        }, Paint("Beach_Stone", new Color(0.4f, 0.4f, 0.42f)), wood, flame, Emissive("Beach_FlameCore", new Color(1f, 0.82f, 0.4f), 3.6f));

        // the lifeguard tower: four legs, a deck, a white hut with a red roof
        var tower = Proto("Lifeguard", mb =>
        {
            foreach (float x in new[] { -1.9f, 1.9f })
                foreach (float z in new[] { -1.9f, 1.9f }) mb.Box(2, new Vector3(x, 1.8f, z), X, Y, Z, new Vector3(0.3f, 5f, 0.3f));
            mb.Box(2, new Vector3(0, 4.4f, 0), X, Y, Z, new Vector3(5.2f, 0.3f, 5.2f));
            mb.Box(0, new Vector3(0, 6.2f, -0.4f), X, Y, Z, new Vector3(4f, 3.3f, 3.6f));
            var up = Quaternion.Euler(-9f, 0f, 0f) * Y; var fwd = Quaternion.Euler(-9f, 0f, 0f) * Z;
            mb.Box(1, new Vector3(0, 8.1f, 0f), X, up, fwd, new Vector3(5.2f, 0.3f, 5.6f));
            mb.Box(3, new Vector3(0, 6.4f, 1.42f), X, Y, Z, new Vector3(2.2f, 1.4f, 0.08f));
        }, white, paints[0], wood, warmGlow);

        // the hoarding's frame: two posts and the box the board sits in (the board itself is laid on per hoarding)
        var frame = Proto("HoardingFrame", mb =>
        {
            foreach (float x in new[] { -5f, 5f }) mb.Box(0, new Vector3(x, (HoardingFoot + HoardingH) / 2f - 0.8f, -0.4f), X, Y, Z, new Vector3(0.5f, HoardingFoot + HoardingH + 1.6f, 0.5f));
            mb.Box(1, new Vector3(0, HoardingFoot + HoardingH / 2f, -0.15f), X, Y, Z, new Vector3(HoardingW + 0.6f, HoardingH + 0.6f, 0.4f));
        }, wood, dark);

        // ---- where things go: anywhere along the lap that is clear of the grid and the tunnel, on ground that is
        // dry and near enough level. (Palm Bay is nearly all sweepers, so corners are not excluded: only the inside
        // of a tight one, where a village would hide the exit.)
        bool Open(int i, float side, float metres)
        {
            int span = Samples(metres);
            for (int k = -span; k <= span; k += Mathf.Max(1, span / 3))
                if (InTunnel(Wrap(i + k, n))) return false;
            var c = CornerAt(i);
            if (c.HasValue && Tight(c.Value) && c.Value.outside != side) return false;
            return i > Samples(110f) && i < n - Samples(70f);
        }
        float Slope(int i, float side, float from, float to) => Mathf.Abs(Beside(i, side, from).y - Beside(i, side, to).y);
        var sites = new List<(int i, float side)>();
        for (int i = 0; i < n; i += Samples(16f))
            foreach (float side in new[] { -1f, 1f })
                if (Open(i, side, 30f) && Beside(i, side, edge + 34f).y > 0.8f && Slope(i, side, edge + 12f, edge + 40f) < 4.5f) sites.Add((i, side));
        // spread them round the lap: a village, then a fire, then a village...
        var used = new List<int>();
        (int i, float side)? Take(float apart)
        {
            foreach (var s in sites.OrderBy(_ => rnd.Next()))
                if (used.All(u => Mathf.Min(Mathf.Abs(u - s.i), n - Mathf.Abs(u - s.i)) > Samples(apart))) { used.Add(s.i); return s; }
            return null;
        }

        var halos = new List<Vector3>();        // warm, steady: bulbs and windows
        var flames = new List<Vector3>();       // flickering: torches and fires
        int villages = 0, fires = 0;

        // ---- villages ----
        for (int v = 0; v < 5; v++)
        {
            var site = Take(105f);
            if (site == null) break;
            var (i, side) = site.Value;
            Vector3 f = Forward(i), r = right[i] * side;            // along the road, and away from it
            float face = Yaw(-r);                                     // fronts look at the road
            Vector3 P(float along, float away) { var p = centre[i] + f * along + r * (edge + away); return At(p.x, p.z, 0f); }
            var here = new GameObject("Village" + v).transform; here.SetParent(town, false);

            var barAt = P(0f, 26f);
            var barGo = Copy(bar, here, barAt, face, new[] { paints[rnd.Next(5)], thatch, warmGlow, bamboo });
            Collide(barGo, Solid.Solid);
            halos.Add(barAt + Vector3.up * 3.2f);
            Pool(here, "Pool" + v, barAt, 19f, "Beach_PoolWarm", Warm * 0.5f);
            var lamp = new GameObject("BarLight").AddComponent<Light>();
            lamp.transform.SetParent(here, false); lamp.transform.position = barAt - r * 5f + Vector3.up * 6.5f;
            lamp.type = LightType.Point; lamp.color = Warm; lamp.intensity = 150f; lamp.range = 24f; lamp.shadows = LightShadows.None;

            var roofs = new List<Vector3>();      // the two end cabins' roofs: the string lights hang from them to the bar
            foreach (int k in new[] { -2, -1, 1, 2 })
            {
                var at = P(k * 8.5f + (R() - 0.5f) * 1.5f, 24f + Mathf.Abs(k) * 2.2f + R() * 2f);
                var c = Copy(cabin, here, at, face + k * -7f, new[] { paints[rnd.Next(5)], white, warmGlow, wood });
                Collide(c, Solid.Solid);
                halos.Add(at + Quaternion.Euler(0, face + k * -7f, 0) * new Vector3(0, 4.3f, 2.4f));
                if (Mathf.Abs(k) == 2) roofs.Add(at + Vector3.up * 5.9f);
            }
            // parasols and loungers on the sand in front, two by two
            for (int k = 0; k < 6; k++)
            {
                var at = P((k - 2.5f) * 6.2f + (R() - 0.5f) * 2f, 11f + R() * 7f);
                float yaw = face + (R() - 0.5f) * 50f;
                Copy(parasol, here, at, R() * 360f, new[] { paints[rnd.Next(5)], white, bamboo }, null, (R() - 0.5f) * 6f);
                foreach (float dx in new[] { -1.5f, 1.5f })
                    Copy(lounger, here, at + Quaternion.Euler(0, yaw, 0) * new Vector3(dx, 0, 0.4f), yaw, new[] { paints[rnd.Next(6)], bamboo });
            }
            // a rack of boards at one end, a ball or two
            for (int k = 0; k < 5; k++)
            {
                var at = P(-23f - k * 1.5f, 19f);
                Copy(board, here, at, face, new[] { paints[(v + k) % 5], white }, new Vector3(1f, 1f, 0.16f), -8f);
            }
            // two posts out front and the string lights slung between the roofs and them
            foreach (float along in new[] { -9f, 9f })
            {
                var at = P(along, 15.5f);
                Copy(torch, here, at, 0f, new[] { bamboo, dark, dark }, new Vector3(1.4f, 1.55f, 1.4f));      // (a torch with no flame is a post)
            }
            Festoon(here, "Festoon" + v, new[] { roofs[0], barAt + Vector3.up * 6.4f, roofs[1] }, bulbs, dark, halos);
            Festoon(here, "FestoonFront" + v, new[] { P(-9f, 15.5f) + Vector3.up * 5.9f, barAt + Vector3.up * 6.4f, P(9f, 15.5f) + Vector3.up * 5.9f }, bulbs, dark, halos);
            villages++;
        }

        // ---- bonfires, a lifeguard tower by the first ----
        for (int b = 0; b < 4; b++)
        {
            var site = Take(60f);
            if (site == null) break;
            var (i, side) = site.Value;
            Vector3 f = Forward(i), r = right[i] * side;
            var p = centre[i] + r * (edge + 17f + R() * 6f); var at = At(p.x, p.z, 0f);
            Copy(fire, town, at, R() * 360f);
            flames.Add(at + Vector3.up * 2.2f);
            Pool(town, "FirePool" + b, at, 14f, "Beach_PoolFlame", Flame * 0.8f);
            for (int k = 0; k < 4; k++)                              // logs to sit on, round it
            {
                float a = (k * 90f + 40f + R() * 20f) * Mathf.Deg2Rad;
                var q = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4.6f;
                Prop(town, "Nature", "log_large", At(q.x, q.z, 0f), -a * Mathf.Rad2Deg + 90f, 1.1f);
            }
            if (b == 0)
            {
                var t = centre[i] + f * 16f + r * (edge + 13f);
                var go = Copy(tower, town, At(t.x, t.z, 0f), Yaw(-r));
                Collide(go, Solid.Solid);
                halos.Add(go.transform.position + Vector3.up * 6.4f - r * 1.5f);
            }
            fires++;
        }

        // ---- tiki torches down the straights, on the sea side of the road (the lamps stand on the land side) ----
        int torches = 0;
        for (int i = Samples(60f); i < n; i += Samples(19f))
        {
            if (InTunnel(i) || InTunnel(Wrap(i + Samples(12f), n)) || InTunnel(Wrap(i - Samples(12f), n))) continue;
            var at = Beside(i, -1f, edge + 3.4f);
            if (at.y < 0.5f) continue;
            Copy(torch, town, at, R() * 360f, null, null, (R() - 0.5f) * 5f);
            flames.Add(at + Vector3.up * 4.35f);
            torches++;
        }

        // ---- KARTEL hoardings facing the karts as they come into each corner ----
        int boards = 0;
        foreach (var c in corners)
        {
            int i = Wrap(c.start + c.count / 2, n);
            if (InTunnel(i)) continue;
            var at = Beside(i, c.outside, edge + 13f);
            if (at.y < 0.6f) continue;
            Hoarding(town, at, Yaw(-right[i] * c.outside), frame);
            boards++;
        }

        // (no undergrowth from the kits: under a blue moon their greens turn teal and the clumps read as crystals)

        // ---- boats pulled up on the sand, a lantern left burning in each ----
        for (int k = 0; k < 12; k++)
        {
            int i = rnd.Next(Samples(120f), n - Samples(80f));
            float side = R() < 0.5f ? -1f : 1f;
            if (InTunnel(i) || InTunnel(Wrap(i + Samples(20f), n)) || InTunnel(Wrap(i - Samples(20f), n))) continue;
            var at = Beside(i, side, edge + 16f + R() * 26f);
            if (at.y < 0.8f) continue;
            Prop(town, "Pirate", "boat-row-small", at, R() * 360f, 2.4f, Solid.None, (R() - 0.5f) * 14f);
            halos.Add(at + Vector3.up * 2.3f);
        }

        // ---- paper lanterns let go over the island ----
        var lanterns = new List<Vector3>();
        for (int k = 0; k < 80; k++)
        {
            float a = R() * Mathf.PI * 2f, d = 40f + R() * 290f;
            lanterns.Add(new Vector3(Mathf.Sin(a) * d, 26f + R() * 60f, Mathf.Cos(a) * d));
        }

        // (small halos: the phone is fill-bound, and a torch's glow three metres from the camera is a lot of screen)
        Glows(town, "BeachHalos", halos, new Color(1f, 0.8f, 0.55f), "Beach_Halo", new[] { (2.2f, 2.2f, 0.8f), (0.8f, 1.3f, 2.2f) });
        Glows(town, "BeachFlames", flames, Flame, "Beach_FlameGlow", new[] { (3.4f, 2.3f, 0.9f), (1.2f, 1.3f, 2.8f) }, 0.45f);
        Glows(town, "SkyLanterns", lanterns, new Color(1f, 0.6f, 0.25f), "Beach_Lantern", new[] { (2.4f, 2.2f, 0.9f), (0.8f, 1.2f, 2.6f) }, 0.25f);

        foreach (var p in new[] { bar, cabin, parasol, lounger, board, torch, fire, tower, frame }) Object.DestroyImmediate(p);
        Debug.Log($"[track] beach town: {villages} villages, {fires} bonfires, {torches} torches, {boards} hoardings, {halos.Count + flames.Count} lights of its own, {lanterns.Count} sky lanterns");
    }

    /// String lights: coloured bulbs every metre and a half along a wire that sags between each pair of anchors.
    static void Festoon(Transform parent, string name, Vector3[] anchors, Material[] bulbs, Material wire, List<Vector3> halos)
    {
        var mb = new MeshBuilder();
        int count = 0;
        for (int a = 0; a + 1 < anchors.Length; a++)
        {
            Vector3 from = anchors[a], to = anchors[a + 1];
            float span = Vector3.Distance(from, to); int steps = Mathf.Max(2, Mathf.RoundToInt(span / 1.5f));
            Vector3 prev = from;
            for (int k = 1; k <= steps; k++)
            {
                float u = k / (float)steps;
                var p = Vector3.Lerp(from, to, u) + Vector3.down * (span * 0.09f * 4f * u * (1f - u));     // a sag of a tenth of the span
                var along = (p - prev).normalized; var side = Vector3.Cross(Vector3.up, along).normalized;
                mb.Box(bulbs.Length, (p + prev) * 0.5f, side, Vector3.Cross(along, side), along, new Vector3(0.05f, 0.05f, Vector3.Distance(p, prev)));
                if (k < steps)
                {
                    mb.Box(count % bulbs.Length, p + Vector3.down * 0.22f, Vector3.right, Vector3.up, Vector3.forward, Vector3.one * 0.32f);
                    if (count % 3 == 0) halos.Add(p + Vector3.down * 0.22f);
                    count++;
                }
                prev = p;
            }
        }
        var go = mb.Finish("Beach_" + name, parent, bulbs.Append(wire).ToArray());
        go.name = name;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    const float HoardingW = 15f, HoardingH = 5.6f, HoardingFoot = 3.6f;

    /// A KARTEL hoarding: the film's lockup on a violet board, lit from within, on two posts.
    static void Hoarding(Transform parent, Vector3 at, float yaw, GameObject frame)
    {
        const float W = HoardingW, H = HoardingH, Foot = HoardingFoot;
        var board = new GameObject("Hoarding").transform;
        board.SetParent(parent, false);
        board.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Collide(Copy(frame, board, at, yaw), Solid.Solid);

        var panel = Emissive("Beach_HoardingPanel", new Color(0.13f, 0.04f, 0.36f), 1.6f);
        var back = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(back.GetComponent<Collider>());
        back.name = "Panel"; back.transform.SetParent(board, false);
        back.transform.localPosition = new Vector3(0, Foot + H / 2f, 0.07f); back.transform.localRotation = Quaternion.Euler(0, 180f, 0); back.transform.localScale = new Vector3(W, H, 1f);
        back.GetComponent<MeshRenderer>().sharedMaterial = panel;

        // the lockup, cut out (alpha-tested, so it is drawn with the solid things and needs no blending)
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/UI/kartel_lockup.png");
        if (tex != null)
        {
            var mark = LoadOrCreate(Art + "Materials/Beach_HoardingMark.mat", Shader.Find("Universal Render Pipeline/Unlit"), Color.white);
            mark.SetTexture("_BaseMap", tex); mark.SetColor("_BaseColor", Color.white * 1.35f);
            mark.SetFloat("_AlphaClip", 1f); mark.SetFloat("_Cutoff", 0.45f); mark.EnableKeyword("_ALPHATEST_ON"); mark.renderQueue = 2450;
            EditorUtility.SetDirty(mark);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(face.GetComponent<Collider>());
            face.name = "Lockup"; face.transform.SetParent(board, false);
            float w = W * 0.92f, h = w * tex.height / tex.width;
            face.transform.localPosition = new Vector3(0, Foot + H / 2f, 0.1f); face.transform.localRotation = Quaternion.Euler(0, 180f, 0); face.transform.localScale = new Vector3(w, h, 1f);
            var mr = face.GetComponent<MeshRenderer>(); mr.sharedMaterial = mark; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        props++;
    }
}
