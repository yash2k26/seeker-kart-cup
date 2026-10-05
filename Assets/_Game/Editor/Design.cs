using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// The design system. Every colour, corner and typeface in the game comes from here, so the rules below are
/// checkable rather than aspirational.
///
///   BRAND     The film's lockup is the brand: a gold wordmark on a deep violet shell, cream and gold words under it
///             (teaser/graphics/kartel2_logo.py, the "gold" look). The palette below is read off it.
///   TYPE      Archivo, the film's name-card face: condensed black for display, bold and semibold for everything a
///             player reads or counts. One display size per screen. The wordmark itself is the film's image.
///   ACCENT    Gold (#FFB126, the wordmark's face) is THE accent. It marks the primary action and nothing else, and
///             words on it are OnAccent (the violet), never Paper: cream on gold cannot be read.
///   INK       One violet family, Ink900 -> Ink600, from the lockup's shell. No greys beside it.
///   SHAPE     Actions are pills. Cards are 32. Rows, meters and chips are 12. Nothing else.
///   SPACING   8-unit base on the 2400x1080 reference canvas.
public static class Design
{
    // ---- palette: one accent, one grey family ----
    public static readonly Color Ink900 = Hex("0D0326");   // scrim behind a modal (the shell's side)
    public static readonly Color Ink800 = Hex("170540");   // card surface (the shell's shadowed front)
    public static readonly Color Ink700 = Hex("2E0B70");   // row / secondary action (the shell's front)
    public static readonly Color Ink600 = Hex("4B25B0");   // pressed secondary, hairlines (toward the shell's lit edge, #7446FF)
    public static readonly Color Paper = Hex("F4EFE6");    // primary text (the tagline's cream)
    public static readonly Color Muted = Hex("B4A7DE");    // secondary text, same family as the inks
    public static readonly Color Accent = Hex("FFB126");   // THE accent (the wordmark's gold)
    public static readonly Color OnAccent = Hex("170540"); // words on the accent

    // Drift tiers are a gameplay signal, not chrome: a charge ramp the player reads at speed.
    public static readonly Color[] Tiers = { Hex("FF5A36"), Hex("FFB03A"), Hex("FFFFFF") };

    // ---- type ----
    // Archivo, the family the teaser is set in (tools/fonts/make_archivo.py cuts the static files). Display is its
    // condensed black, as narrow as the Anton every title and button was drawn for; Strong and Body are the text
    // weights; Hero is the film's own lockup face (expanded black italic), for the wordmark and the one big line.
    public static TMP_FontAsset Display, Body, Strong, Hero;

    const string FontDir = "Assets/_Game/UI/Fonts/";
    const string SpriteDir = "Assets/_Game/Art/Textures/UI/Gen/";

    public static void Fonts()
    {
        Display = MakeFont("Archivo-CondensedBlack", 96);
        Strong = MakeFont("Archivo-Bold", 78);
        Body = MakeFont("Archivo-SemiBold", 78);
        Hero = MakeFont("Archivo-ExpandedBlackItalic", 96);
    }

    static TMP_FontAsset MakeFont(string ttf, int samplingSize)
    {
        var path = FontDir + ttf + " SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(FontDir + ttf + ".ttf");
        if (source == null) { Debug.LogError("[design] missing font " + ttf); return null; }
        var asset = TMP_FontAsset.CreateFontAsset(source, samplingSize, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        asset.name = ttf + " SDF";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    // ---- shapes: generated, so the radius scale is enforced by construction ----
    public const int PillSize = 128, PillRadius = 64;   // a true pill needs its border scaled to half the button height
    public const int CardRadius = 32, RowRadius = 12;

    public static Sprite Pill => RoundedRect("pill", PillSize, PillRadius);
    public static Sprite Card => RoundedRect("card", 96, CardRadius);
    public static Sprite Row => RoundedRect("row", 48, RowRadius);

    /// White rounded square with an antialiased edge, imported as a 9-slice. Tinted at the point of use, so the
    /// whole UI is three textures instead of twenty-two button PNGs.
    static Sprite RoundedRect(string name, int size, int radius)
    {
        string path = SpriteDir + $"ui_{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f, inner = half - radius;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // signed distance to a rounded box, so the corner is smooth at any scale
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
        tex.Apply();
        Directory.CreateDirectory(SpriteDir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = Vector4.one * (radius + 1);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- depth: an action looks like something you can press ----
    // A button is its face over a darker lip (the face's own colour, darker), the way a key sits on its switch. On
    // press the face drops into the lip (the Down sprite, swapped by PressFeel). Both are white and tinted at the
    // point of use like every other shape, so gold stays gold and the lip is the same gold in shadow.
    public const int Lip = 14, TileRadius = 28, TileSize = 96, TileLip = 10;

    public static Sprite PillUp => DepthRect("pill_up", PillSize, PillRadius, Lip, Lip);
    public static Sprite PillDown => DepthRect("pill_down", PillSize, PillRadius, Lip, 4);
    public static Sprite TileUp => DepthRect("tile_up", TileSize, TileRadius, TileLip, TileLip);
    public static Sprite TileDown => DepthRect("tile_down", TileSize, TileRadius, TileLip, 3);
    /// A card shaded from its top (lit) to its bottom, and the lit rim that goes round it (the lockup's shell edge).
    public static Sprite CardShade => Shaded("card_shade", 96, CardRadius);
    public static Sprite CardEdge => Edge("card_edge", 96, CardRadius, 3f);
    public static Sprite TileEdge => Edge("tile_edge", TileSize, TileRadius, 5f);
    public static Sprite RingEdge => Edge("ring_edge", 128, 64, 6f);
    public static Sprite Glow => Radial("glow", 128);
    public static Sprite Band => ShineBand("shine", 64);
    public static readonly Color Lit = Hex("7446FF");      // the shell's lit edge: rims, an ON switch, never an action

    /// White rounded box over its lip: `lip` sprite pixels of darker band under a face of `size`, the face raised
    /// `raise` pixels off the bottom (the full lip when up, almost none when pressed). The face shades from its top to
    /// its bottom so a flat tint reads as a rounded key. Sliced with the lip in the bottom border, so it never stretches.
    static Sprite DepthRect(string name, int size, int radius, int lip, int raise)
    {
        return Generated(name, size, size + lip, new Vector4(radius, radius + lip, radius, radius), (x, y) =>
        {
            float fa = Coverage(x, y - raise, size, radius);          // the face, raised off the bottom
            float la = Coverage(x, y, size, radius);                  // the lip: the same shape at the bottom
            float shade = Mathf.Lerp(0.86f, 1f, Mathf.Clamp01((y - raise) / (float)size));
            float a = fa + la * (1f - fa);
            if (a <= 0f) return new Color(1f, 1f, 1f, 0f);
            float c = (shade * fa + 0.52f * la * (1f - fa)) / a;
            return new Color(c, c, c, a);
        });
    }

    static Sprite Shaded(string name, int size, int radius) =>
        Generated(name, size, size, Vector4.one * (radius + 1), (x, y) =>
        {
            float c = Mathf.Lerp(0.78f, 1f, y / (size - 1f));
            return new Color(c, c, c, Coverage(x, y, size, radius));
        });

    /// A rim `width` pixels wide inside the rounded box, brightest along the top: light from above.
    static Sprite Edge(string name, int size, int radius, float width) =>
        Generated(name, size, size, Vector4.one * (radius + 1), (x, y) =>
        {
            float outer = Coverage(x, y, size, radius);
            float inner = CoverageInset(x, y, size, radius, width);
            float fall = Mathf.Lerp(0.35f, 1f, y / (size - 1f));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(outer - inner) * fall);
        });

    static Sprite Radial(string name, int size) =>
        Generated(name, size, size, Vector4.zero, (x, y) =>
        {
            float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        });

    /// A soft vertical band, clear at both sides: the light that sweeps over the primary button.
    static Sprite ShineBand(string name, int size) =>
        Generated(name, size, size, Vector4.zero, (x, y) =>
        {
            float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
            return new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, u) * 0.55f);
        });

    /// Antialiased coverage of a `size` square with corners of `radius`, at pixel (x, y).
    static float Coverage(int x, int y, int size, int radius) => CoverageInset(x, y, size, radius, 0f);

    static float CoverageInset(int x, int y, int size, int radius, float inset)
    {
        float half = size * 0.5f, r = Mathf.Max(0f, radius - inset), inner = half - inset - r;
        float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
        float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
        if (inner < 0f) return 0f;
        return Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - r));
    }

    /// Writes a generated white sprite once (Gen/ui_<name>.png), sliced by `border`, and reuses it after.
    static Sprite Generated(string name, int w, int h, Vector4 border, System.Func<int, int, Color> pixel)
    {
        string path = SpriteDir + $"ui_{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, pixel(x, y));
        tex.Apply();
        Directory.CreateDirectory(SpriteDir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- icons: drawn here as distance fields, so they stay sharp at any size (the kit's are 48 px) ----
    /// White glyph on clear, 128 px, made once into Gen/Icons. play, back, next, globe, trophy, kart, user, flag; the
    /// race items (rocket, homing, bomb, nitro, bouncer, sunbomb, trap); arc (the loading spinner).
    public static readonly string[] Drawn = { "play", "back", "next", "globe", "trophy", "kart", "user", "flag",
                                              "rocket", "homing", "bomb", "nitro", "bouncer", "sunbomb", "trap", "arc", "download" };
    /// The race items' glyphs by ItemManager name.
    public static string ItemGlyph(string item) => item switch
    {
        "Rocket" => "rocket", "Homing Item" => "homing", "Bomb" => "bomb", "Nitro" => "nitro",
        "Bouncer" => "bouncer", "Sun Bomb" => "sunbomb", "Trap" => "trap", _ => null,
    };
    public static Sprite Icon(string name)
    {
        const int S = 128;
        System.Func<float, float, float> sdf = name switch
        {
            "play" => (x, y) => Tri(x, y, new Vector2(44, 28), new Vector2(44, 100), new Vector2(100, 64)) - 6f,
            "back" => (x, y) => Mathf.Min(Seg(x, y, 74, 30, 42, 64, 11), Seg(x, y, 42, 64, 74, 98, 11)),
            "next" => (x, y) => Mathf.Min(Seg(x, y, 54, 30, 86, 64, 11), Seg(x, y, 86, 64, 54, 98, 11)),
            "globe" => (x, y) => Mathf.Min(Mathf.Min(Ring(x, y, 64, 64, 46, 6), RingEllipse(x, y, 64, 64, 20, 46, 6)),
                                      Mathf.Min(Seg(x, y, 20, 64, 108, 64, 6), Mathf.Min(Seg(x, y, 30, 88, 98, 88, 5), Seg(x, y, 30, 40, 98, 40, 5)))),
            "trophy" => (x, y) => Mathf.Min(Mathf.Min(Cup(x, y), Mathf.Min(Ring(x, y, 34, 86, 13, 7), Ring(x, y, 94, 86, 13, 7))),
                                       Mathf.Min(Box(x, y, 64, 44, 7, 14, 2), Box(x, y, 64, 24, 26, 8, 5))),
            "kart" => (x, y) => Mathf.Min(Mathf.Min(Box(x, y, 64, 52, 46, 14, 10), Circle(x, y, 60, 82, 15)),
                                     Mathf.Min(Mathf.Min(Circle(x, y, 30, 34, 15), Circle(x, y, 98, 34, 15)), Box(x, y, 98, 72, 6, 14, 3))),
            "user" => (x, y) => Mathf.Min(Circle(x, y, 64, 84, 22), Box(x, y, 64, 30, 38, 22, 20)),
            "flag" => (x, y) => Mathf.Min(Box(x, y, 34, 64, 5, 46, 4), Flag(x, y)),
            // the race items, one glyph each (the HUD's item slot, the shop's rows)
            "rocket" => (x, y) => Mathf.Max(Mathf.Min(Mathf.Min(Seg(x, y, 42, 42, 88, 88, 28), Circle(x, y, 30, 30, 9)),
                                                     Mathf.Min(Seg(x, y, 35, 49, 16, 51, 11), Seg(x, y, 49, 35, 51, 16, 11))),
                                            -Circle(x, y, 71, 71, 6)),
            "homing" => (x, y) => Mathf.Min(Mathf.Min(Ring(x, y, 64, 64, 40, 9), Circle(x, y, 64, 64, 10)),
                                       Mathf.Min(Mathf.Min(Seg(x, y, 64, 4, 64, 30, 9), Seg(x, y, 64, 98, 64, 124, 9)),
                                                 Mathf.Min(Seg(x, y, 4, 64, 30, 64, 9), Seg(x, y, 98, 64, 124, 64, 9)))),
            "bomb" => (x, y) => Mathf.Min(Mathf.Max(Circle(x, y, 58, 54, 38), -Circle(x, y, 44, 68, 8)),
                                     Mathf.Min(Seg(x, y, 82, 80, 96, 98, 9), Mathf.Min(Seg(x, y, 92, 108, 112, 108, 6), Seg(x, y, 102, 98, 102, 118, 6)))),
            "nitro" => (x, y) => Mathf.Min(Tri(x, y, new Vector2(76, 120), new Vector2(32, 56), new Vector2(72, 56)),
                                      Tri(x, y, new Vector2(56, 72), new Vector2(50, 6), new Vector2(98, 72))) - 2f,
            "bouncer" => (x, y) => Mathf.Min(Mathf.Min(Circle(x, y, 70, 48, 28), Mathf.Max(Ring(x, y, 70, 48, 44, 8), 76f - y)),
                                        Mathf.Min(Seg(x, y, 10, 56, 30, 56, 8), Seg(x, y, 16, 38, 32, 38, 8))),
            "sunbomb" => (x, y) => Mathf.Min(Circle(x, y, 64, 64, 28), Rays(x, y)),
            "trap" => (x, y) => Mathf.Min(Circle(x, y, 64, 64, 14),
                                     Mathf.Min(Seg(x, y, 20, 64, 108, 64, 13), Mathf.Min(Seg(x, y, 42, 26, 86, 102, 13), Seg(x, y, 42, 102, 86, 26, 13)))),
            "arc" => (x, y) => Arc(x, y),
            "download" => (x, y) => Mathf.Min(Mathf.Min(Seg(x, y, 64, 108, 64, 46, 13), Mathf.Min(Seg(x, y, 38, 70, 64, 44, 13), Seg(x, y, 90, 70, 64, 44, 13))),
                                         Box(x, y, 64, 20, 46, 7, 4)),
            _ => null,
        };
        if (sdf == null) { Debug.LogError("[design] no icon " + name); return null; }
        var dir = SpriteDir + "Icons/";
        Directory.CreateDirectory(dir);
        string path = dir + $"icon_{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f))));
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;                    // shown from 40 to 120 px
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float Circle(float x, float y, float cx, float cy, float r) => new Vector2(x - cx, y - cy).magnitude - r;
    static float Ring(float x, float y, float cx, float cy, float r, float w) => Mathf.Abs(Circle(x, y, cx, cy, r)) - w * 0.5f;
    static float RingEllipse(float x, float y, float cx, float cy, float rx, float ry, float w)
    {
        // a ring stretched to an ellipse: distance scaled back by the axis it was squeezed along (close enough at this size)
        var p = new Vector2((x - cx) / rx, (y - cy) / ry);
        return Mathf.Abs(p.magnitude - 1f) * Mathf.Min(rx, ry) - w * 0.5f;
    }
    static float Seg(float x, float y, float ax, float ay, float bx, float by, float w)
    {
        var p = new Vector2(x - ax, y - ay); var d = new Vector2(bx - ax, by - ay);
        float t = Mathf.Clamp01(Vector2.Dot(p, d) / d.sqrMagnitude);
        return (p - d * t).magnitude - w * 0.5f;
    }
    static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
    {
        float dx = Mathf.Abs(x - cx) - hw + r, dy = Mathf.Abs(y - cy) - hh + r;
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
    }
    static float Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        // signed distance to a triangle (inside negative): the nearest edge, signed by which side of all three it is on
        var p = new Vector2(x, y);
        float Edge(Vector2 u, Vector2 v) { var e = v - u; var w = p - u; return (w - e * Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude)).magnitude; }
        float Side(Vector2 u, Vector2 v) => (v.x - u.x) * (p.y - u.y) - (v.y - u.y) * (p.x - u.x);
        float d = Mathf.Min(Edge(a, b), Mathf.Min(Edge(b, c), Edge(c, a)));
        float s1 = Side(a, b), s2 = Side(b, c), s3 = Side(c, a);
        bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        return inside ? -d : d;
    }
    // the trophy's bowl: a half disc on a short box, the rim flat along the top
    static float Cup(float x, float y) => Mathf.Max(Mathf.Min(Circle(x, y, 64, 80, 30), Box(x, y, 64, 95, 30, 15, 2)), y - 110f);
    // eight rays round the sun bomb's face
    static float Rays(float x, float y)
    {
        float d = float.MaxValue;
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI / 4f, c = Mathf.Cos(a), s = Mathf.Sin(a);
            d = Mathf.Min(d, Seg(x, y, 64 + c * 38, 64 + s * 38, 64 + c * 56, 64 + s * 56, 10));
        }
        return d;
    }
    // three quarters of a ring with round ends: the loading spinner
    static float Arc(float x, float y)
    {
        const float r = 48f, w = 13f;
        float a = Mathf.Atan2(y - 64f, x - 64f) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
        if (a <= 270f) return Ring(x, y, 64, 64, r, w);
        return Mathf.Min(Circle(x, y, 64 + r, 64, w * 0.5f), Circle(x, y, 64, 64 - r, w * 0.5f));
    }
    // a pennant off the pole: a slanted quad as two triangles
    static float Flag(float x, float y) => Mathf.Min(Tri(x, y, new Vector2(38, 108), new Vector2(102, 92), new Vector2(38, 66)),
                                                   Tri(x, y, new Vector2(38, 66), new Vector2(102, 92), new Vector2(98, 58))) - 2f;

    // ---- appliers ----
    /// A pill action. The border is scaled to half the height so the ends are true semicircles at any size.
    public static Image AsPill(RectTransform rt, Color tint)
    {
        var img = Fill(rt, Pill, tint);
        img.pixelsPerUnitMultiplier = PillRadius / Mathf.Max(1f, rt.sizeDelta.y * 0.5f);
        return img;
    }

    /// A pressable pill: the face over its lip. The slices are scaled so the face's ends stay semicircles; returns how
    /// tall the lip is in canvas units (the label sits that much higher, and sinks by it when pressed).
    public static Image AsButton(RectTransform rt, Color tint, out float lip)
    {
        var img = Fill(rt, PillUp, tint);
        float h = Mathf.Max(1f, rt.sizeDelta.y);
        img.pixelsPerUnitMultiplier = (PillSize + Lip) / h;
        lip = Lip * h / (PillSize + Lip);
        return img;
    }

    /// A pressable square-ish tile (portraits, track cards, icon tiles): corner 28 at 96 px, scaled with `corner`.
    public static Image AsTile(RectTransform rt, Color tint, float corner, out float lip)
    {
        var img = Fill(rt, TileUp, tint);
        img.pixelsPerUnitMultiplier = TileRadius / Mathf.Max(1f, corner);
        lip = TileLip / img.pixelsPerUnitMultiplier;
        return img;
    }

    /// A card: shaded top to bottom, with the lit rim round it as its first child (so it draws over the face and under
    /// everything put on the card after).
    public static Image AsCard(RectTransform rt, Color tint)
    {
        var img = Fill(rt, CardShade, tint);
        var edge = new GameObject("Edge", typeof(RectTransform));
        var ert = (RectTransform)edge.transform;
        ert.SetParent(rt, false);
        ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one; ert.offsetMin = Vector2.zero; ert.offsetMax = Vector2.zero;
        var e = edge.AddComponent<Image>();
        e.sprite = CardEdge; e.type = Image.Type.Sliced; e.raycastTarget = false;
        e.color = new Color(Lit.r, Lit.g, Lit.b, 0.55f);
        return img;
    }

    public static Image AsRow(RectTransform rt, Color tint) => Fill(rt, Row, tint);

    static Image Fill(RectTransform rt, Sprite sprite, Color tint)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = tint;
        return img;
    }

    static Color Hex(string h) => new(
        int.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
        int.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
        int.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f);
}
