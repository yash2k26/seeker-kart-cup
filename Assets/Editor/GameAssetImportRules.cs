using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ponytail: one file, path-prefix rules, no ScriptableObject config, no editor window.
// Scope is Assets/_Game/** only, so third-party packages keep their own settings.
// Bump k_Version when you change a rule -> Unity reimports everything this touches.
public sealed class GameAssetImportRules : AssetPostprocessor
{
    const uint k_Version = 6;
    public override uint GetVersion() => k_Version;

    const string Root = "Assets/_Game/";
    const string Android = "Android";

    bool In(string sub) => assetPath.StartsWith(Root + sub, System.StringComparison.Ordinal);
    bool Mine => assetPath.StartsWith(Root, System.StringComparison.Ordinal);
    // Kenney's Nature and Racing kits predate his colormaps: no texture, a few flat-colour FBX materials
    // (grass, dirt, woodBark...) and a submesh per material. Imported like the colormap kits they are grey boxes.
    bool FlatKit => In("Art/Models/Props/Nature") || In("Art/Models/Props/Racing");

    void OnPreprocessTexture()
    {
        if (!Mine) return;
        var t = (TextureImporter)assetImporter;
        t.isReadable = false;
        t.streamingMipmaps = false;
        t.anisoLevel = 0;
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        t.sRGBTexture = true;

        var ps = new TextureImporterPlatformSettings { name = Android, overridden = true, compressionQuality = 100 };

        if (In("Art/Textures/Colormaps"))
        {
            // These are palettes of solid colour bands, not pictures. Bilinear filtering, mipmaps and downscaling
            // all blend neighbouring bands together, which is what made the karts and drivers look muddy and
            // washed out. Point-sample them at native size and every UV lands on exactly the colour it was
            // authored with.
            t.textureType = TextureImporterType.Default;
            t.alphaSource = TextureImporterAlphaSource.None;
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            ps.maxTextureSize = 1024;
            ps.format = TextureImporterFormat.RGBA32;      // no block compression on a palette
        }
        else if (In("Art/Textures/UI/Kit"))
        {
            // Kenney's UI kit is drawn with rounded corners: a uniform 9-slice border keeps them crisp at any size.
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spriteBorder = new Vector4(24, 24, 24, 24);
            t.mipmapEnabled = false;
            ps.maxTextureSize = 512;
            ps.format = TextureImporterFormat.ASTC_4x4;
        }
        else if (In("Art/Textures/UI"))
        {
            t.textureType = TextureImporterType.Sprite;
            t.mipmapEnabled = false;                        // UI never minifies
            ps.maxTextureSize = 1024;
            // A smooth alpha ramp is exactly what ASTC bands. It is 8 KB uncompressed; keep it exact.
            ps.format = assetPath.Contains("plate_fade") ? TextureImporterFormat.RGBA32 : TextureImporterFormat.ASTC_4x4;
            if (In("Art/Textures/UI/Tracks"))
            {
                // Key art, shown full-bleed, and re-rendered by every track build. Single mode regenerates the sprite
                // rect from the texture each import; Multiple kept a rect from the first 1024-wide render, so the
                // sprite no longer matched its texture after the size changed and the select screen showed nothing.
                t.spriteImportMode = SpriteImportMode.Single;
                ps.maxTextureSize = 2048; ps.format = TextureImporterFormat.ASTC_6x6;
            }
        }
        else if (In("Art/Textures/Sky"))
        {
            t.textureShape = TextureImporterShape.TextureCube;   // 2:1 equirect -> Latitude-Longitude cube
            t.mipmapEnabled = false;                        // mips crease a skybox
            ps.maxTextureSize = 1024;
            ps.format = TextureImporterFormat.ASTC_6x6;
        }
        else // VFX + World
        {
            t.mipmapEnabled = true;
            t.alphaIsTransparency = true;
            if (In("Art/Textures/World") || In("Art/Textures/Kits")) t.wrapMode = TextureWrapMode.Repeat;   // water tiles, chevrons, fence net
            ps.maxTextureSize = 512;
            ps.format = TextureImporterFormat.ASTC_6x6;
        }
        t.SetPlatformTextureSettings(ps);
    }

    void OnPreprocessModel()
    {
        if (!Mine) return;
        var m = (ModelImporter)assetImporter;

        m.globalScale = 1f; m.useFileScale = true;   // Kenney FBX: Y-up, UnitScaleFactor 1.0
        m.bakeAxisConversion = false;
        m.importBlendShapes = false;
        m.importVisibility = false;
        m.importCameras = false;
        m.importLights = false;
        m.importConstraints = false;

        m.isReadable = false;                        // required for vertex compression
        m.meshCompression = ModelImporterMeshCompression.Off;
        m.meshOptimizationFlags = MeshOptimizationFlags.Everything;
        m.keepQuads = false;
        m.weldVertices = true;
        m.indexFormat = ModelImporterIndexFormat.UInt16;
        m.importNormals = ModelImporterNormals.Import;
        m.importBlendShapeNormals = ModelImporterNormals.None;
        m.importTangents = ModelImporterTangents.None;
        m.generateSecondaryUV = false;               // no lightmaps -> no UV2
        m.addCollider = false;

        // One shared material per colormap, authored by hand. Never import FBX materials — except the flat-colour
        // kits: None also suppresses OnAssignMaterialModel, and ImportStandard is what hands it the FBX colour.
        m.materialImportMode = FlatKit ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
        m.materialName = ModelImporterMaterialName.BasedOnMaterialName;

        // Kenney's Mini Characters (Art/Models/Drivers) are skinned and carry ~30 takes including "drive" and "sit".
        // Legacy keeps those playable by name with a plain Animation component — no controller asset per character.
        bool drivers = In("Art/Models/Drivers");
        bool rigged = drivers || In("Art/Models/Characters");
        m.animationType = drivers ? ModelImporterAnimationType.Legacy
                        : rigged ? ModelImporterAnimationType.Generic
                        : ModelImporterAnimationType.None;
        m.importAnimation = rigged;
        if (drivers) m.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        m.optimizeGameObjects = false;               // keep torso/arm-left/arm-right/head addressable
    }

    // One shared .mat per kit and FBX material name (Kits/Nature_grass.mat), so 300 props still batch by colour.
    // The source material is destroyed after this call, hence a fresh one rather than saving it.
    Material OnAssignMaterialModel(Material src, Renderer renderer)
    {
        if (!FlatKit) return null;
        const string Mats = Root + "Art/Materials/Kits/";
        string kit = Path.GetFileName(Path.GetDirectoryName(assetPath));
        string name = Regex.Replace(src.name, "[^A-Za-z0-9]", "");         // "_defaultMat"
        string path = $"{Mats}{kit}_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        // Born on Simple Lit with the spec-off flags LookSetup.Materials gives the KartCup/ ones; it never sees these.
        mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
        mat.SetColor("_BaseColor", src.color);                              // the FBX DiffuseColor
        mat.SetColor("_SpecColor", Color.black);
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.SetFloat("_Smoothness", 0f);

        // Racing names its three textured materials after their PNG (tankco, net, checkers). Keying on the name
        // rather than src.mainTexture means the result does not depend on Unity's texture search finding the file.
        string texPath = Root + "Art/Textures/Kits/" + name + ".png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            if (((TextureImporter)AssetImporter.GetAtPath(texPath)).DoesSourceTextureHaveAlpha())
            {
                // net.png is white on alpha: without clipping the fence is a solid white slab
                mat.SetFloat("_AlphaClip", 1f); mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);   // one panel, seen from both sides
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
        }
        Directory.CreateDirectory(Mats);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    void OnPreprocessAudio()
    {
        if (!Mine) return;
        var a = (AudioImporter)assetImporter;
        bool music = In("Audio/Music");

        a.forceToMono = !music;
        a.loadInBackground = music;

        var s = new AudioImporterSampleSettings
        {
            loadType          = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
            compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM,
            quality           = 0.5f,
            sampleRateSetting = music ? AudioSampleRateSetting.PreserveSampleRate
                                      : AudioSampleRateSetting.OverrideSampleRate,
            sampleRateOverride = 22050,
            preloadAudioData   = !music
        };
        a.defaultSampleSettings = s;
        a.SetOverrideSampleSettings(Android, s);
    }
}
