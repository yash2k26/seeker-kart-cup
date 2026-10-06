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
        Gloss();
        Retint();
        Water();
        Liveries();
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
        Set(so, "m_MainLightShadowmapResolution", 2048);   // (4096 with a wider filter was tried: with the rest of that pass it halved Palm Bay's frame rate)
        Set(so, "m_ShadowDistance", 60f);                  // 6 cm a texel: a quarter finer than the 75 m it was
        Set(so, "m_ShadowCascadeCount", 1);
        Set(so, "m_ShadowDepthBias", 1.0f);
        Set(so, "m_ShadowNormalBias", 0.8f);
        Set(so, "m_SoftShadowsSupported", true);
        Set(so, "m_SoftShadowQuality", 1);          // low: a 2-tap filter, cheap on mobile
        // The phone's panel at its own resolution (QualitySettings lifts the fixed-DPI cap), so the HUD and menus are
        // drawn sharp, and the 3D at 0.78 of that, a little more than the 1829 x 823 everything used to be drawn at.
        // Measured on the Nothing Phone (FpsCounter's sweep, Palm Bay's grid): at 0.85 through FSR with a 4096 shadow
        // map it ran 33 fps; FSR alone was 6 ms of that, the shadows 5, and 0.7 with a plain stretch ran 52. The phone
        // is fill-bound: resolution is the first thing to give back if a circuit drops under 50.
        Set(so, "m_RenderScale", 0.78f);
        Set(so, "m_UpscalingFilter", 1);            // bilinear
        Set(so, "m_MSAA", 4);
        Set(so, "m_ConservativeEnclosingSphere", true);
        // The night map: HDR so the lamps, studs and moon bloom from real values, and the sodium lamps and headlights
        // as per-pixel lights (eight per renderer; the road is built in chunks for it). Light shadows stay off.
        Set(so, "m_SupportsHDR", true);
        Set(so, "m_AdditionalLightsRenderingMode", 1);
        Set(so, "m_AdditionalLightsPerObjectLimit", 8);
        Set(so, "m_AdditionalLightShadowsSupported", false);
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
            if (mat.shader.name.Contains("Terrain")) continue;                // reads vertex colour; Simple Lit would drop it
            if (mat.shader.name.Contains("Road") || mat.shader.name.Contains("Beam") || mat.shader.name.Contains("Glow")) continue;   // the PBR road and the lighthouse beam are their own shaders
            if (mat.shader.name.Contains("Contact") || mat.shader.name.Contains("Pool")) continue;                // the title's contact shadow: on Simple Lit it is a white slab under each kart
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

    /// Toy-car paint: the kart bodies take a gloss, so the sun, the lamps and the moon put a highlight on them.
    static void Gloss()
    {
        foreach (var c in "abcdef")
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + $"Materials/Body_{c}.mat");
            if (mat == null || !mat.shader.name.EndsWith("Simple Lit")) continue;
            mat.SetFloat("_Smoothness", 0.72f);
            mat.SetColor("_SpecColor", new Color(0.32f, 0.32f, 0.32f));
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.SetFloat("_SmoothnessSource", 0f);
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_SPECULAR_COLOR");
            EditorUtility.SetDirty(mat);
        }
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
        grade.postExposure.overrideState = true; grade.postExposure.value = 0.12f;
        grade.contrast.overrideState = true; grade.contrast.value = 26f;
        grade.saturation.overrideState = true; grade.saturation.value = 34f;   // the sand read as washed-out tan
        grade.colorFilter.overrideState = true; grade.colorFilter.value = new Color(1f, 0.985f, 0.94f);

        // Warm highlights, cool shadows. This is most of what separates a flat toy render from a sunny one.
        var split = profile.Add<SplitToning>(true);
        split.shadows.overrideState = true; split.shadows.value = new Color(0.16f, 0.38f, 0.62f);
        split.highlights.overrideState = true; split.highlights.value = new Color(1f, 0.82f, 0.48f);
        split.balance.overrideState = true; split.balance.value = -8f;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true; vignette.intensity.value = 0.26f;
        vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.45f;

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.overrideState = true; bloom.threshold.value = 1.1f;
        bloom.intensity.overrideState = true; bloom.intensity.value = 0.85f;
        bloom.scatter.overrideState = true; bloom.scatter.value = 0.62f;
        bloom.maxIterations.overrideState = true; bloom.maxIterations.value = 3;     // few mip passes: cheap on mobile
        bloom.downscale.overrideState = true; bloom.downscale.value = BloomDownscaleMode.Quarter;

        var grain = profile.Add<FilmGrain>(true);
        grain.type.overrideState = true; grain.type.value = FilmGrainLookup.Thin1;
        grain.intensity.overrideState = true; grain.intensity.value = 0f;         // off by day; the night map turns it up
        grain.response.overrideState = true; grain.response.value = 0.8f;

        var fringe = profile.Add<ChromaticAberration>(true);
        fringe.intensity.overrideState = true; fringe.intensity.value = 0f;

        // Add<T> only lists a component: it has to be saved into the profile as a sub-asset or the list holds a null,
        // which is what every entry was until now, so no map had its grade at all.
        foreach (var c in profile.components)
        {
            c.name = c.GetType().Name;
            c.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
        }

        EditorUtility.SetDirty(profile);
        Debug.Log("[look] post profile written");
    }

    /// The sea is the biggest block of colour on screen, so it does the most work per byte.
    static void Water()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water.mat");
        if (mat == null) { Debug.LogWarning("[look] no Water.mat"); return; }
        mat.SetColor("_DeepColor", new Color(0.02f, 0.30f, 0.52f));
        mat.SetColor("_ShallowColor", new Color(0.16f, 0.78f, 0.80f));
        mat.SetVector("_Edge", new Vector4(0.18f, 0.72f, 0, 0));
        mat.SetFloat("_ShoreRadius", 420f);          // matches the 840 m island the tracks now sit on
        mat.SetFloat("_ShoreWidth", 30f);
        mat.SetVector("_ShoreWobble", new Vector4(16f, 0.03f, 0.45f, 0));
        EditorUtility.SetDirty(mat);
    }

    /// Every racer gets a livery. The roster shipped with tint = white on all eight, so the whole grid was the same
    /// five kit colours and you could not tell who you were chasing.
    static void Liveries()
    {
        var colours = new (string name, Color c)[]
        {
            ("Toly",   new Color(0.62f, 0.35f, 0.95f)),   // violet
            ("Mert",   new Color(1.00f, 0.47f, 0.13f)),   // orange
            ("Akshay", new Color(0.18f, 0.74f, 0.98f)),   // sky
            ("Emmett", new Color(0.20f, 0.84f, 0.48f)),   // green
            ("Bitsy",  new Color(1.00f, 0.32f, 0.52f)),   // pink
            ("Pip",    new Color(1.00f, 0.80f, 0.18f)),   // yellow
            ("Maroo",  new Color(0.22f, 0.42f, 1.00f)),   // royal blue
            ("Andrew", new Color(0.62f, 0.92f, 0.18f)),   // lime
        };
        foreach (var (name, c) in colours)
        {
            var def = AssetDatabase.LoadAssetAtPath<KartCup.CharacterDef>($"Assets/_Game/Data/Character_{name}.asset");
            if (def == null) continue;
            def.tint = c;
            EditorUtility.SetDirty(def);
        }
    }

    static void Set(SerializedObject so, string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; else Debug.LogWarning("[look] no " + prop); }
    static void Set(SerializedObject so, string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; else Debug.LogWarning("[look] no " + prop); }
    static void Set(SerializedObject so, string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; else Debug.LogWarning("[look] no " + prop); }
}
