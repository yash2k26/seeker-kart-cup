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
        // the circuit: control points in metres, a closed loop run in their order, point 0 the start line on a straight
        // (tools/layouts/design.py lays them out, measures them and draws them before a scene is built)
        public Vector2[] layout;
        public float halfWidth;
        public int seed;
        // terrain: `shape` is the land (islands, the horizon); `road` the height the road rolls over, when it is not the land's
        public System.Func<float, float, float> shape, road;
        public TerrainGen.Palette palette;
        public Color ground;                   // the palette's mid tone, for the Simple Lit fallback
        // Off the road (the user, 11 Oct 2026: "the offroad area should be like ocean, or lava ... so that users don't try
        // to skip the racing track"): past a shoulder of `shoulder` metres the ground falls over `bank` metres into the
        // hazard, a sheet at hazardY that takes a kart out of the race (WaterBody, KartProgress). Land comes back no nearer
        // than `moat` past the shoulder, except behind a corner's barrier and round the start, where it meets the kerb.
        // NaN for none (the speedway walls its road instead).
        public float hazardY = float.NaN, shoulder = 3f, bank = 7f, moat = 16f;
        public bool lava, walls;
        public Color hazardDeep, hazardShallow;
        public string hazardWord = "SPLASH!";
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
        // Palm Bay: flowing sweepers and one hairpin round the lighthouse headland, on sand causeways across a lagoon.
        // The atoll's palm-lined rim closes it from r ~345 out and falls to the sea under the water shader's surf ring
        // (r ~410); keys of sand stand in the lagoon. 2.28 km, every corner wider than 27 m.
        new()
        {
            id = "beach", halfWidth = 10.0f, seed = 3,
            layout = L(-112,-265, 0,-265, 102,-265, 195,-242, 260,-177, 279,-93, 251,-19, 186,19, 158,84, 205,149, 270,186, 279,251, 214,288, 140,260,
                       84,186, 19,158, -56,205, -130,270, -223,260, -279,186, -270,93, -205,37, -140,-28, -186,-102, -242,-167, -205,-242),
            road = (x, z) => 1.4f + 2.6f * TerrainGen.Fbm(x, z, 1f / 140f, 2),
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float rim = TerrainGen.Step(330f, 352f, r) * (1f - TerrainGen.Step(405f, 440f, r));
                float key = TerrainGen.Step(0.22f, 0.34f, TerrainGen.Fbm(x, z, 1f / 85f, 2, 7f));
                float land = 1.2f + 2.4f * TerrainGen.Fbm(x, z, 1f / 120f, 3);
                return Mathf.Lerp(-4f, land, Mathf.Max(rim, key)) - 11f * TerrainGen.Step(440f, 500f, r);
            },
            palette = BeachPalette, ground = new Color(0.78f, 0.68f, 0.46f), hazardY = -0.9f, moat = 26f,   // keys well off the road: inside a bend it is lagoon
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
        // Frost Peak: switchbacks across a glacier lake, three legs stacked up the valley with S-bends in them and two
        // 19 m hairpins, then the fast run back down the far shore. Snowy rock islands in the black water, mountains
        // from r ~330 out. 2.29 km.
        new()
        {
            id = "alpine", halfWidth = 9.0f, seed = 11,
            layout = L(0,-240, 110,-242, 200,-215, 250,-155, 258,-90, 232,-48, 175,-35, 110,-55, 40,-30, -40,-10, -100,0, -130,25, -118,55, -70,68,
                       10,60, 90,85, 160,95, 200,120, 190,152, 140,165, 60,160, -20,185, -110,200, -200,215, -255,170, -270,90, -250,10, -275,-70,
                       -250,-160, -180,-225, -90,-242),
            road = (x, z) => 1.8f + 2.2f * TerrainGen.Fbm(x, z, 1f / 150f, 2, 11f) + 0.006f * (z + 240f),
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float isle = TerrainGen.Step(0.2f, 0.34f, TerrainGen.Fbm(x, z, 1f / 70f, 2, 13f));
                float floor = Mathf.Lerp(-4f, 2.5f + 4f * TerrainGen.Ridged(x, z, 1f / 50f, 2f), isle);
                float m = TerrainGen.Step(330f, 470f, r);
                return Mathf.Lerp(floor, 0f, m) + m * (60f + 45f * TerrainGen.Ridged(x, z, 1f / 160f, 5f) + 8f * TerrainGen.Fbm(x, z, 1f / 45f, 2, 7f));
            },
            palette = AlpinePalette, ground = new Color(0.86f, 0.88f, 0.94f), hazardY = 0f, hazardWord = "SPLASH!", water = "Water_ice",
            hazardDeep = new Color(0.02f, 0.06f, 0.11f), hazardShallow = new Color(0.20f, 0.42f, 0.55f),
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
        // Pine Ridge: tight and technical, on boardwalk-narrow causeways through a flooded forest: esses, a hairpin
        // pocket, a chicane, every bend 16-23 m. Pine islands in the swamp, ridged hills from r ~280 out. 1.79 km.
        new()
        {
            id = "forest", halfWidth = 7.5f, seed = 23,
            layout = L(0,-230, 90,-232, 160,-222, 195,-185, 180,-135, 215,-90, 180,-40, 215,10, 235,70, 205,125, 140,135, 90,105, 55,50, 15,40,
                       -5,85, -30,150, -100,195, -170,185, -215,140, -205,85, -150,55, -165,0, -225,-40, -250,-110, -215,-180, -140,-222, -70,-232),
            road = (x, z) => 1.6f + 2.4f * TerrainGen.Fbm(x, z, 1f / 120f, 2, 23f),
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float isle = TerrainGen.Step(0.22f, 0.34f, TerrainGen.Fbm(x, z, 1f / 60f, 2, 29f));
                float floor = Mathf.Lerp(-3.5f, 1.4f + 2f * TerrainGen.Fbm(x, z, 1f / 40f, 2, 5f), isle);
                float m = TerrainGen.Step(270f, 360f, r);
                return Mathf.Lerp(floor, 2f, m) + m * (14f + 16f * TerrainGen.Ridged(x, z, 1f / 220f, 3f));
            },
            palette = ForestPalette, ground = new Color(0.36f, 0.58f, 0.30f), hazardY = 0f, hazardWord = "SPLASH!", water = "Water_swamp",
            hazardDeep = new Color(0.03f, 0.06f, 0.06f), hazardShallow = new Color(0.12f, 0.20f, 0.17f),   // dark peaty water: it has to read as not-grass through the gold haze
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
        // Sunset Speedway: a roval. Out of the start onto the oval, the long banked sweep round turns one and two, the
        // back straight, then down off it into the infield: esses, a 21 m hairpin, a chicane and a 20 m hairpin back up
        // onto the front straight. Walled both sides the whole way. 2.31 km.
        new()
        {
            id = "speedway", halfWidth = 11.0f, seed = 41, walls = true,
            layout = L(0,-160, 130,-160, 240,-150, 320,-100, 345,0, 320,100, 240,150, 120,160, 0,160, -120,160, -210,150, -245,110, -235,60, -180,30,
                       -110,45, -45,20, 20,40, 85,15, 125,-25, 95,-70, 25,-72, -50,-58, -115,-88, -190,-88, -240,-100, -250,-140, -205,-162, -110,-162),
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
        // Mount Ember (the user, 11 Oct 2026: "would be really good to have a lava track"): a basalt road over a lake of
        // lava in a caldera, three lobes round the smoking cone in the middle. The long north lobe off the start, a
        // chicane and hairpin in the south-east one, a 9 m hairpin in the short south-west one. 1.96 km.
        new()
        {
            id = "volcano", halfWidth = 9.5f, seed = 53,
            layout = L(-61,182, -61,258, -46,324, 0,374, 46,324, 61,258, 61,182, 107,62, 187,-38, 223,-87, 284,-76, 279,-110, 300,-173, 235,-187,
                       194,-182, 127,-143, 0,-123, -127,-143, -194,-182, -213,-158, -272,-157, -244,-106, -254,-77, -187,-38, -107,62),
            road = (x, z) => 1.8f + 2.6f * TerrainGen.Fbm(x, z, 1f / 130f, 2, 53f),
            shape = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float isle = TerrainGen.Step(0.18f, 0.32f, TerrainGen.Fbm(x, z, 1f / 55f, 2, 61f));
                float floor = Mathf.Lerp(-4f, 1.2f + 5f * TerrainGen.Ridged(x, z, 1f / 35f, 4f), isle);
                // the cone: 70 m of it with a bowl of a crater at the top; and the caldera's wall round the lake
                float cone = Mathf.Max(0f, 72f - r * 0.82f) + 3f * TerrainGen.Fbm(x, z, 1f / 18f, 2, 3f);
                cone = Mathf.Lerp(50f, cone, TerrainGen.Step(6f, 16f, r));
                float wall = TerrainGen.Step(410f, 480f, r) * (45f + 30f * TerrainGen.Ridged(x, z, 1f / 120f, 8f));
                return Mathf.Max(floor, cone - 4f) + wall;
            },
            palette = VolcanoPalette, ground = new Color(0.16f, 0.14f, 0.14f), hazardY = 0f, lava = true, hazardWord = "TOO HOT!", water = "Lava",
            // the crust a hot red, the cracks between its plates past white-orange so they bloom
            hazardDeep = new Color(0.95f, 0.20f, 0.03f), hazardShallow = new Color(2.6f, 1.15f, 0.28f),
            // Dusk under the ash: a fiery sky, the lava lighting everything from below, embers in the air.
            sun = new Vector3(10, -40, 0), sunColor = new Color(1f, 0.55f, 0.32f), sunIntensity = 1.1f,
            skyTint = new Color(0.60f, 0.24f, 0.14f), horizon = new Color(0.90f, 0.38f, 0.12f), exposure = 0.95f, atmosphere = 1.6f, sunSize = 0.06f, sunConvergence = 4f,
            ambientSky = new Color(0.42f, 0.30f, 0.32f), ambientEquator = new Color(0.62f, 0.32f, 0.20f), ambientGround = new Color(0.80f, 0.30f, 0.08f),
            fog = new Color(0.46f, 0.20f, 0.11f), fogStart = 40, fogEnd = 420, farClip = 480,
            lampEvery = 44f, lampColor = new Color(1f, 0.55f, 0.22f),
            studs = true, studColor = new Color(1f, 0.55f, 0.12f), wet = 0.25f, headlights = true, roadTint = 1.25f,
            fireflies = 380, fireflyColor = new Color(1f, 0.42f, 0.08f),
            post = p =>
            {
                if (p.TryGet(out Tonemapping tone)) tone.mode.value = TonemappingMode.ACES;
                if (p.TryGet(out ColorAdjustments g)) { g.postExposure.value = 1.0f; g.contrast.value = 14f; g.saturation.value = 12f; g.colorFilter.value = new Color(1f, 0.96f, 0.92f); }
                if (p.TryGet(out SplitToning split)) { split.shadows.value = new Color(0.30f, 0.12f, 0.20f); split.highlights.value = new Color(1f, 0.70f, 0.40f); split.balance.value = -8f; }
                if (p.TryGet(out Bloom bloom)) { bloom.threshold.value = 0.75f; bloom.intensity.value = 1.8f; bloom.scatter.value = 0.8f; }
                if (p.TryGet(out Vignette vignette)) vignette.intensity.value = 0.34f;
                if (p.TryGet(out FilmGrain grain)) grain.intensity.value = 0.22f;
            },
        },
    };

    /// Normalised elliptical distance round the oval (about 1 at its turns), for the bank and the dark outfield.
    static float Ellipse(float x, float z) => Mathf.Sqrt((x - 47f) * (x - 47f) / (310f * 310f) + z * z / (175f * 175f));

    /// A layout as flat (x, z) pairs.
    static Vector2[] L(params float[] xz)
    {
        var v = new Vector2[xz.Length / 2];
        for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
        return v;
    }

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

    static Color VolcanoPalette(TerrainGen.Face f)
    {
        if (f.p.y < V.hazardY + 0.9f) return Pick(f, 0.05f, C(0.42f, 0.10f, 0.04f), C(0.36f, 0.08f, 0.04f));          // the scorched rim at the lava
        if (f.road < V.halfWidth + Kerb + V.shoulder + 1f) return Pick(f, 0.05f, C(0.24f, 0.22f, 0.21f), C(0.21f, 0.20f, 0.19f));   // packed ash at the shoulder
        if (f.slope > 30f) return Pick(f, 0.02f, C(0.13f, 0.12f, 0.12f), C(0.10f, 0.10f, 0.10f));                      // basalt faces
        if (Mathf.PerlinNoise(f.p.x * 0.02f + 4f, f.p.z * 0.02f + 9f) > 0.62f) return Pick(f, 0.04f, C(0.30f, 0.26f, 0.24f), C(0.27f, 0.24f, 0.22f));   // ash drifts
        return Pick(f, 0.03f, C(0.17f, 0.15f, 0.15f), C(0.15f, 0.13f, 0.13f));
    }

    static Color SpeedwayPalette(TerrainGen.Face f)
    {
        if (f.road < V.halfWidth + Kerb + 7.5f) return Pick(f, 0.05f, C(0.24f, 0.24f, 0.27f), C(0.22f, 0.22f, 0.25f));   // asphalt apron
        if (Ellipse(f.p.x, f.p.z) > 1.25f) return Pick(f, 0.02f, C(0.16f, 0.19f, 0.17f), C(0.14f, 0.17f, 0.15f));        // the dark bank
        int band = Mathf.FloorToInt((f.p.x * 0.94f + f.p.z * 0.34f) / 12f);                                               // mown stripes
        return Jitter((band & 1) == 0 ? C(0.34f, 0.56f, 0.30f) : C(0.29f, 0.50f, 0.26f), f.jitter);
    }
}
