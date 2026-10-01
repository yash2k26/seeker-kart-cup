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

    // ---- appliers ----
    /// A pill action. The border is scaled to half the height so the ends are true semicircles at any size.
    public static Image AsPill(RectTransform rt, Color tint)
    {
        var img = Fill(rt, Pill, tint);
        img.pixelsPerUnitMultiplier = PillRadius / Mathf.Max(1f, rt.sizeDelta.y * 0.5f);
        return img;
    }

    public static Image AsCard(RectTransform rt, Color tint) => Fill(rt, Card, tint);
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
