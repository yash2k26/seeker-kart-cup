using System.IO;
using UnityEditor;
using UnityEngine;

// The race-feel effects added after playing Beach Buggy (8 Oct 2026): the item bubble's material, the pop when one is
// taken, the puff and dizzy stars when a kart is hit, the dust its wheels throw, and the speed lines' texture. Built
// into Resources/FX so race code loads them by name, with no scene wiring.
// Textures come from tools/fx/feel_textures.py. Run: ./tools/unity.sh FeelSetup.Build   (TrackSetup.BuildRace runs it too)
public static class FeelSetup
{
    const string Tex = "Assets/_Game/Art/Textures/FX/";
    const string Out = "Assets/_Game/Resources/FX/";
    const string MatDir = "Assets/_Game/Art/Materials/FX/";

    [MenuItem("Build/Assemble Race Feel FX")]
    public static void Build()
    {
        Directory.CreateDirectory(Out); Directory.CreateDirectory(MatDir);
        Import("fx_bubble", false); Import("fx_star", false); Import("fx_puff", false); Import("fx_speedlines", true);

        // the bubble itself is drawn by TrackSetup's item boxes with this material: plain URP Unlit, transparent (the
        // particle shader reads vertex colour, and a quad mesh has none: it drew nothing)
        Bubble();
        BubblePop();
        HitPuff();
        Dizzy();
        WheelDust();
        AssetDatabase.SaveAssets();
        Debug.Log("[feel] built BubblePop, HitPuff, Dizzy, WheelDust");
    }

    public static Material BubbleMaterial => AssetDatabase.LoadAssetAtPath<Material>(MatDir + "FX_Bubble.mat");
    public static Sprite SpeedLines => AssetDatabase.LoadAssetAtPath<Sprite>(Tex + "fx_speedlines.png");

    /// Taken: gold stars thrown out of where the bubble was, and a violet ring that opens and fades.
    static void BubblePop()
    {
        var go = new GameObject("BubblePop");
        var stars = Particles("Stars", go.transform, Additive("FX_PopStar", Load("fx_star"), new Color(1f, 0.85f, 0.45f) * 1.6f));
        {
            // slow enough not to fly into the lens: the bubble pops a few metres in front of the camera
            var m = stars.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f); m.gravityModifier = 0.9f; m.maxParticles = 24;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(stars, 12);
            var sh = stars.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var rot = stars.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            Fade(stars, Color.white, new Color(1f, 0.7f, 0.2f));
        }
        var ring = Particles("Ring", go.transform, Additive("FX_PopRing", Load("fx_ring", "Assets/_Game/Art/Textures/FX/fx_ring.png"), new Color(0.62f, 0.45f, 1f) * 1.8f));
        {
            var m = ring.main; m.startLifetime = 0.32f; m.startSpeed = 0f; m.startSize = 3.2f; m.maxParticles = 2;
            Burst(ring, 1);
            var sz = ring.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1f)));
            Fade(ring, Color.white, new Color(0.7f, 0.5f, 1f));
        }
        Save(go, "BubblePop", 1.5f);
    }

    /// A hit: a grey puff round the struck kart, and sparks out of it. KartHooks parents it to the kart and it simulates
    /// in the kart's space, so the smoke goes with the kart instead of being left behind the camera at racing speed.
    static void HitPuff()
    {
        var go = new GameObject("HitPuff");
        var smoke = Particles("Smoke", go.transform, Alpha("FX_Smoke", Load("fx_puff"), new Color(0.82f, 0.82f, 0.86f, 0.85f)));
        {
            var m = smoke.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.7f); m.gravityModifier = -0.15f; m.maxParticles = 20;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(smoke, 12);
            var sh = smoke.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f;
            var lim = smoke.limitVelocityOverLifetime; lim.enabled = true; lim.drag = 3f;
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));
            Fade(smoke, Color.white, new Color(0.7f, 0.7f, 0.74f));
        }
        var sparks = Particles("Sparks", go.transform, Additive("FX_HitSpark", Load("fx_sparkle", "Assets/_Game/Art/Textures/FX/fx_sparkle.png"), new Color(1f, 0.7f, 0.3f) * 2.2f));
        {
            var m = sparks.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f); m.gravityModifier = 1.2f; m.maxParticles = 16;
            Burst(sparks, 10);
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
            Fade(sparks, Color.white, new Color(1f, 0.45f, 0.1f));
        }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) { var mm = ps.main; mm.simulationSpace = ParticleSystemSimulationSpace.Local; }
        Save(go, "HitPuff", 2f);
    }

    /// Stars circling the driver's head while the kart spins: three quads that KartCup.Dizzy turns and faces to the lens.
    static void Dizzy()
    {
        var go = new GameObject("Dizzy");
        var mat = Additive("FX_DizzyStar", Load("fx_star"), new Color(1f, 0.9f, 0.55f) * 1.5f);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        for (int i = 0; i < 3; i++)
        {
            var s = new GameObject("Star" + i);
            s.transform.SetParent(go.transform, false);
            s.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = s.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            s.transform.localScale = Vector3.one * 0.42f;
        }
        go.AddComponent<KartCup.Dizzy>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Out + "Dizzy.prefab");
        Object.DestroyImmediate(go);
    }

    /// What a wheel throws up: soft puffs, streamed while it rolls (KartCup.WheelDust sets the rate and the colour).
    static void WheelDust()
    {
        var go = new GameObject("WheelDust");
        var ps = go.AddComponent<ParticleSystem>();
        var m = ps.main; m.loop = true; m.duration = 1f; m.playOnAwake = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.95f); m.gravityModifier = -0.04f; m.maxParticles = 90;
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var e = ps.emission; e.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.25f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.1f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.12f), new GradientAlphaKey(0.25f, 0.6f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime; col.enabled = true; col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Alpha("FX_Dust", Load("fx_puff"), Color.white);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        PrefabUtility.SaveAsPrefabAsset(go, Out + "WheelDust.prefab");
        Object.DestroyImmediate(go);
    }

    static void Bubble()
    {
        string path = MatDir + "FX_Bubble.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (m == null) { m = new Material(unlit) { name = "FX_Bubble" }; AssetDatabase.CreateAsset(m, path); }
        m.shader = unlit;
        m.SetTexture("_BaseMap", Load("fx_bubble")); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_Cull", 0); m.SetFloat("_ZWrite", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(m);
    }

    // ---------- helpers (ItemSetup's, for a different folder) ----------
    static void Save(GameObject go, string name, float life)
    {
        go.AddComponent<KartCup.Lifetime>().seconds = life;
        PrefabUtility.SaveAsPrefabAsset(go, Out + name + ".prefab");
        Object.DestroyImmediate(go);
    }

    static ParticleSystem Particles(string name, Transform parent, Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var m = ps.main; m.loop = false; m.duration = 1f; m.playOnAwake = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Shape;
        var e = ps.emission; e.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    static void Burst(ParticleSystem ps, int count) { var e = ps.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) }); }

    static void Fade(ParticleSystem ps, Color a, Color b)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f) });
        var c = ps.colorOverLifetime; c.enabled = true; c.color = g;
    }

    static Texture2D Load(string name, string path = null) => AssetDatabase.LoadAssetAtPath<Texture2D>(path ?? Tex + name + ".png");

    static void Import(string name, bool sprite)
    {
        var path = Tex + name + ".png";
        if (AssetImporter.GetAtPath(path) is not TextureImporter ti) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (ti == null) { Debug.LogError($"[feel] {path} missing: run tools/fx/feel_textures.py"); return; }
        ti.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = !sprite;
        ti.maxTextureSize = sprite ? 2048 : 512;
        ti.SaveAndReimport();
    }

    /// Additive and unlit: light laid on the frame, no depth texture (the phone has none).
    static Material Additive(string name, Texture2D tex, Color hdr) => Particle(name, tex, hdr, true);
    /// Alpha-blended and unlit: smoke and dust, which must darken what is behind them a little, not glow.
    static Material Alpha(string name, Texture2D tex, Color tint) => Particle(name, tex, tint, false);

    static Material Particle(string name, Texture2D tex, Color color, bool additive)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SoftParticlesEnabled", 0); m.SetFloat("_ColorMode", 0);
        m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_SOFTPARTICLES_ON");
        if (!additive) m.EnableKeyword("_ALPHABLEND_ON"); else m.DisableKeyword("_ALPHABLEND_ON");
        EditorUtility.SetDirty(m);
        return m;
    }
}
