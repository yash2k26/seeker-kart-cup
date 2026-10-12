using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// The launcher icon (13 Oct 2026, the user: "change the app logo to kartel-toly-logo.png"): the user's own art
// (beach-buggy/output/branding/kartel-character-logo), copied to Art/Textures/Branding/AppIcon*.png. Legacy and round
// icons are the logo; the adaptive icon is the logo at 55% on its own backdrop colour, so a circular mask keeps the word.
// tools/unity.sh AppIcon.Apply (ProjectSettings keeps it; run again only when the art changes).
public static class AppIcon
{
    const string Dir = "Assets/_Game/Art/Textures/Branding/";

    public static void Apply()
    {
        var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIcon.png");
        var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIconForeground.png");
        var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "AppIconBackground.png");
        if (full == null || fg == null || bg == null) { Debug.LogError("[icon] art missing under " + Dir); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        var target = NamedBuildTarget.Android;
        foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round })
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var i in icons) i.SetTexture(full);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }
        var adaptive = PlayerSettings.GetPlatformIcons(target, AndroidPlatformIconKind.Adaptive);
        foreach (var i in adaptive) { i.SetTexture(bg, 0); i.SetTexture(fg, 1); }
        PlayerSettings.SetPlatformIcons(target, AndroidPlatformIconKind.Adaptive, adaptive);
        AssetDatabase.SaveAssets();
        Debug.Log($"[icon] launcher icon set: {PlayerSettings.GetPlatformIcons(target, AndroidPlatformIconKind.Legacy).Length} legacy, {adaptive.Length} adaptive");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
