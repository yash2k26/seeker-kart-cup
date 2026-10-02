using KartCup;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One theme per KartCup.Tracks entry, in table order: the circuit's shape, the terrain under it, the palette the
// terrain is painted with, and the sky/sun/fog that make four maps read as four places rather than four layouts.
public static partial class TrackSetup
{
    sealed class Theme
    {
        public string id;
        // circuit shape: a wobbled circle (two harmonics) or, with `straight` > 0, a stadium of two straights
        // and two semicircles with a chicane on the back straight
        public float radius, halfWidth, ampA, ampB, straight, chicane;
        public int harmA, harmB, controlPoints, seed;
        // terrain
        public System.Func<float, float, float> shape;
        public TerrainGen.Palette palette;
        public Color ground;                   // the palette's mid tone, for the Simple Lit fallback
        public float sea = float.NaN;          // water plane height, NaN for none
        public Vector2 lake; public float lakeRadius;
        // look
        public Vector3 sun; public Color sunColor; public float sunIntensity;
        public Color skyTint, horizon; public float exposure, atmosphere, sunSize, sunConvergence;
        public Color ambientSky, ambientEquator, ambientGround, fog; public float fogStart, fogEnd, farClip;
        // Per-map post overrides on a copy of the shared profile: the shared split-toning (warm highlights) is
        // right for a beach and wrong for snow, and dusk needs more exposure and a lower bloom threshold.
        public System.Action<VolumeProfile> post;
        // After dark (Palm Bay): a photographed sky cube in place of the procedural one, the sun fields driving the
        // moon, a wet road, sodium lamps every lampEvery metres, cyan road studs and the lighthouse. The ground can
        // carry a detail tile (Art/Textures/Surfaces) so its flat faces read as sand up close.
        public bool night, studs, lighthouse, tunnel;
        public string sky, water = "Water", groundDetail;
        public float skyRotation, skyExposure = 1f, wet, lampEvery, groundDetailTile = 7f;
        // The same kit on the other circuits, each in its own light: what colour the lamps and the studs burn, whether
        // the karts run with headlights without it being night, how light the asphalt is, and (Pine Ridge) fireflies.
        public Color lampColor = new(1f, 0.62f, 0.28f), studColor = new(0.10f, 0.75f, 1f), fireflyColor = new(0.75f, 1f, 0.35f);
        public bool headlights;
        public float roadTint;
        public int fireflies;
    }

    static readonly Theme[] Themes =
    {
        // Palm Bay: open and flowing, the shape the game shipped with. Gentle dunes on an island that falls to a
        // sandy shore just past the treeline (the shoreline lands at r ~410, under the water shader's surf ring).
        new()
        {
            id = "beach", radius = 215f, halfWidth = 10.0f, ampA = 0.10f, harmA = 2, ampB = 0.06f, harmB = 3, controlPoints = 12, seed = 3,
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                return 3.2f + 4.8f * TerrainGen.Fbm(x, z, 1f / 120f, 3) - 15f * TerrainGen.Step(395f, 445f, r);
            },
            palette = BeachPalette, ground = new Color(0.78f, 0.68f, 0.46f), sea = -0.9f,
            // After dark, the teaser's coast: the moonrise sky turned so the moon hangs over the sea ahead-left of the
            // start straight, the same moon lighting everything, and fog in the sky's own horizon colour.
            // The cube's centre column faces +Z and _Rotation turns the sky the other way: the image's moon (36 deg
            // right of centre) lands at yaw 30, and the moonlight comes from there.
            night = true, sky = "skybox-moonrise", skyRotation = 6f, skyExposure = 0.9f,
            // (a full moon: bright enough that the sand, the palms and the town read between the lamps)
            sun = new Vector3(26, -150, 0), sunColor = new Color(0.62f, 0.72f, 1f), sunIntensity = 1.0f,
            ambientSky = new Color(0.28f, 0.33f, 0.46f), ambientEquator = new Color(0.25f, 0.26f, 0.32f), ambientGround = new Color(0.11f, 0.10f, 0.10f),
            // the film's air is thick and a little warm from the lamps: the haze starts close and the far coast sinks into it
            fog = new Color(0.24f, 0.25f, 0.29f), fogStart = 22, fogEnd = 330, farClip = 470,
            water = "Water_night", wet = 1f, lampEvery = 36f, studs = true, lighthouse = true, tunnel = true, groundDetail = "sand_diff",
            post = p =>
            {
                // The teaser's grade: filmic roll-off, teal shadows under warm lamps, bloom on every practical, a
                // little grain and fringe. The base profile (LookSetup) carries every component; this only retunes.
                if (p.TryGet(out Tonemapping tone)) tone.mode.value = TonemappingMode.ACES;
                if (p.TryGet(out ColorAdjustments g)) { g.postExposure.value = 1.0f; g.contrast.value = 12f; g.saturation.value = 10f; g.colorFilter.value = new Color(0.94f, 0.97f, 1f); }
                if (p.TryGet(out SplitToning split)) { split.shadows.value = new Color(0.10f, 0.36f, 0.52f); split.highlights.value = new Color(1f, 0.80f, 0.58f); split.balance.value = -12f; }
                if (p.TryGet(out Bloom bloom)) { bloom.threshold.value = 0.8f; bloom.intensity.value = 1.5f; bloom.scatter.value = 0.76f; }
                if (p.TryGet(out Vignette vignette)) vignette.intensity.value = 0.34f;
                if (p.TryGet(out FilmGrain grain)) grain.intensity.value = 0.22f;
                if (p.TryGet(out ChromaticAberration fringe)) fringe.intensity.value = 0.06f;
            },
        },
        // Frost Peak: the hairpin layout in a valley floor that rolls 8-15 m, with mountains from r ~290 out.
        new()
        {
            id = "alpine", radius = 160f, halfWidth = 9.0f, ampA = 0.34f, harmA = 2, ampB = 0.06f, harmB = 5, controlPoints = 16, seed = 11,
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float valley = 8f + 7.5f * TerrainGen.Fbm(x, z, 1f / 150f, 3, 11f);
                float m = TerrainGen.Step(285f, 520f, r);
                return valley + m * (70f + 45f * TerrainGen.Ridged(x, z, 1f / 160f, 5f) + 8f * TerrainGen.Fbm(x, z, 1f / 45f, 2, 7f));
            },
            palette = AlpinePalette, ground = new Color(0.86f, 0.88f, 0.94f),
            // Blue hour: the sun is gone behind the range, the snow holds the sky's blue, and the road is a string of
            // warm lamps through it. Cold everywhere except where there is light: that contrast is the map.
            sun = new Vector3(14, -60, 0), sunColor = new Color(0.72f, 0.80f, 1f), sunIntensity = 0.6f,
            skyTint = new Color(0.16f, 0.24f, 0.55f), horizon = new Color(0.62f, 0.52f, 0.66f), exposure = 0.7f, atmosphere = 1.3f, sunSize = 0.03f, sunConvergence = 5f,
            ambientSky = new Color(0.24f, 0.32f, 0.56f), ambientEquator = new Color(0.30f, 0.34f, 0.52f), ambientGround = new Color(0.20f, 0.24f, 0.38f),
            fog = new Color(0.34f, 0.40f, 0.62f), fogStart = 35, fogEnd = 400, farClip = 520,
            // (the lamps at 0.55: on snow a full sodium lamp blows the road and the kart under it to white)
            lampEvery = 38f, lampColor = new Color(0.55f, 0.34f, 0.16f), studs = true, studColor = new Color(0.75f, 0.92f, 1f), wet = 0.45f, headlights = true, roadTint = 1.5f,
            post = p =>
            {
                if (p.TryGet(out Tonemapping tone)) tone.mode.value = TonemappingMode.ACES;
                if (p.TryGet(out ColorAdjustments g)) { g.postExposure.value = 0.95f; g.contrast.value = 12f; g.saturation.value = 14f; g.colorFilter.value = new Color(0.95f, 0.97f, 1f); }
                if (p.TryGet(out SplitToning split)) { split.shadows.value = new Color(0.14f, 0.28f, 0.62f); split.highlights.value = new Color(1f, 0.82f, 0.60f); split.balance.value = -10f; }
                if (p.TryGet(out Bloom bloom)) { bloom.threshold.value = 0.8f; bloom.intensity.value = 1.5f; bloom.scatter.value = 0.76f; }
                if (p.TryGet(out Vignette vignette)) vignette.intensity.value = 0.32f;
                if (p.TryGet(out FilmGrain grain)) grain.intensity.value = 0.2f;
            },
        },
        // Pine Ridge: tight and technical. Gentle inside the loop, strong ridged hills outside it, and a lake in the
        // bay between the two lobes at 18 and 90 degrees (the smallest-radius bay of the five-lobe shape).
        new()
        {
            id = "forest", radius = 118f, halfWidth = 7.5f, ampA = 0.30f, harmA = 5, ampB = 0.12f, harmB = 3, controlPoints = 20, seed = 23,
            lake = new Vector2(121f, 88f), lakeRadius = 48f, sea = float.NaN,
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float hills = 10f + 9f * TerrainGen.Fbm(x, z, 1f / 160f, 3, 23f);
                hills += TerrainGen.Step(170f, 330f, r) * (18f + 14f * TerrainGen.Ridged(x, z, 1f / 220f, 3f));
                float basin = 1f - TerrainGen.Step(46f, 96f, Vector2.Distance(new Vector2(x, z), new Vector2(121f, 88f)));
                return Mathf.Lerp(hills, -2.2f, basin);
            },
            palette = ForestPalette, ground = new Color(0.36f, 0.58f, 0.30f),
            // Golden mist: a low sun through thick air, so the trees ahead are shapes before they are trees, the
            // headlights cut beams in it, amber studs mark the road and fireflies hang in the wood either side.
            sun = new Vector3(11, -30, 0), sunColor = new Color(1f, 0.76f, 0.48f), sunIntensity = 1.15f,
            skyTint = new Color(0.62f, 0.60f, 0.50f), horizon = new Color(0.92f, 0.80f, 0.58f), exposure = 1.05f, atmosphere = 1.5f, sunSize = 0.07f, sunConvergence = 3f,
            ambientSky = new Color(0.44f, 0.46f, 0.40f), ambientEquator = new Color(0.46f, 0.44f, 0.30f), ambientGround = new Color(0.16f, 0.18f, 0.10f),
            fog = new Color(0.80f, 0.72f, 0.50f), fogStart = 10, fogEnd = 190, farClip = 260,
            studs = true, studColor = new Color(1f, 0.72f, 0.25f), wet = 0.35f, headlights = true, roadTint = 1.1f, fireflies = 320,
            post = p =>
            {
                if (p.TryGet(out Tonemapping tone)) tone.mode.value = TonemappingMode.ACES;
                if (p.TryGet(out ColorAdjustments g)) { g.postExposure.value = 1.0f; g.contrast.value = 10f; g.saturation.value = 8f; }
                if (p.TryGet(out SplitToning split)) { split.shadows.value = new Color(0.20f, 0.36f, 0.30f); split.highlights.value = new Color(1f, 0.86f, 0.55f); split.balance.value = -6f; }
                if (p.TryGet(out Bloom bloom)) { bloom.threshold.value = 0.72f; bloom.intensity.value = 1.7f; bloom.scatter.value = 0.82f; }
                if (p.TryGet(out Vignette vignette)) vignette.intensity.value = 0.30f;
                if (p.TryGet(out FilmGrain grain)) grain.intensity.value = 0.2f;
            },
        },
        // Sunset Speedway: a stadium oval, near-flat infield, an outer bank so the stands have a backdrop. Measured
        // like the others: lap 1279 m, longest straight 216 m, 9 deg/sample at the chicane, 21 % of samples straight.
        new()
        {
            id = "speedway", radius = 120f, straight = 260f, chicane = 9f, halfWidth = 11.0f, controlPoints = 24, seed = 41,
            shape = (x, z) =>
            {
                float re = Ellipse(x, z);
                return 2f + 2f * TerrainGen.Fbm(x, z, 1f / 120f, 2, 41f) + TerrainGen.Step(1.25f, 1.7f, re) * (25f + 4f * TerrainGen.Fbm(x, z, 1f / 60f, 2, 9f));
            },
            palette = SpeedwayPalette, ground = new Color(0.34f, 0.56f, 0.30f),
            // Low enough to read as dusk, high enough that a flat road still catches it: at 9-11 deg the ground went black.
            // (the sky used to be an acid yellow: a thick atmosphere under a 17 deg sun. Lower and thinner, it runs
            // orange at the horizon into violet overhead; the terrain's wrapped lambert keeps the infield readable.)
            sun = new Vector3(7, -120, 0), sunColor = new Color(1f, 0.60f, 0.36f), sunIntensity = 1.45f,
            skyTint = new Color(0.46f, 0.30f, 0.62f), horizon = new Color(0.42f, 0.22f, 0.36f), exposure = 1.0f, atmosphere = 1.15f, sunSize = 0.07f, sunConvergence = 6f,
            ambientSky = new Color(0.30f, 0.30f, 0.52f), ambientEquator = new Color(0.56f, 0.36f, 0.40f), ambientGround = new Color(0.24f, 0.17f, 0.22f),
            fog = new Color(0.34f, 0.19f, 0.32f), fogStart = 70, fogEnd = 420, farClip = 450,
            // Magenta dusk under the floodlights: a wet oval that mirrors the sky, hot-pink studs, every floodlight haloed.
            studs = true, studColor = new Color(1f, 0.22f, 0.55f), wet = 0.6f, headlights = true, roadTint = 1.2f,
            post = p =>
            {
                // The camera is LDR, so an HDR lamp head clamps to white: the only way it blooms is a threshold under 1.
                // (not ACES here: it pulled the orange sky to an acid yellow)
                if (p.TryGet(out Bloom bloom)) { bloom.threshold.value = 0.8f; bloom.intensity.value = 1.5f; bloom.scatter.value = 0.76f; }
                if (p.TryGet(out ColorAdjustments g)) { g.postExposure.value = 0.88f; g.contrast.value = 16f; }
                if (p.TryGet(out SplitToning split)) { split.highlights.value = new Color(1f, 0.74f, 0.46f); split.shadows.value = new Color(0.28f, 0.20f, 0.50f); }
            },
        },
    };

    /// Normalised elliptical distance around the stadium (1 = the far turns), for the bank and the dark outfield.
    static float Ellipse(float x, float z) => Mathf.Sqrt(x * x / (250f * 250f) + z * z / (150f * 150f));

    // ---------- palettes: two close shades per surface, chosen by a low-frequency mottle, so faces vary ----------
    static Color Pick(TerrainGen.Face f, float freq, Color a, Color b) =>
        Jitter(Mathf.PerlinNoise(f.p.x * freq + 91.3f, f.p.z * freq + 17.7f) > 0.5f ? a : b, f.jitter);
    static Color Jitter(Color c, float j) => c * (0.975f + 0.05f * j);
    static Color C(float r, float g, float b) => new(r, g, b);

    static Color BeachPalette(TerrainGen.Face f)
    {
        if (f.p.y < -0.4f) return Pick(f, 0.02f, C(0.55f, 0.52f, 0.42f), C(0.50f, 0.48f, 0.40f));          // sea bed
        if (f.p.y < 1.2f) return Pick(f, 0.02f, C(0.70f, 0.62f, 0.44f), C(0.66f, 0.58f, 0.42f));           // damp sand at the shore
        if (f.road > 14f && f.p.y > 2.5f && Mathf.PerlinNoise(f.p.x * 0.011f + 3f, f.p.z * 0.011f + 8f) > 0.60f)
            return Pick(f, 0.03f, C(0.44f, 0.62f, 0.30f), C(0.38f, 0.56f, 0.27f));                          // grass patches inland
        return Pick(f, 0.02f, C(0.78f, 0.68f, 0.46f), C(0.74f, 0.64f, 0.43f));
    }

    static Color AlpinePalette(TerrainGen.Face f)
    {
        float r = Mathf.Sqrt(f.p.x * f.p.x + f.p.z * f.p.z);
        if (f.slope > 35f || (r > 470f && f.slope > 22f)) return Pick(f, 0.02f, C(0.42f, 0.40f, 0.44f), C(0.38f, 0.37f, 0.41f));   // rock
        if (f.road < V.halfWidth + Kerb + 3f) return Jitter(C(0.80f, 0.82f, 0.87f), f.jitter);                                  // packed snow at the shoulder
        if (f.p.y < 14f && f.slope < 12f && Mathf.PerlinNoise(f.p.x * 0.014f + 5f, f.p.z * 0.014f + 2f) > 0.55f)
            return Pick(f, 0.03f, C(0.72f, 0.78f, 0.88f), C(0.68f, 0.75f, 0.86f));                                                // blue-shadowed floor
        return Pick(f, 0.02f, C(0.86f, 0.88f, 0.94f), C(0.82f, 0.85f, 0.92f));
    }

    static Color ForestPalette(TerrainGen.Face f)
    {
        if (f.p.y < -0.6f) return Pick(f, 0.03f, C(0.40f, 0.34f, 0.26f), C(0.36f, 0.31f, 0.24f));           // lake bed
        if (f.slope > 38f) return Pick(f, 0.02f, C(0.44f, 0.42f, 0.40f), C(0.40f, 0.38f, 0.37f));           // rock
        if (f.slope > 24f) return Pick(f, 0.02f, C(0.50f, 0.40f, 0.28f), C(0.46f, 0.37f, 0.26f));           // dirt on the banks
        if (f.slope < 20f && Mathf.PerlinNoise(f.p.x * 0.009f + 9f, f.p.z * 0.009f + 4f) > 0.74f)
            return Pick(f, 0.03f, C(0.76f, 0.46f, 0.18f), C(0.70f, 0.40f, 0.16f));                          // autumn patches, sparingly
        return Pick(f, 0.02f, C(0.36f, 0.58f, 0.30f), C(0.30f, 0.52f, 0.26f));
    }

    static Color SpeedwayPalette(TerrainGen.Face f)
    {
        if (f.road < V.halfWidth + Kerb + 7.5f) return Pick(f, 0.05f, C(0.24f, 0.24f, 0.27f), C(0.22f, 0.22f, 0.25f));   // asphalt apron
        if (Ellipse(f.p.x, f.p.z) > 1.25f) return Pick(f, 0.02f, C(0.16f, 0.19f, 0.17f), C(0.14f, 0.17f, 0.15f));        // the dark bank
        int band = Mathf.FloorToInt((f.p.x * 0.94f + f.p.z * 0.34f) / 12f);                                               // mown stripes
        return Jitter((band & 1) == 0 ? C(0.34f, 0.56f, 0.30f) : C(0.29f, 0.50f, 0.26f), f.jitter);
    }
}
