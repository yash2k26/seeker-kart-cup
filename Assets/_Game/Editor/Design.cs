using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// The design system. Every colour, corner and typeface in the game comes from here, so the rules below are
/// checkable rather than aspirational.
///
///   TYPE      Anton for display (condensed, motorsport), Barlow for everything a player reads or counts.
///             Two families, never mixed inside a word. One display size per screen.
///   ACCENT    Ember (#FF5A36) is THE accent. It marks the primary action and nothing else. The old gold is retired.
///   GREY      One cool blue-grey family, Ink900 -> Ink600. No warm greys anywhere.
///   SHAPE     Actions are pills. Cards are 32. Rows, meters and chips are 12. Nothing else.
///   SPACING   8-unit base on the 2400x1080 reference canvas.
public static class Design
{
    // ---- palette: one accent, one grey family ----
    public static readonly Color Ink900 = Hex("0A151C");   // scrim behind a modal
    public static readonly Color Ink800 = Hex("11242E");   // card surface
    public static readonly Color Ink700 = Hex("1B3643");   // row / secondary action
    public static readonly Color Ink600 = Hex("2A5060");   // pressed secondary, hairlines
    public static readonly Color Paper = Hex("F4F1EA");    // primary text
    public static readonly Color Muted = Hex("93AEBC");    // secondary text, same family as the inks
    public static readonly Color Ember = Hex("FF5A36");    // THE accent

    // Drift tiers are a gameplay signal, not chrome: a charge ramp the player reads at speed.
    public static readonly Color[] Tiers = { Hex("FF5A36"), Hex("FFB03A"), Hex("FFFFFF") };

    // ---- type ----
    public static TMP_FontAsset Display, Body, Strong;

    const string FontDir = "Assets/_Game/UI/Fonts/";
    const string SpriteDir = "Assets/_Game/Art/Textures/UI/Gen/";

    public static void Fonts()
    {
        Display = MakeFont("Anton-Regular", 96);
        Strong = MakeFont("Barlow-Bold", 78);
        Body = MakeFont("Barlow-SemiBold", 78);
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
