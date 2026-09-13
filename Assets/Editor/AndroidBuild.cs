using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Release: -batchmode -executeMethod AndroidBuild.Release (or menu Build > Android Release when batchmode IL2CPP misbehaves on Apple Silicon)
// Dev:     menu Build > Android Patch And Run
public static class AndroidBuild
{
    const string kRelease = "Assets/Settings/Build Profiles/Android-Release.asset";
    const string kDev     = "Assets/Settings/Build Profiles/Android-Dev.asset";
    const string kCodeFile = "ProjectSettings/versionCode.txt";   // committed, single source of truth

    static string Env(string k, string fallback = null) =>
        Environment.GetEnvironmentVariable(k) is { Length: > 0 } v ? v : fallback;

    [MenuItem("Build/Android Release")]
    public static void Release()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(kRelease)
            ?? throw new Exception($"Build profile not found: {kRelease}");
        BuildProfile.SetActiveBuildProfile(profile);

        EditorUserBuildSettings.buildAppBundle = false;            // dApp Store takes an APK, never an AAB
        ApplySigning();
        int code = BumpVersionCode();

        string dir = Path.GetFullPath(Env("BUILD_DIR", "Builds/Android"));
        Directory.CreateDirectory(dir);
        string apk = Path.Combine(dir, $"kartcup-{PlayerSettings.bundleVersion}-{code}.apk");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
        {
            buildProfile     = profile,
            locationPathName = apk,
            options          = BuildOptions.CompressWithLz4HC
        });

        var s = report.summary;
        Debug.Log($"[build] {s.result} {s.totalSize / 1048576f:F1} MB in {s.totalTime} -> {apk}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    [MenuItem("Build/Android Patch And Run (dev)")]
    public static void PatchAndRun()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(kDev)
            ?? throw new Exception($"Build profile not found: {kDev}");
        BuildProfile.SetActiveBuildProfile(profile);
        BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            target = BuildTarget.Android,
            scenes = Array.ConvertAll(profile.GetScenesForBuild(), sc => sc.path),
            locationPathName = Path.GetFullPath("Builds/Android/dev.apk"),
            options = BuildOptions.PatchPackage | BuildOptions.AutoRunPlayer | BuildOptions.Development
        });
    }

    static void ApplySigning()
    {
        // Unity never persists these passwords (by design), so they must be set on every build.
        string ks = Env("ANDROID_KEYSTORE");
        if (ks == null)
        {
            Debug.LogWarning("[build] ANDROID_KEYSTORE unset — using the keystore configured in Player Settings (GUI build).");
            return;
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = ks;
        PlayerSettings.Android.keystorePass = Env("ANDROID_KEYSTORE_PASS") ?? throw new Exception("ANDROID_KEYSTORE_PASS unset");
        PlayerSettings.Android.keyaliasName = Env("ANDROID_KEY_ALIAS")     ?? throw new Exception("ANDROID_KEY_ALIAS unset");
        PlayerSettings.Android.keyaliasPass = Env("ANDROID_KEY_PASS")      ?? throw new Exception("ANDROID_KEY_PASS unset");
    }

    static int BumpVersionCode()
    {
        int code = Env("VERSION_CODE") is { } s && int.TryParse(s, out var forced)
            ? forced
            : (File.Exists(kCodeFile) && int.TryParse(File.ReadAllText(kCodeFile).Trim(), out var last) ? last : 0) + 1;
        File.WriteAllText(kCodeFile, code.ToString());
        PlayerSettings.Android.bundleVersionCode = code;
        AssetDatabase.SaveAssets();                                  // persist ProjectSettings in batchmode
        return code;
    }
}
