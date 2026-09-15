using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The look pass: real lighting and shadows, a colour-graded frame, and materials moved off the flat unlit shader.
// Run: Unity -batchmode -projectPath . -executeMethod LookSetup.Apply -quit   (menu Build > Apply Look)
public static class LookSetup
{
    const string Art = "Assets/_Game/Art/";
    const string Settings = "Assets/Settings/";

    [MenuItem("Build/Apply Look")]
    public static void Apply()
    {
        Shadows();
        Materials();
        Retint();
        PostProfile();
        AssetDatabase.SaveAssets();
        Debug.Log("[look] shadows, materials and post profile applied");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Shadows()
    {
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Settings + "Mobile_RPAsset.asset");
        if (rp == null) { Debug.LogError("[look] no URP asset"); return; }
        var so = new SerializedObject(rp);
        // One directional light, one cascade, a short distance: enough to ground the karts and props without
        // paying for a cascade split we would never see on a phone.
        Set(so, "m_MainLightShadowsSupported", true);
        Set(so, "m_MainLightRenderingMode", 1);
        Set(so, "m_MainLightShadowmapResolution", 2048);
        Set(so, "m_ShadowDistance", 75f);
        Set(so, "m_ShadowCascadeCount", 1);
        Set(so, "m_ShadowDepthBias", 1.0f);
        Set(so, "m_ShadowNormalBias", 0.8f);
        Set(so, "m_SoftShadowsSupported", true);
        Set(so, "m_SoftShadowQuality", 1);          // low: a 2-tap filter, cheap on mobile
        Set(so, "m_ConservativeEnclosingSphere", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rp);
    }

    static void Materials()
    {
        // URP's stock Simple Lit: Blinn-Phong, one light, specular off. A hand-written lit shader kept tripping the
        // SRP Batcher (one material's constants ended up on every object), and this is both correct and cheap.
        var lit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (lit == null) { Debug.LogError("[look] Simple Lit shader missing"); return; }
        int moved = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game/Art/Materials" }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null) continue;
            if (!mat.shader.name.StartsWith("KartCup/")) continue;
            if (mat.shader.name.Contains("Water")) continue;                  // the water shader animates its own scroll
            var map = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            var tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            mat.shader = lit;
            mat.SetTexture("_BaseMap", map);
            mat.SetColor("_BaseColor", tint);
            mat.SetColor("_SpecColor", Color.black);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.DisableKeyword("_SPECGLOSSMAP");
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(mat);
            moved++;
        }
        Debug.Log($"[look] {moved} materials on Simple Lit");
    }

    static void Retint()
    {
        // These were authored against the unlit shader, where the tint was the final colour. Under lighting they
        // need to be darker or the sun and ambient wash them out to the same cream as everything else.
        var tints = new (string asset, Color colour)[]
        {
            ("Road", new Color(0.26f, 0.26f, 0.29f)),
            ("Sand", new Color(0.78f, 0.68f, 0.46f)),
            ("Kerb", new Color(0.82f, 0.82f, 0.84f)),
            ("KerbRed", new Color(0.62f, 0.13f, 0.12f)),
            ("RoadLine", new Color(0.80f, 0.80f, 0.78f)),
            ("RoadDark", new Color(0.10f, 0.10f, 0.12f)),
        };
        foreach (var (asset, colour) in tints)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + $"Materials/{asset}.mat");
            if (mat == null) continue;
            mat.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(mat);
        }
    }

    static void PostProfile()
    {
        Directory.CreateDirectory(Settings);
        var path = Settings + "PostProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        foreach (var c in profile.components.ToArray()) { profile.Remove(c.GetType()); Object.DestroyImmediate(c, true); }

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.overrideState = true; tone.mode.value = TonemappingMode.Neutral;

        var grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.overrideState = true; grade.postExposure.value = 0f;
        grade.contrast.overrideState = true; grade.contrast.value = 14f;
        grade.saturation.overrideState = true; grade.saturation.value = 16f;
        grade.colorFilter.overrideState = true; grade.colorFilter.value = new Color(1f, 0.99f, 0.95f);

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true; vignette.intensity.value = 0.26f;
        vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.45f;

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.overrideState = true; bloom.threshold.value = 1.1f;
        bloom.intensity.overrideState = true; bloom.intensity.value = 0.55f;
        bloom.scatter.overrideState = true; bloom.scatter.value = 0.62f;
        bloom.maxIterations.overrideState = true; bloom.maxIterations.value = 3;     // few mip passes: cheap on mobile
        bloom.downscale.overrideState = true; bloom.downscale.value = BloomDownscaleMode.Quarter;

        EditorUtility.SetDirty(profile);
        Debug.Log("[look] post profile written");
    }

    static void Set(SerializedObject so, string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; else Debug.LogWarning("[look] no " + prop); }
    static void Set(SerializedObject so, string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; else Debug.LogWarning("[look] no " + prop); }
    static void Set(SerializedObject so, string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; else Debug.LogWarning("[look] no " + prop); }
}
