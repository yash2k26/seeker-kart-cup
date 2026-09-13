using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// One-shot, idempotent: applies every Player / URP / Quality setting from APK_PLAN.html §4 through the API.
// Run headless:  Unity -batchmode -projectPath . -executeMethod ProjectSetup.Apply -quit   (or menu Build > Apply Project Settings)
// Re-run after any Ultimate Kart Racing re-import; BuildSettingsGuard fails the build if anything drifted.
public static class ProjectSetup
{
    const string PackageName = "com.fensory.kartcup";
    const string MobileRpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
    const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";

    [MenuItem("Build/Apply Project Settings")]
    public static void Apply()
    {
        var a = NamedBuildTarget.Android;

        // ---- identity ----
        PlayerSettings.companyName = "Fensory";
        PlayerSettings.productName = "Seeker Kart Cup";
        PlayerSettings.SetApplicationIdentifier(a, PackageName);
        if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "0.1.0";

        // ---- scripting / stripping ----
        PlayerSettings.SetScriptingBackend(a, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetApiCompatibilityLevel(a, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetIl2CppCodeGeneration(a, Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetIl2CppCompilerConfiguration(a, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetIl2CppStacktraceInformation(a, Il2CppStacktraceInformation.MethodOnly);
        PlayerSettings.SetManagedStrippingLevel(a, ManagedStrippingLevel.Low);   // SDK issue #257; raise later with link.xml proven
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.gcIncremental = true;

        // ---- Android ----
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.buildApkPerCpuArchitecture = false;
        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;   // Unity 6 default; verified on device day one
        PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
        PlayerSettings.Android.blitType = AndroidBlitType.Auto;
        PlayerSettings.Android.minifyRelease = false;
        PlayerSettings.Android.minifyDebug = false;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.Android.optimizedFramePacing = true;          // Swappy: asks the 120 Hz panel for 60 Hz
        PlayerSettings.Android.androidTVCompatibility = false;
        PlayerSettings.Android.renderOutsideSafeArea = true;
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "keys", "kartcup.keystore");
        PlayerSettings.Android.keyaliasName = "kartcup";
        PlayerSettings.SetMobileMTRendering(a, true);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.Android.startInFullscreen = true;

        // Vulkan
        PlayerSettings.vulkanEnableSetSRGBWrite = false;
        PlayerSettings.vulkanNumSwapchainBuffers = 3;
        PlayerSettings.vulkanEnableLateAcquireNextImage = false;

        // Batching: static on, dynamic off (SRP Batcher supersedes it; dynamic suppresses vertex compression)
        PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.Android, true);
        PlayerSettings.SetDynamicBatchingForPlatform(BuildTarget.Android, false);

        EnsureMainGradleTemplate();

        // ---- URP ----
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRpAssetPath) ?? FindFirst<UniversalRenderPipelineAsset>();
        if (rp != null)
        {
            var so = new SerializedObject(rp);
            Set(so, "m_MSAA", 4);
            Set(so, "m_SupportsHDR", false);
            Set(so, "m_RequireDepthTexture", false);
            Set(so, "m_RequireOpaqueTexture", false);
            SetFloat(so, "m_RenderScale", 1f);
            Set(so, "m_MainLightShadowsSupported", false);
            Set(so, "m_AdditionalLightsRenderingMode", 0);     // Disabled
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_SupportsDynamicBatching", false);
            Set(so, "m_MixedLightingSupported", false);
            Set(so, "m_SupportsLightLayers", false);
            Set(so, "m_EnableLODCrossFade", false);
            Set(so, "m_GPUResidentDrawerMode", 0);              // Disabled (GLES3 fallback needs it off)
            Set(so, "m_StoreActionsOptimization", 0);           // Auto
            so.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = rp;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = rp; }
            EditorUtility.SetDirty(rp);
        }
        else Debug.LogWarning("[setup] no URP asset found — assign Graphics > Default Render Pipeline by hand");

        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MobileRendererPath) ?? FindFirst<UniversalRendererData>();
        if (renderer != null)
        {
            var so = new SerializedObject(renderer);
            Set(so, "m_RenderingMode", 0);            // Forward
            Set(so, "m_DepthPrimingMode", 0);         // Disabled
            Set(so, "m_IntermediateTextureMode", 0);  // Auto (a kit import can set Always)
            Set(so, "m_CopyDepthMode", 1);            // After Transparents (only matters if depth is ever on)
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        // ---- Quality: one Android level, no shadows, MSAA from the URP asset, vsync off ----
        var androidLevel = QualitySettings.GetQualityLevel();
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
        QualitySettings.resolutionScalingFixedDPIFactor = 1f;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.SetQualityLevel(androidLevel, false);

        // ---- Lighting: no GI (unlit + blob shadows) ----
        Lightmapping.realtimeGI = false;
        Lightmapping.bakedGI = false;

        AssetDatabase.SaveAssets();

        // ---- keys without a public API: patch the YAML AFTER Unity has serialized PlayerSettings, or the save overwrites us ----
        PatchYaml("ProjectSettings/ProjectSettings.asset", new[] {
            ("resolutionScalingMode:", "resolutionScalingMode: 1"),        // Fixed DPI
            ("targetPixelDensity:", "targetPixelDensity: 320"),           // calibrate on device (Screen.width/height in the boot log)
            ("AndroidFilterTouchesWhenObscured:", "AndroidFilterTouchesWhenObscured: 0"),
            ("useCustomMainGradleTemplate:", "useCustomMainGradleTemplate: 1"),
            ("androidUseCustomKeystore:", "androidUseCustomKeystore: 1"),
            ("androidPredictiveBackSupport:", "androidPredictiveBackSupport: 0"),
            ("vulkanEnablePreTransform:", "vulkanEnablePreTransform: 1"),   // "Apply display rotation during rendering" — measure; flip to 0 if worse
            ("activeInputHandler:", "activeInputHandler: 1"),             // Input System (New); "Both" is unsupported on Android
        });
        Debug.Log("[setup] project settings applied");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void EnsureMainGradleTemplate()
    {
        // The Solana SDK throws BuildFailedException without this file; Unity generates it when the checkbox is ticked.
        const string dst = "Assets/Plugins/Android/mainTemplate.gradle";
        if (File.Exists(dst)) return;
        var src = Path.Combine(EditorApplication.applicationContentsPath, "/Applications/Unity/Hub/Editor/6000.3.24f1/PlaybackEngines/AndroidPlayer/Tools/GradleTemplates/mainTemplate.gradle");
        if (File.Exists(src)) { Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(src, dst); Debug.Log("[setup] copied mainTemplate.gradle"); }
        else Debug.LogWarning("[setup] mainTemplate.gradle source not found at " + src);
    }

    static void PatchYaml(string path, (string key, string line)[] edits)
    {
        var lines = File.ReadAllLines(path).ToList();
        foreach (var (key, line) in edits)
        {
            int i = lines.FindIndex(l => l.TrimStart().StartsWith(key));
            string indent = i >= 0 ? new string(' ', lines[i].Length - lines[i].TrimStart().Length) : "  ";
            if (i >= 0) lines[i] = indent + line;
            else { int p = lines.FindIndex(l => l.StartsWith("PlayerSettings:")); lines.Insert(p + 1, "  " + line); }
        }
        File.WriteAllLines(path, lines);
    }

    static T FindFirst<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x != null);

    static void Set(SerializedObject so, string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; else Debug.LogWarning("[setup] no property " + prop); }
    // intValue is the enum's underlying value; enumValueIndex is its position in the names list (MsaaQuality._4x = index 2, value 4 — the index write left MSAA Disabled)
    static void Set(SerializedObject so, string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; else Debug.LogWarning("[setup] no property " + prop); }
    static void SetFloat(SerializedObject so, string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; else Debug.LogWarning("[setup] no property " + prop); }
}
