using UnityEditor;
using UnityEngine;

// ponytail: one file, path-prefix rules, no ScriptableObject config, no editor window.
// Scope is Assets/_Game/** only, so third-party packages keep their own settings.
// Bump k_Version when you change a rule -> Unity reimports everything this touches.
public sealed class GameAssetImportRules : AssetPostprocessor
{
    const uint k_Version = 1;
    public override uint GetVersion() => k_Version;

    const string Root = "Assets/_Game/";
    const string Android = "Android";

    bool In(string sub) => assetPath.StartsWith(Root + sub, System.StringComparison.Ordinal);
    bool Mine => assetPath.StartsWith(Root, System.StringComparison.Ordinal);

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
            // Kenney palettes: 32px columns, UVs centred at 16px -> 256 is still within 3/255 of the original.
            t.textureType = TextureImporterType.Default;
            t.alphaSource = TextureImporterAlphaSource.None;
            t.mipmapEnabled = true;
            ps.maxTextureSize = 256;
            ps.format = TextureImporterFormat.RGBA32;      // no block compression on gradient (AO) columns
        }
        else if (In("Art/Textures/UI"))
        {
            t.textureType = TextureImporterType.Sprite;
            t.mipmapEnabled = false;                        // UI never minifies
            ps.maxTextureSize = 1024;
            ps.format = TextureImporterFormat.ASTC_4x4;
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
            if (In("Art/Textures/World")) t.wrapMode = TextureWrapMode.Repeat;   // water tiles, chevrons
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

        // One shared material per colormap, authored by hand. Never import FBX materials.
        m.materialImportMode = ModelImporterMaterialImportMode.None;

        bool rigged = In("Art/Models/Characters");   // Blocky Characters: rigid nodes + the `drive` clip
        m.animationType = rigged ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        m.importAnimation = rigged;
        m.optimizeGameObjects = false;               // keep torso/arm-left/arm-right/head addressable
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
