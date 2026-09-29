using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Set dressing per map. Composition, not confetti: a treeline that closes the horizon, groves on the outside of
// corners and open views on the inside, two or three landmarks so a lap has a narrative, the start line celebrated,
// furniture rhythm on the straights, and nothing solid within Clear metres of the kerb.
public static partial class TrackSetup
{
    const float Clear = 1.2f;                        // metres of run-off between the kerb and any collider

    struct Corner { public int start, count, apex; public float outside, total; }   // outside: the side (in `right` units) a kart runs wide to
    static List<Corner> corners;
    static int props, propTris;

    /// Signed turn over ±k samples on the ground plane. Forward() has pitch now, so a crest would read as a corner.
    static float Turn(int i, int k)
    {
        int n = centre.Length;
        Vector3 a = Forward(Wrap(i - k, n)), b = Forward(Wrap(i + k, n));
        a.y = 0; b.y = 0;
        return Vector3.SignedAngle(a, b, Vector3.up);
    }

    /// Marks every sample inside a corner and walks the marks into runs; barriers, groves and landmarks all read this.
    static void Analyse()
    {
        int n = centre.Length;
        corners = new List<Corner>();
        var sign = new int[n];
        for (int i = 0; i < n; i++) { float t = Turn(i, 5); sign[i] = Mathf.Abs(t) > 5.5f ? (t > 0 ? 1 : -1) : 0; }
        int start = -1;
        for (int i = 0; i <= n; i++)
        {
            int here = i < n ? sign[i] : 0;
            if (here != 0 && start < 0) start = i;
            bool ends = here == 0 || (start >= 0 && sign[start] != here);
            if (start < 0 || !ends) continue;
            int count = i - start;
            if (count >= 7)
            {
                // A right-hander (turn > 0) runs wide to the left.
                var c = new Corner { start = start, count = count, outside = -sign[start], apex = start };
                float best = 0;
                for (int k = start; k < i; k++)
                {
                    float t = Mathf.Abs(Turn(k, 1)) * 0.5f;          // per-sample turn: the sum is the corner's angle
                    c.total += t;
                    if (t > best) { best = t; c.apex = k; }
                }
                corners.Add(c);
            }
            start = here != 0 ? i : -1;
        }
    }

    static Corner? CornerAt(int i) { foreach (var c in corners) if (i >= c.start && i < c.start + c.count) return c; return null; }
    static bool Tight(Corner c) => c.total > 70f;
    static List<Corner> Tightest() { var l = new List<Corner>(corners); l.Sort((a, b) => b.total.CompareTo(a.total)); return l; }

    // ---------- placement ----------
    static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    /// On the ground, sunk a little so nothing floats on a slope's downhill side.
    static Vector3 At(float x, float z, float lift = -0.15f) => new(x, TerrainGen.Height(x, z) + lift, z);
    static Vector3 Beside(int i, float side, float dist) { var p = centre[i] + right[i] * (dist * side); return At(p.x, p.z); }
    static Vector3 Polar(float a, float r) => At(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
    static float Metres => Length() / centre.Length;                 // metres per sample
    static int Samples(float metres) => Mathf.Max(1, Mathf.RoundToInt(metres / Metres));

    /// Nature and Racing import with their own per-material colours; the other kits share one colormap material.
    static Material MatFor(string kit) => kit is "Nature" or "Racing" ? null : AssetDatabase.LoadAssetAtPath<Material>(Art + $"Materials/{kit}.mat");

    /// A kit prop scaled to `height` metres (or to `width` across, for flat things), centred on `pos` in XZ so the
    /// kits' assorted pivots never matter, and `stretchX` for the one prop that spans the road.
    static GameObject Prop(Transform parent, string kit, string name, Vector3 pos, float yaw, float height, Solid solid = Solid.None,
                           float tilt = 0f, float width = 0f, float stretchX = 1f)
    {
        var go = Place(parent, Art + $"Models/Props/{kit}/{name}.fbx", MatFor(kit), pos, yaw, 1f, Solid.None, tilt);
        var b = Bounds(go);
        float basis = width > 0f ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
        if (basis > 0.001f)
        {
            float s = (width > 0f ? width : height) / basis;
            go.transform.localScale = new Vector3(s * stretchX, s, s);
            b = Bounds(go);
        }
        go.transform.position += new Vector3(pos.x - b.center.x, 0, pos.z - b.center.z);
        if (solid != Solid.None) Collide(go, solid);
        return go;
    }

    /// A ring of scenery that closes the horizon. Skips anything that would stand in the sea.
    static void Ring(Transform root, System.Random rnd, string kit, string[] names, float r0, float r1, int count, float h0, float h1, float minY = float.NegativeInfinity)
    {
        for (int i = 0; i < count; i++)
        {
            float a = (i + (float)rnd.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
            var p = Polar(a, Mathf.Lerp(r0, r1, (float)rnd.NextDouble()));
            if (p.y < minY) continue;
            Prop(root, kit, names[rnd.Next(names.Length)], p, (float)rnd.NextDouble() * 360f, Mathf.Lerp(h0, h1, (float)rnd.NextDouble()));
        }
    }

    /// Trackside density from a low-frequency noise, so there are groves and open stretches instead of a picket line.
    /// Corners double it on the outside and nearly empty the inside; a tight corner's inside gets nothing tall at all,
    /// so the exit stays visible. Solids are pushed clear of the kerb.
    static void Groves(Transform root, System.Random rnd, string kit, string[] tall, float tallH0, float tallH1, Solid tallSolid,
                       string[] low, float lowH, Solid lowSolid, float density = 1f)
    {
        float edge = HalfWidth + Kerb;
        int n = centre.Length;
        for (int i = 0; i < n; i += 3)
            for (int side = -1; side <= 1; side += 2)
            {
                var c = CornerAt(i);
                float mult = 1f; bool tallOk = true;
                if (c.HasValue)
                {
                    bool outside = side == c.Value.outside;
                    mult = outside ? 1.9f : 0.3f;
                    tallOk = outside || !Tight(c.Value);
                }
                float grove = Mathf.PerlinNoise(i * 0.021f + V.seed * 3.1f, side > 0 ? 0.3f : 7.7f);
                double roll = rnd.NextDouble() * Mathf.Lerp(2.2f, 0.75f, grove) / (mult * density);
                if (roll < 0.30 && tallOk)
                {
                    var go = Prop(root, kit, tall[rnd.Next(tall.Length)], Beside(i, side, edge + 5f + (float)rnd.NextDouble() * 16f),
                                  (float)rnd.NextDouble() * 360f, Mathf.Lerp(tallH0, tallH1, (float)rnd.NextDouble()), tallSolid, (float)(rnd.NextDouble() * 4 - 2));
                    PushClear(go, centre[i], right[i], side, Clear);
                    // a second row behind the first: a grove needs depth
                    if (grove > 0.5f)
                        Prop(root, kit, tall[rnd.Next(tall.Length)], Beside(i, side, edge + 20f + (float)rnd.NextDouble() * 22f),
                             (float)rnd.NextDouble() * 360f, Mathf.Lerp(tallH0, tallH1, (float)rnd.NextDouble()));
                }
                else if (roll < 0.42 && low != null)
                {
                    var go = Prop(root, kit, low[rnd.Next(low.Length)], Beside(i, side, edge + 2.5f + (float)rnd.NextDouble() * 6f),
                                  (float)rnd.NextDouble() * 360f, lowH * (0.8f + (float)rnd.NextDouble() * 0.5f), lowSolid);
                    PushClear(go, centre[i], right[i], side, Clear);
                }
            }
    }

    /// Trackside furniture every `every` metres on the straights only, so the corners keep their sightlines.
    static void Rhythm(Transform root, string kit, string name, float every, float height, float lateral, Solid solid = Solid.None, float yawToRoad = 90f, int phase = 0)
    {
        int n = centre.Length, step = Samples(every);
        for (int i = phase; i < n; i += step)
        {
            if (CornerAt(i).HasValue) continue;
            for (int side = -1; side <= 1; side += 2)
                Prop(root, kit, name, Beside(i, side, HalfWidth + Kerb + lateral), Yaw(right[i] * side) + yawToRoad, height, solid);
        }
    }

    /// Small things near the road: ground cover, never an obstacle. `width` > 0 fits flat props across instead of up.
    static void Cover(Transform root, System.Random rnd, string kit, string[] names, int count, float h0, float h1, float from, float to, bool byWidth = false)
    {
        int n = centre.Length;
        for (int k = 0; k < count; k++)
        {
            int i = rnd.Next(n); float side = rnd.NextDouble() > 0.5 ? 1 : -1;
            float size = Mathf.Lerp(h0, h1, (float)rnd.NextDouble());
            Prop(root, kit, names[rnd.Next(names.Length)], Beside(i, side, HalfWidth + Kerb + from + (float)rnd.NextDouble() * (to - from)),
                 (float)rnd.NextDouble() * 360f, size, Solid.None, 0f, byWidth ? size : 0f);
        }
    }

    /// Start/finish: the Racing kit's overhead gantry across the chequer and a grandstand run beside the start straight.
    /// The gantry is stretched across the road only (its clear span is 1.0 of its 1.26 width, measured off the kit),
    /// with the height held at 12 m so it stays a gantry and not a bridge; its legs land half a metre outside the kerb.
    static void StartLine(Transform root, int standSegments, Material glow)
    {
        int n = centre.Length;
        float span = 2f * (HalfWidth + Kerb) + 1f;
        var gantry = Place(root, Art + "Models/Props/Racing/overheadLights.fbx", null, centre[1], 0f, 1f);
        var b = Bounds(gantry);                                    // unrotated, so x is the model's own width
        if (b.size.y > 0.01f)
        {
            float sy = 12f / b.size.y, sx = span * 1.26f / b.size.x;
            gantry.transform.localScale = new Vector3(sx, sy, sy);
            gantry.transform.rotation = Quaternion.Euler(0, Yaw(Forward(1)), 0);
            b = Bounds(gantry);
            gantry.transform.position += new Vector3(centre[1].x - b.center.x, -0.3f, centre[1].z - b.center.z);
            // one box per leg; a box around the whole mesh would wall off the start line
            for (int side = -1; side <= 1; side += 2)
            {
                var leg = new GameObject("Leg").AddComponent<BoxCollider>();
                leg.transform.SetParent(root, false);
                leg.transform.SetPositionAndRotation(centre[1] + right[1] * ((span * 0.5f + 0.065f * sx) * side) + Vector3.up * 6f, Quaternion.LookRotation(Forward(1), Vector3.up));
                leg.size = new Vector3(0.13f * sx, 12f, 0.19f * sy);
            }
            if (glow != null) Glow(gantry, glow);
        }

        int per = Samples(10f);
        for (int k = 0; k < standSegments; k++)
        {
            int i = Wrap((k - standSegments / 2) * per + 2, n);
            bool covered = k % 3 == 1;
            // both tiles are 1 unit long; heights chosen so they scale the same and the run tiles without gaps
            Prop(root, "Racing", covered ? "grandStandCovered" : "grandStand", Beside(i, -1, HalfWidth + Kerb + 9f), Yaw(-right[i]), covered ? 11.9f : 9f, Solid.Solid);
        }
        for (int side = -1; side <= 1; side += 2)
            Prop(root, "Racing", "flagCheckers", Beside(0, side, HalfWidth + Kerb + 1.4f), Yaw(right[0] * side), 4.5f);
    }

    /// The bright white parts of a Racing prop (its lamp heads, on the kit's default white material) become emissive,
    /// so the shared Bloom makes them glow: no point lights on a phone.
    static void Glow(GameObject go, Material glow)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var mats = r.sharedMaterials; bool changed = false;
            for (int k = 0; k < mats.Length; k++)
            {
                var m = mats[k];
                if (m == null) continue;
                var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.black;
                if (m.name.ToLowerInvariant().Contains("default") || c.grayscale > 0.985f) { mats[k] = glow; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    static Material GlowMat()
    {
        var m = LoadOrCreate(Art + "Materials/Glow.mat", Shader.Find("Universal Render Pipeline/Unlit"), Color.white);
        m.SetColor("_BaseColor", new Color(2.4f, 2.2f, 1.7f));      // over 1: past the bloom threshold
        EditorUtility.SetDirty(m);
        return m;
    }

    /// The longest stretch of straight road, as the sample it starts at and its length in samples.
    static (int start, int count) LongestStraight()
    {
        int n = centre.Length, bestStart = 0, best = 0, run = 0;
        for (int i = 0; i < n * 2; i++)
        {
            if (CornerAt(i % n).HasValue) { run = 0; continue; }
            run++;
            if (run > best) { best = run; bestStart = Wrap(i - run + 1, n); }
        }
        return (bestStart, Mathf.Min(best, n));
    }

    // ---------- the maps ----------
    static void Dressing()
    {
        var root = new GameObject("Dressing").transform;
        var rnd = new System.Random(V.seed);
        props = 0; propTris = 0;
        switch (V.id)
        {
            case "beach": DressBeach(root, rnd); break;
            case "alpine": DressAlpine(root, rnd); break;
            case "forest": DressForest(root, rnd); break;
            default: DressSpeedway(root, rnd); break;
        }

        // Cones on the apex of the tighter corners: they read as a racing line. Deliberately NOT solid: a collider
        // there punishes the line the track is teaching you to take, and a kart can wedge on a knocked-loose one.
        var toycar = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/ToyCar.mat");
        int n = centre.Length;
        for (int i = 0; i < n; i += 2)
        {
            float turn = Turn(i, 6);
            if (Mathf.Abs(turn) < 7f) continue;
            Place(root, Art + "Models/Props/ToyCar/item-cone.fbx", toycar, centre[i] + right[i] * (HalfWidth - 0.6f) * -Mathf.Sign(turn), 0, 1.6f);
        }

        // after dark (TrackNight.cs): the lamps down the road and the studs along it
        SodiumLamps(root);
        Studs(root);
    }

    static void DressBeach(Transform root, System.Random rnd)
    {
        var palms = new[] { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        var rocks = new[] { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        var clutter = new[] { "crate", "barrel", "crate-bottles", "chest" };
        float edge = HalfWidth + Kerb;
        int n = centre.Length;

        StartLine(root, 5, null);
        Lighthouse(root);
        Groves(root, rnd, "Pirate", palms, 7f, 11f, Solid.Trunk, clutter, 1.4f, Solid.Solid, 1.1f);
        Cover(root, rnd, "Pirate", new[] { "patch-sand", "patch-sand-foliage", "patch-sand" }, 110, 5f, 10f, 4f, 60f, byWidth: true);
        // no pennant flags: after dark the lamps are the rhythm down the straights

        // Headlands: rock stacks on the outside of the two tightest corners, the lighthouse out to sea off the first
        // (TrackNight.cs), so you aim at something through the lap. The wreck sits off the second one; the dock on
        // the shore past the start.
        var tight = Tightest();
        for (int k = 0; k < Mathf.Min(2, tight.Count); k++)
        {
            var c = tight[k];
            for (int j = 0; j < 7; j++)
            {
                int i = Wrap(c.apex + (j - 3) * 4, n);
                var go = Prop(root, "Pirate", rocks[rnd.Next(rocks.Length)], Beside(i, c.outside, edge + 7f + (float)rnd.NextDouble() * 12f),
                              (float)rnd.NextDouble() * 360f, 4f + (float)rnd.NextDouble() * 3.5f, Solid.Solid, (float)(rnd.NextDouble() * 8 - 4));
                PushClear(go, centre[i], right[i], c.outside, Clear);
            }
            if (k == 1)
            {
                var a = Mathf.Atan2(centre[c.apex].x, centre[c.apex].z);
                Prop(root, "Pirate", "ship-wreck", new Vector3(Mathf.Sin(a) * 455f, -0.6f, Mathf.Cos(a) * 455f), a * Mathf.Rad2Deg + 140f, 22f);
            }
        }
        {
            int i = Wrap(n / 4, n);
            var a = Mathf.Atan2(centre[i].x, centre[i].z);
            Prop(root, "Pirate", "structure-platform-dock", new Vector3(Mathf.Sin(a) * 404f, -0.2f, Mathf.Cos(a) * 404f), a * Mathf.Rad2Deg + 90f, 3.6f, Solid.Solid);
            Prop(root, "Pirate", "boat-row-small", new Vector3(Mathf.Sin(a + 0.08f) * 440f, -0.3f, Mathf.Cos(a + 0.08f) * 440f), (float)rnd.NextDouble() * 360f, 11f);
            Prop(root, "Pirate", "boat-row-small", new Vector3(Mathf.Sin(a - 0.12f) * 452f, -0.3f, Mathf.Cos(a - 0.12f) * 452f), (float)rnd.NextDouble() * 360f, 10f);
        }

        // The shore: a palm treeline above the tide line and rocks in the surf, then buoys out past it.
        Ring(root, rnd, "Pirate", palms, 335f, 398f, 120, 8f, 12f, minY: 0.4f);
        Ring(root, rnd, "Pirate", rocks, 398f, 428f, 44, 3f, 7f);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f + 0.2f;
            Prop(root, "Watercraft", i % 3 == 0 ? "buoy-flag" : "buoy", new Vector3(Mathf.Sin(a) * 470f, -0.5f, Mathf.Cos(a) * 470f), 0, i % 3 == 0 ? 6f : 4.5f);
        }
    }

    static void DressAlpine(Transform root, System.Random rnd)
    {
        var firs = new[] { "tree-snow-a", "tree-snow-b", "tree-snow-c" };
        float edge = HalfWidth + Kerb;
        int n = centre.Length;

        StartLine(root, 3, null);
        Groves(root, rnd, "Holiday", firs, 8f, 12f, Solid.Trunk, new[] { "rocks-small", "rocks-medium" }, 2.2f, Solid.Solid, 1.4f);
        Cover(root, rnd, "Holiday", new[] { "snow-flat", "snow-flat-large", "snow-pile" }, 90, 4f, 8f, 1f, 12f, byWidth: true);
        Rhythm(root, "Holiday", "lantern", 40f, 3.4f, 1.6f);
        Rhythm(root, "Holiday", "candy-cane-red", 24f, 1.7f, 1.3f, phase: Samples(12f));

        // Decorated trees flank the start, presents beside the grid: the village end of the pass.
        for (int side = -1; side <= 1; side += 2)
            Prop(root, "Holiday", "tree-decorated", Beside(Wrap(-4, n), side, edge + 6f), (float)rnd.NextDouble() * 360f, 11f, Solid.Trunk);
        var presents = new[] { "present-a-cube", "present-b-round", "present-a-rectangle", "present-b-cube" };
        for (int k = 0; k < 6; k++)
            Prop(root, "Holiday", presents[k % presents.Length], Beside(Wrap(-Samples(8f + k * 4f), n), k % 2 == 0 ? 1 : -1, edge + 2.6f), (float)rnd.NextDouble() * 360f, 1.1f, Solid.Solid);

        // Cabins on the outside of corners, well back; the ones by the tightest two corners get a sled, a snowman and a bench.
        var tight = Tightest();
        for (int k = 0; k < Mathf.Min(6, tight.Count); k++)
        {
            var c = tight[k];
            int i = Wrap(c.apex + (k % 2 == 0 ? 6 : -6), n);
            var p = Beside(i, c.outside, edge + 24f + k * 2f);
            float yaw = Yaw(right[i] * -c.outside);                // door toward the road
            Cabin(root, p, yaw, 3f);
            Prop(root, "Holiday", "lantern", Beside(i, c.outside, edge + 19f), yaw, 3.2f);
            if (k < 2)
            {
                Prop(root, "Holiday", "snowman", Beside(Wrap(i + 5, n), c.outside, edge + 20f), yaw + 20f, 2.6f, Solid.Solid);
                Prop(root, "Holiday", "sled", Beside(Wrap(i - 4, n), c.outside, edge + 21f), yaw - 40f, 1.2f);
                Prop(root, "Holiday", "bench", Beside(Wrap(i - 2, n), c.outside, edge + 18f), yaw, 1.6f);
            }
        }

        // A parked train on the longest straight: the landmark you line up on before the hairpins.
        var (s0, count) = LongestStraight();
        Train(root, Wrap(s0 + count / 6, n), -1);

        // Outcrops on the lower slopes, then fir stands climbing the mountains and a bigger, further ring on the peaks.
        Ring(root, rnd, "Holiday", new[] { "rocks-large", "rocks-medium" }, 300f, 420f, 34, 5f, 9f);
        Ring(root, rnd, "Holiday", firs, 285f, 400f, 150, 9f, 13f);
        Ring(root, rnd, "Holiday", firs, 410f, 480f, 70, 12f, 15f);
    }

    /// A hut from the Holiday cabin modules: four wall modules on one tile, a corner post at each corner, a pointed
    /// snow roof. One box collider for the whole hut, so a kart bounces off the building and not off each plank.
    static void Cabin(Transform parent, Vector3 pos, float yaw, float scale)
    {
        var hut = new GameObject("Cabin").transform;
        hut.SetParent(parent, false);
        hut.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        hut.localScale = Vector3.one * scale;
        var walls = new[] { "cabin-doorway", "cabin-window-a", "cabin-wall-wreath", "cabin-window-b" };
        for (int k = 0; k < 4; k++)
        {
            Module(hut, walls[k], Vector3.zero, k * 90f);
            Module(hut, "cabin-corner", Vector3.zero, k * 90f);
        }
        Module(hut, "cabin-roof-snow-point", Vector3.up, 0f);
        var box = hut.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0, 0.8f, 0); box.size = new Vector3(1.4f, 1.6f, 1.4f);
        GameObjectUtility.SetStaticEditorFlags(hut.gameObject, StaticEditorFlags.BatchingStatic);
    }

    static void Module(Transform hut, string name, Vector3 local, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Props/Holiday/{name}.fbx");
        if (prefab == null) { Debug.LogWarning("[track] missing Holiday/" + name); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hut);
        go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one;
        var mat = MatFor("Holiday");
        if (mat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        Count(go);
    }

    /// Locomotive, tender and three wagons on straight rail, parked beside the road. Everything sits at one height:
    /// the cars are 7 m off the kerb, inside the band that is level with the road, so the train does not undulate.
    static void Train(Transform root, int i0, float side)
    {
        const float S = 9f;                                    // the kit's train at 9x is a 4 m tall narrow-gauge loco
        float lateral = HalfWidth + Kerb + 7f;
        var start = Beside(i0, side, lateral);
        int n = centre.Length;
        for (int k = 0; k < 16; k++)
        {
            int i = Wrap(i0 + Samples(k * 4.5f), n);
            var p = Beside(i, side, lateral); p.y = start.y;
            Scaled(root, "Holiday", "trainset-rail-straight", p, Yaw(Forward(i)) - 90f, S);
        }
        var cars = new[] { ("train-locomotive", 6.1f), ("train-tender", 3.3f), ("train-wagon-logs", 6.3f), ("train-wagon", 6.5f), ("train-wagon-flat", 6.3f) };
        float along = 6f;
        foreach (var (name, len) in cars)
        {
            int i = Wrap(i0 + Samples(along + len * 0.5f), n);
            var p = Beside(i, side, lateral); p.y = start.y;
            Scaled(root, "Holiday", name, p, Yaw(Forward(i)) - 90f, S, Solid.Solid);
            along += len + 0.4f;
        }
    }

    /// Place at an explicit scale, centred on `pos` in XZ.
    static GameObject Scaled(Transform parent, string kit, string name, Vector3 pos, float yaw, float scale, Solid solid = Solid.None)
    {
        var go = Place(parent, Art + $"Models/Props/{kit}/{name}.fbx", MatFor(kit), pos, yaw, scale, solid);
        var b = Bounds(go);
        go.transform.position += new Vector3(pos.x - b.center.x, 0, pos.z - b.center.z);
        return go;
    }

    static void DressForest(Transform root, System.Random rnd)
    {
        var pines = new[] { "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD", "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundA", "tree_pineRoundB", "tree_pineRoundC" };
        var broad = new[] { "tree_oak", "tree_fat", "tree_detailed" };
        float edge = HalfWidth + Kerb;
        int n = centre.Length;

        StartLine(root, 3, null);
        Groves(root, rnd, "Nature", pines, 9f, 12f, Solid.Trunk, new[] { "rock_tallA", "rock_tallC", "stone_tallB", "plant_bushLarge", "plant_bushDetailed" }, 2.4f, Solid.Solid, 1.5f);
        Groves(root, rnd, "Nature", broad, 6f, 8f, Solid.Trunk, null, 0f, Solid.None, 0.5f);
        Cover(root, rnd, "Nature", new[] { "mushroom_red", "mushroom_redGroup", "mushroom_tanGroup", "flower_redA", "flower_yellowB", "flower_purpleC" }, 60, 0.6f, 1.1f, 1.5f, 6f);
        Cover(root, rnd, "Nature", new[] { "grass", "grass_large", "plant_bush", "grass_leafs" }, 120, 0.6f, 1.1f, 1.2f, 10f);
        Cover(root, rnd, "Survival", new[] { "tree-autumn", "tree-autumn-tall" }, 26, 7f, 9.5f, 8f, 26f);

        // A rock wall on the outside of the tightest corner: two staggered rows of big boulders. The kit's cliff
        // blocks are tiling cubes and read as a stack of crates from the road; the boulders read as a rock face.
        var tight = Tightest();
        var boulders = new[] { "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_tallE", "rock_tallF", "stone_largeA", "stone_largeC", "stone_tallB" };
        if (tight.Count > 0)
        {
            var c = tight[0];
            // Fitted by WIDTH: these rocks are wide and flat, so an 8 m tall one is 30 m across and its box sat on the road.
            for (int j = 0; j < 10; j++)
            {
                int i = Wrap(c.apex + (j - 5) * Samples(5.5f), n);
                float w = 5f + (float)rnd.NextDouble() * 3f;
                var go = Prop(root, "Nature", boulders[rnd.Next(boulders.Length)], Beside(i, c.outside, edge + 5f), (float)rnd.NextDouble() * 360f, 0f, Solid.Solid, width: w);
                PushClear(go, centre[i], right[i], c.outside, Clear);
                if (j % 2 == 1)
                {
                    var back = Prop(root, "Nature", boulders[rnd.Next(boulders.Length)], Beside(i, c.outside, edge + 12f), (float)rnd.NextDouble() * 360f, 0f, Solid.Solid, width: w + 4f);
                    PushClear(back, centre[i], right[i], c.outside, Clear + 4f);
                }
            }
        }
        // Boulder clusters on the next two corners' outsides.
        for (int k = 1; k < Mathf.Min(3, tight.Count); k++)
        {
            var c = tight[k];
            for (int j = 0; j < 5; j++)
            {
                int i = Wrap(c.apex + (j - 2) * 3, n);
                var go = Prop(root, "Nature", j % 2 == 0 ? "rock_tallB" : "stone_tallD", Beside(i, c.outside, edge + 5f + (float)rnd.NextDouble() * 8f),
                              (float)rnd.NextDouble() * 360f, 3f + (float)rnd.NextDouble() * 2.5f, Solid.Solid);
                PushClear(go, centre[i], right[i], c.outside, Clear);
            }
        }

        // Fence along the longest straight, a campsite on the open inside of the corner after it.
        var (s0, count) = LongestStraight();
        for (int k = 0; k < Mathf.Min(count, Samples(66f)); k += Samples(3f))
        {
            int i = Wrap(s0 + k, n);
            Prop(root, "Nature", "fence_simple", Beside(i, 1, edge + 1.8f), Yaw(Forward(i)) + 90f, 1.1f);
        }
        {
            int i = Wrap(s0 + count + 8, n);
            var c = CornerAt(i);
            float side = c.HasValue ? -c.Value.outside : 1f;
            var camp = Beside(i, side, edge + 12f);
            Prop(root, "Nature", "tent_detailedOpen", camp, Yaw(right[i] * -side), 2.6f, Solid.Solid);
            Prop(root, "Nature", "campfire_logs", Beside(i, side, edge + 8f), 0f, 0.5f);
            Prop(root, "Nature", "log_stack", Beside(Wrap(i + 3, n), side, edge + 11f), Yaw(Forward(i)), 1.3f, Solid.Solid);
        }

        // The lake: a wooden bridge at its near shore, on the line from the water to the road, as the lap's landmark.
        {
            var toRoad = -V.lake.normalized;
            var shore = V.lake + toRoad * (V.lakeRadius + 1f);
            Prop(root, "Nature", "bridge_wood", At(shore.x, shore.y, 0.3f), Yaw(new Vector3(toRoad.x, 0, toRoad.y)), 0f, Solid.Solid, 0f, width: 9f);
        }

        // The pines close the horizon on the hills, thinning to a taller far ring on the ridge.
        Ring(root, rnd, "Nature", pines, 290f, 390f, 150, 11f, 14f);
        Ring(root, rnd, "Nature", pines, 405f, 470f, 70, 14f, 17f);
    }

    static void DressSpeedway(Transform root, System.Random rnd)
    {
        float edge = HalfWidth + Kerb;
        int n = centre.Length;
        var glow = GlowMat();
        StartLine(root, 0, glow);

        // Grandstands the length of both long straights, on the outside; the pits and paddock inside the start straight.
        int per = Samples(10f);
        foreach (int centreSample in new[] { 0, n / 2 })
            for (int k = -11; k <= 11; k++)
            {
                int i = Wrap(centreSample + k * per, n);
                if (CornerAt(i).HasValue) continue;
                bool covered = ((k % 4) + 4) % 4 == 1;
                Prop(root, "Racing", covered ? "grandStandCovered" : "grandStand", Beside(i, -1, edge + 12f), Yaw(-right[i]), covered ? 11.9f : 9f, Solid.Solid);
            }
        int pit = Samples(8.6f);
        for (int k = 0; k < 7; k++)
        {
            int i = Wrap(-40 + k * pit, n);
            Prop(root, "Racing", k < 5 ? "pitsGarage" : "pitsOffice", Beside(i, 1, edge + 9f), Yaw(right[i]), k < 5 ? 6f : 4.4f, Solid.Solid);
        }
        var cars = new[] { "raceCarRed", "raceCarGreen", "raceCarOrange", "raceCarWhite" };
        for (int k = 0; k < 4; k++)
        {
            int i = Wrap(-36 + k * Samples(12f), n);
            Prop(root, "Racing", k % 2 == 0 ? "tent" : "tentLong", Beside(i, 1, edge + 24f), Yaw(right[i]), 5f, Solid.Solid);
            Prop(root, "Racing", cars[k], Beside(Wrap(i + 2, n), 1, edge + 18f), (float)rnd.NextDouble() * 360f, 1.5f, Solid.Solid);
        }

        // Floodlights every 35 m both sides, overhead signs at the entry to the two big turns, banners and billboards
        // on the corner outsides.
        Rhythm(root, "Racing", "lightPostLarge", 35f, 12f, 3f, phase: Samples(17f));
        foreach (var go in new List<GameObject>(Lamps(root))) Glow(go, glow);
        var tight = Tightest();
        for (int k = 0; k < Mathf.Min(2, tight.Count); k++)
        {
            int i = Wrap(tight[k].start - 6, n);
            var span = Place(root, Art + "Models/Props/Racing/overheadRound.fbx", null, centre[i], 0f, 1f);
            var b = Bounds(span);
            if (b.size.y > 0.01f)
            {
                float sy = 12f / b.size.y, sx = (2f * edge + 1f) * 1.87f / (1.5f * b.size.x);   // 1.5 of its 1.87 width is clear span
                span.transform.localScale = new Vector3(sx, sy, sy);
                span.transform.rotation = Quaternion.Euler(0, Yaw(Forward(i)), 0);
                b = Bounds(span);
                span.transform.position += new Vector3(centre[i].x - b.center.x, -0.3f, centre[i].z - b.center.z);
                Glow(span, glow);
            }
        }
        for (int k = 0; k < corners.Count; k++)
        {
            var c = corners[k];
            Prop(root, "Racing", k % 2 == 0 ? "bannerTowerRed" : "bannerTowerGreen", Beside(c.apex, c.outside, edge + 10f), Yaw(right[c.apex] * -c.outside), 11f, Solid.Solid);
            int i = Wrap(c.apex + 14, n);
            Prop(root, "Racing", k % 2 == 0 ? "billboard" : "billboardLow", Beside(i, c.outside, edge + 12f), Yaw(right[i] * -c.outside), 8f, Solid.Solid);
        }

        // Low walls and pylons give the straights their rhythm; the corners keep the tall chevron barriers.
        Rhythm(root, "Racing", "barrierWall", 7f, 0.9f, 1.8f, Solid.Solid, 90f);
        Rhythm(root, "Racing", "pylon", 20f, 0.6f, 0.5f, Solid.None, 0f, Samples(10f));

        // A few trees in the infield, and a ring of them on the bank so the stands have a skyline behind them.
        for (int k = 0; k < 14; k++)
            Prop(root, "Racing", k % 3 == 0 ? "treeSmall" : "treeLarge", At((float)(rnd.NextDouble() * 180 - 90), (float)(rnd.NextDouble() * 110 - 55)), (float)rnd.NextDouble() * 360f, 8f + (float)rnd.NextDouble() * 3f, Solid.Trunk);
        for (int k = 0; k < 110; k++)
        {
            float a = (k + (float)rnd.NextDouble() * 0.6f) / 110f * Mathf.PI * 2f, e = 1.42f + (float)rnd.NextDouble() * 0.2f;
            Prop(root, "Racing", k % 4 == 0 ? "treeSmall" : "treeLarge", At(250f * e * Mathf.Sin(a), 150f * e * Mathf.Cos(a)), (float)rnd.NextDouble() * 360f, 10f + (float)rnd.NextDouble() * 4f);
        }
    }

    static IEnumerable<GameObject> Lamps(Transform root)
    {
        foreach (Transform t in root) if (t.name.StartsWith("lightPost")) yield return t.gameObject;
    }

    static void Count(GameObject go)
    {
        props++;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) propTris += (int)mf.sharedMesh.GetIndexCount(s) / 3;
    }
}
