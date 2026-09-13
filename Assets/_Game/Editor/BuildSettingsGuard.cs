using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ponytail: one guard, one exception, no config asset. Fails the Android build with a clear message when a setting drifted
// (a UKR re-import can overwrite ProjectSettings). Runs before the Solana SDK's own AndroidGradleAutoConfig (callbackOrder 0).
class BuildSettingsGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        var a = NamedBuildTarget.Android;
        var errors = new List<string>();
        var warnings = new List<string>();
        void Check(bool ok, string msg) { if (!ok) errors.Add(msg); }
        void Warn(bool ok, string msg) { if (!ok) warnings.Add(msg); }

        Check(PlayerSettings.GetScriptingBackend(a) == ScriptingImplementation.IL2CPP, "Scripting Backend must be IL2CPP (Solana SDK)");
        Check(PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "Target Architectures must be ARM64 only");
        Warn(PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.GameActivity, "Application Entry Point is not GameActivity (fine if you deliberately flipped to Activity after the device test)");
        Check(PlayerSettings.Android.textureCompressionFormats.FirstOrDefault() == TextureCompressionFormat.ASTC, "Texture compression must be ASTC (first entry)");
        Check(PlayerSettings.Android.blitType == AndroidBlitType.Auto, "Blit Type must be Auto");
        Check(!PlayerSettings.Android.minifyRelease && !PlayerSettings.Android.minifyDebug, "Minify must be OFF (AGP 9 R8 hygiene)");
        Check(!PlayerSettings.Android.buildApkPerCpuArchitecture && !EditorUserBuildSettings.buildAppBundle, "Ship one APK: no split, no AAB");
        Check(PlayerSettings.Android.useCustomKeystore && File.Exists(PlayerSettings.Android.keystoreName), "Custom keystore must be set and exist on disk");
        Check(File.Exists("Assets/Plugins/Android/mainTemplate.gradle"), "Custom Main Gradle Template must be ticked (Solana SDK hard-fails without it)");
        Check(PlayerSettings.GetManagedStrippingLevel(a) <= ManagedStrippingLevel.Low, "Managed Stripping must be Low or Disabled until wallet+race+board pass on device (SDK issue #257)");
        Check(PlayerSettings.colorSpace == ColorSpace.Linear, "Color Space must be Linear");
        Check(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset, "Graphics > Default Render Pipeline must be a URP asset (a UKR import can clear it)");
        Check(QualitySettings.renderPipeline is UniversalRenderPipelineAsset || GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset, "Active quality level must reference a URP asset");
        Check(File.ReadAllText("ProjectSettings/ProjectSettings.asset").Contains("resolutionScalingMode: 1"), "Resolution Scaling Mode must be Fixed DPI");
        Check(!EditorUserBuildSettings.development || report.summary.options.HasFlag(BuildOptions.Development), "Development Build state inconsistent");

        // A MonoBehaviour class whose file has a different name gets an in-memory MonoScript that is embedded into the scene
        // (--- !u!115). Loads fine in the editor; the player aborts with "level0 is corrupted [Position out of bounds]".
        foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            Check(!File.ReadAllText(s.path).Contains("\n--- !u!115 "), $"{s.path} embeds a MonoScript: some MonoBehaviour in it lives in a file not named after its class — split the file and rebuild the scene");

#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
        errors.Add("Active Input Handling must be Input System Package (New); set InputManager.enableStandardInput=false on UKR's prefab instead of 'Both'");
#endif
        foreach (var t in new[] { "Walls", "Track", "reset", "Reset", "Character", "Kart", "Shield", "Wheel", "Left", "Right", "Look" })
            Warn(UnityEditorInternal.InternalEditorUtility.tags.Contains(t), $"Missing tag '{t}' (UKR TagManager not imported?)");
        foreach (var (i, n) in new[] { (8, "Karts"), (9, "Items"), (10, "Walls"), (11, "Kart Box Collider"), (12, "Shield") })
            Warn(LayerMask.LayerToName(i) == n, $"Layer {i} should be '{n}' (UKR)");

        foreach (var w in warnings) Debug.LogWarning("[BuildSettingsGuard] " + w);
        if (errors.Count > 0)
            throw new BuildFailedException("Build settings drifted — fix before building:\n - " + string.Join("\n - ", errors));
        Debug.Log("[BuildSettingsGuard] all Android build settings OK");
    }
}
