using System.IO;
using System.Linq;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEngine;

// The race's item set: Powerslide's four (Homing Item, Turbo, Bouncer, Trap) and three of ours (Rocket, Bomb, Nitro),
// as one Items prefab the race scenes instantiate, so the boxes hand out seven kinds and the shop's packs have
// something to give. Run: ./tools/unity.sh ItemSetup.Build   (before TrackSetup.BuildRace: the scenes reference it)
public static class ItemSetup
{
    const string PKP = "Assets/ThirdParty/PowerslideKartPhysics/Prefabs/Items/";
    const string Out = "Assets/_Game/Prefabs/Items/";

    [MenuItem("Build/Assemble Items")]
    public static void Build()
    {
        Directory.CreateDirectory(Out);
        // A rocket is the homing missile with the homing off and the speed up: out of the tubes at once and gone. The
        // homing one keeps its brain and wears a blue nose. A bomb is the trap, bigger, black, dropped behind the
        // kart and hitting three spins hard. Both rockets are the film's (Rocketize).
        var rocket = Variant("HomingItem", "Rocket", p => { p.homingAccuracy = 0f; p.startSpeed = 48f; p.targetSpeed = 96f; p.accel = 46f; p.kartSpinCount = 2; Rocketize(p, Red); });
        var homing = Variant("HomingItem", "Homing", p => { p.startSpeed = Mathf.Max(p.startSpeed, 26f); Rocketize(p, Blue); });
        var bomb = Variant("StaticItem", "Bomb", p =>
        {
            p.kartSpinCount = 3;
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            {
                r.transform.localScale *= 1.3f;
                var m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "Bomb" };
                m.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.09f));
                AssetDatabase.CreateAsset(m, Out + "Bomb.mat");
                r.sharedMaterial = m;
            }
            p.gameObject.AddComponent<KartCup.BurstOnDestroy>().fx = HitFx("BombHit", 6f, "bomb_explode");
        });

        var sun = SunBomb();
        Mount();
        TurboPad();
        OilSlick();

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(PKP + "Items.prefab");
        if (src == null) { Debug.LogError("[items] Powerslide Items.prefab missing"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Items";
        // the homing item Powerslide ships fires its own grey missile: point it at ours
        foreach (var item in go.GetComponentsInChildren<ProjectileItem>()) if (item.itemName == "Homing Item") { item.itemPrefab = homing; item.spawnOffset = new Vector3(0, 1.5f, 1.2f); }
        // the trap is let go behind the kart, like the bomb (Powerslide drops it at the kart's own centre)
        foreach (var item in go.GetComponentsInChildren<ProjectileItem>()) if (item.itemName == "Trap") item.spawnOffset = new Vector3(0, 0.5f, -2.6f);
        // (the kart's ItemMount moves a fired shot to the pod's tubes; this offset is only where it starts without one)
        Projectile(go.transform, "RocketItem", "Rocket", rocket, new Vector3(0, 1.5f, 1.2f));
        Projectile(go.transform, "BombItem", "Bomb", bomb, new Vector3(0, 0.5f, -2.5f));
        Projectile(go.transform, "SunBombItem", "Sun Bomb", sun, new Vector3(0, 0.8f, 3.2f));   // clear of the longest kart's collider (2.55 m ahead)
        var nitro = new GameObject("NitroItem");
        nitro.transform.SetParent(go.transform, false);
        var boost = nitro.AddComponent<BoostItem>();
        boost.itemName = "Nitro"; boost.boostAmount = 6f; boost.boostForce = 8f;     // Turbo is 3 / 5

        PrefabUtility.SaveAsPrefabAsset(go, Out + "Items.prefab");
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Debug.Log($"[items] wrote {Out}Items.prefab with {AssetDatabase.LoadAssetAtPath<GameObject>(Out + "Items.prefab").GetComponentsInChildren<Item>().Length} items");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    const string FxTex = "Assets/_Game/Art/Textures/FX/";

    /// The sun bomb (Radiants' ask, 1 Oct 2026), as the teaser shows it: the film's own model (tools/blender/
    /// export_sunbomb.py writes the FBX from the teaser kit) on the homing missile's physics, with SunBomb for a
    /// brain — lobbed ahead, a burning fuse, an amber ring pulsing on the road, then the solar burst.
    static GameObject SunBomb()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(PKP + "HomingItem.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "SunBomb";
        Object.DestroyImmediate(go.GetComponent<SpawnedProjectileItem>());
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        foreach (var r in go.GetComponents<Renderer>()) Object.DestroyImmediate(r);
        var sphere = go.GetComponent<SphereCollider>(); if (sphere != null) sphere.radius = 0.5f;   // the missile's is 1 m

        var bomb = go.AddComponent<KartCup.SunBomb>();
        bomb.launchHeight = 0.55f; bomb.startSpeed = 12f; bomb.targetSpeed = 0f; bomb.accel = 0f;
        bomb.inheritKartSpeed = true; bomb.maintainKartSpeed = false; bomb.moveInAir = false;
        bomb.gravityAdd = -12f; bomb.forwardFriction = 3f; bomb.sideFriction = 10f;
        bomb.wallBounceReflect = true; bomb.maxBounces = 2; bomb.destroyOnWallHit = false;
        bomb.casterIgnoreTime = 0.7f; bomb.canHitCaster = true;
        bomb.homingAccuracy = 0f; bomb.findTargetWhileActive = false;
        bomb.kartSpinCount = 2; bomb.fuse = 2.4f; bomb.radius = 7f; bomb.knock = 7f;

        // ---- the model: gold shell with amber grooves, twelve rays, the lit face, the fuse
        var gold = new Color(1f, 0.58f, 0.05f); var hot = new Color(1f, 0.70f, 0.12f);
        var slot = new System.Collections.Generic.Dictionary<string, Material>
        {
            ["sun_gold"]   = Lit("Sun_Gold",   gold, 1f, 0.62f, new Color(1f, 0.50f, 0.03f) * 1.1f),   // all metal: a headlight on it comes back gold, not white
            ["sun_groove"] = Lit("Sun_Groove", new Color(0.16f, 0.05f, 0.008f), 1f, 0.4f, Color.black),
            ["sun_trim"]   = Lit("Sun_Trim",   new Color(0.62f, 0.27f, 0.03f), 1f, 0.6f, new Color(1f, 0.40f, 0.04f) * 0.35f),
            ["sun_rays"]   = Lit("Sun_Rays",   new Color(1f, 0.64f, 0.07f), 1f, 0.65f, new Color(1f, 0.56f, 0.04f) * 1.1f),
            ["sun_face"]   = Lit("Sun_Face",   new Color(0.16f, 0.05f, 0.008f), 0.3f, 0.65f, new Color(1f, 0.85f, 0.4f) * 2.6f),
            ["sun_dark"]   = Lit("Sun_Dark",   new Color(0.03f, 0.02f, 0.015f), 0.6f, 0.55f, Color.black),
            ["sun_rope"]   = Lit("Sun_Rope",   new Color(0.30f, 0.19f, 0.09f), 0f, 0.2f, Color.black),
            ["sun_ember"]  = Lit("Sun_Ember",  Color.white, 0f, 0.5f, new Color(1f, 0.95f, 0.8f) * 8f),
        };
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/Items/SunBomb.fbx");
        var tip = new Vector3(0, 0.55f, 0);
        if (fbx != null)
        {
            var ball = (GameObject)PrefabUtility.InstantiatePrefab(fbx, go.transform);
            PrefabUtility.UnpackPrefabInstance(ball, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            ball.name = "Ball";
            var mr = ball.GetComponentInChildren<MeshRenderer>(); var mf = ball.GetComponentInChildren<MeshFilter>();
            // the importer names every material "Lit", so slots go by position: the order export_sunbomb.py writes
            string[] order = { "sun_gold", "sun_groove", "sun_trim", "sun_rays", "sun_face", "sun_dark", "sun_rope", "sun_ember" };
            var ms = mr.sharedMaterials;
            int emberSlot = System.Array.IndexOf(order, "sun_ember");
            if (ms.Length != order.Length) Debug.LogError($"[items] SunBomb.fbx has {ms.Length} slots, expected {order.Length}: re-run tools/blender/export_sunbomb.py");
            for (int i = 0; i < ms.Length && i < order.Length; i++) ms[i] = slot[order[i]];
            mr.sharedMaterials = ms; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the film's bomb is 0.73 m across its rays; in a race it has to read from a chase camera: 1.7 m
            // (the importer's own rotation and scale stay: they are what stand the model up)
            ball.transform.localPosition = Vector3.zero;
            ball.transform.localRotation = Quaternion.Euler(0, 180, 0) * ball.transform.localRotation;   // the face looks back at whoever threw it
            float across = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z);
            ball.transform.localScale *= 1.7f / Mathf.Max(0.001f, across);
            if (emberSlot >= 0)   // the fuse's burning end, for the sparks
            {
                var mesh = mf.sharedMesh; var tris = mesh.GetTriangles(emberSlot); var vs = mesh.vertices; var sum = Vector3.zero;
                foreach (int t in tris) sum += vs[t];
                if (tris.Length > 0) tip = go.transform.InverseTransformPoint(mf.transform.TransformPoint(sum / tris.Length));
            }
        }
        else Debug.LogError("[items] SunBomb.fbx missing: run tools/blender/export_sunbomb.py");

        var dot = Additive("FX_SunDot", Tex("dot"), new Color(1f, 0.34f, 0.05f) * 3f);   // low green: ACES pulls a bright orange toward lime
        var glow = new GameObject("Glow").AddComponent<Light>(); glow.transform.SetParent(go.transform, false);
        glow.color = new Color(1f, 0.72f, 0.2f); glow.intensity = 12f; glow.range = 9f;

        // sparks spitting off the fuse, left behind in the air as it flies
        var sparks = Particles("Sparks", go.transform, dot, tip);
        {
            var m = sparks.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f); m.gravityModifier = 0.5f; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 60;
            var e = sparks.emission; e.rateOverTime = 45f;
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 40f; sh.radius = 0.03f; sh.rotation = new Vector3(-90, 0, 0);
            Fade(sparks, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.5f, 0.1f));
        }
        // the warning: an amber ring on the asphalt that swells to the blast radius, again and again, once it has landed
        var warn = Particles("Warn", go.transform, Additive("FX_SunRing", Tex("ring"), new Color(1f, 0.26f, 0.02f) * 2.6f), new Vector3(0, -0.38f, 0));
        {
            // (not on awake: SunBomb plays it when the bomb lands)
            var m = warn.main; m.loop = true; m.playOnAwake = false; m.startLifetime = 0.8f; m.startSpeed = 0f; m.startSize = bomb.radius * 2f; m.maxParticles = 4;
            var e = warn.emission; e.rateOverTime = 2.2f; Burst(warn, 1);   // the first ring the moment it lands
            var sz = warn.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
            Fade(warn, Color.white, Color.white);
            Flat(warn);
        }
        bomb.warn = warn;
        Voice(go, null, 10f, play: false);
        bomb.land = Sound("bomb_land"); bomb.tick = Sound("bomb_tick");
        bomb.burst = SunBurst(bomb.radius, dot);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Out + "SunBomb.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// The burst, after the film: a gold-white core, a sun-shaped shockwave across the road, embers thrown out in
    /// streaks, firework arcs, glitter that hangs and twinkles, and a light that turns the night gold for a moment.
    static GameObject SunBurst(float radius, Material dot)
    {
        var go = new GameObject("SunBurst");
        var light = new GameObject("Light").AddComponent<Light>(); light.transform.SetParent(go.transform, false); light.transform.localPosition = Vector3.up * 1.2f;
        light.color = new Color(1f, 0.74f, 0.3f); light.intensity = 140f; light.range = radius * 5f;
        var flash = go.AddComponent<KartCup.SunFlash>(); flash.glow = light; flash.fade = 1.3f; flash.life = 4f;

        var core = Particles("Core", go.transform, Additive("FX_SunCore", Tex("dot"), new Color(1f, 0.72f, 0.28f) * 2.6f), Vector3.up * 0.3f);
        {
            var m = core.main; m.startLifetime = 0.5f; m.startSpeed = 0f; m.startSize = radius * 1.9f; m.maxParticles = 2;
            Burst(core, 1);
            var sz = core.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.8f)));
            Fade(core, Color.white, new Color(1f, 0.7f, 0.3f));
        }
        var wave = Particles("Wave", go.transform, Additive("FX_SunWave", Tex("sunwave"), new Color(1f, 0.86f, 0.5f) * 2.4f), new Vector3(0, -0.36f, 0));
        {
            var m = wave.main; m.startLifetime = 0.6f; m.startSpeed = 0f; m.startSize = radius * 3.2f; m.maxParticles = 2;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(wave, 1);
            var sz = wave.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f, 0, 4f), new Keyframe(0.35f, 0.8f), new Keyframe(1f, 1f)));
            Fade(wave, Color.white, new Color(1f, 0.8f, 0.4f));
            Flat(wave);
        }
        var embers = Particles("Embers", go.transform, dot, Vector3.zero);
        {
            var m = embers.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.3f); m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 19f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f); m.gravityModifier = 1f; m.maxParticles = 120;
            Burst(embers, 90);
            var sh = embers.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.6f; sh.rotation = new Vector3(-90, 0, 0);
            var lim = embers.limitVelocityOverLifetime; lim.enabled = true; lim.drag = 1.8f;
            Fade(embers, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.45f, 0.08f));
            var r = embers.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.6f;
        }
        var arcs = Particles("Arcs", go.transform, dot, Vector3.zero);
        {
            var m = arcs.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(13f, 22f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f); m.gravityModifier = 2.2f; m.maxParticles = 24;
            Burst(arcs, 18);
            var sh = arcs.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 38f; sh.radius = 0.4f; sh.rotation = new Vector3(-90, 0, 0);
            Fade(arcs, new Color(1f, 0.85f, 0.6f), new Color(1f, 0.4f, 0.08f));
            var tr = arcs.trails; tr.enabled = true; tr.lifetime = 0.28f; tr.dieWithParticles = false; tr.inheritParticleColor = true;
            tr.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            arcs.GetComponent<ParticleSystemRenderer>().trailMaterial = dot;
        }
        var glitter = Particles("Glitter", go.transform, Additive("FX_SunGlitter", Tex("sparkle"), new Color(1f, 0.85f, 0.5f) * 4f), Vector3.up * 0.4f);
        {
            var m = glitter.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f); m.gravityModifier = 0.08f; m.maxParticles = 150;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
            Burst(glitter, 140);
            var sh = glitter.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = radius * 0.7f; sh.radiusThickness = 1f; sh.rotation = new Vector3(-90, 0, 0);
            var lim = glitter.limitVelocityOverLifetime; lim.enabled = true; lim.drag = 2.5f;
            var noise = glitter.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 0.6f;
            // the twinkle: each one flares and dims a few times before it goes out
            var tw = new AnimationCurve(); for (int i = 0; i <= 10; i++) tw.AddKey(i / 10f, (i % 2 == 0 ? 0.25f : 1f) * (1f - i / 12f));
            var sz = glitter.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, tw);
            Fade(glitter, Color.white, new Color(1f, 0.8f, 0.4f));
        }
        Voice(go, "solar_boom", 18f, play: true);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Out + "SunBurst.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static ParticleSystem Particles(string name, Transform parent, Material mat, Vector3 at)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at;
        var ps = go.AddComponent<ParticleSystem>();
        var m = ps.main; m.loop = false; m.duration = 1f; m.playOnAwake = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Shape;
        var e = ps.emission; e.rateOverTime = 0f;
        var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    /// Lies the particles in the system's own ground plane (its local XZ), so SunBomb can tilt them to the road's
    /// slope: a world-horizontal quad fourteen metres wide is half under the tarmac on any banked corner.
    static void Flat(ParticleSystem ps)
    {
        // a quad mesh, not a billboard: a mesh particle with Local alignment takes the transform's orientation exactly
        ps.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx"); r.alignment = ParticleSystemRenderSpace.Local;
    }

    static void Burst(ParticleSystem ps, int count) { var e = ps.emission; e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) }); }

    /// Colour a -> b over the life, alpha in fast and out slowly.
    static void Fade(ParticleSystem ps, Color a, Color b)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f) });
        var c = ps.colorOverLifetime; c.enabled = true; c.color = g;
    }

    static Material Lit(string name, Color baseColor, float metallic, float smoothness, Color emission)
    {
        string path = Out + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", baseColor); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        EditorUtility.SetDirty(m);
        return m;
    }

    /// An additive, unlit particle material: light on top of the frame, no depth texture needed (the phone has none).
    static Material Additive(string name, Texture2D tex, Color hdr)
    {
        string path = Out + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", hdr);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_SoftParticlesEnabled", 0); m.SetFloat("_ColorMode", 0);
        m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Resources/OilSlick: a dark puddle with a rainbow sheen at its edge (alpha-blended over the road; unlit, so it
    /// reads under the lamps and in the dark), a faint violet glow round it, and a low trigger a little smaller than
    /// the puddle, so a kart that clips the very edge gets away with it. OilSlick.cs lays them in solo races.
    static void OilSlick()
    {
        const float D = 3.6f;
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var root = new GameObject("OilSlick");
        root.AddComponent<KartCup.OilSlick>();
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true; trigger.center = new Vector3(0, 0.7f, 0); trigger.size = new Vector3(D * 0.72f, 1.4f, D * 0.72f);
        void Flat(string name, Material m, float y, float size)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0, y, 0); go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        Flat("Sheen", Additive("FX_OilSheen", Tex("dot"), new Color(0.45f, 0.25f, 0.9f) * 0.55f), 0f, D * 1.6f);
        string path = Out + "FX_Oil.mat";
        var oil = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (oil == null) { oil = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "FX_Oil" }; AssetDatabase.CreateAsset(oil, path); }
        oil.SetTexture("_BaseMap", OilTex()); oil.SetColor("_BaseColor", Color.white);
        oil.SetFloat("_Surface", 1); oil.SetFloat("_Blend", 0); oil.SetFloat("_ZWrite", 0); oil.SetFloat("_Cull", 0);
        oil.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); oil.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        oil.SetFloat("_SoftParticlesEnabled", 0); oil.SetFloat("_ColorMode", 0);
        oil.renderQueue = 3001; oil.SetOverrideTag("RenderType", "Transparent"); oil.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); oil.DisableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(oil);
        Flat("Oil", oil, 0.02f, D);
        Directory.CreateDirectory("Assets/_Game/Resources");
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Resources/OilSlick.prefab");
        Object.DestroyImmediate(root);
    }

    /// The puddle: an irregular blob (its edge wanders with the angle), near black inside, a band of oily rainbow
    /// round the rim, soft at the very edge.
    static Texture2D OilTex()
    {
        string path = FxTex + "fx_oil.png";
        var have = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (have != null) return have;
        const int S = 256;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                float a = Mathf.Atan2(v, u), r = Mathf.Sqrt(u * u + v * v);
                float edge = 0.78f + 0.09f * Mathf.Sin(3f * a + 0.7f) + 0.06f * Mathf.Sin(5f * a + 2.1f) + 0.04f * Mathf.Sin(9f * a);
                float t = r / edge;                                               // 0 at the middle, 1 at the edge
                float alpha = Mathf.Clamp01((1f - t) / 0.06f) * 0.92f;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(t - 0.8f) / 0.17f);      // the rainbow band just inside the edge
                var hue = Color.HSVToRGB(Mathf.Repeat(a / (2f * Mathf.PI) + t * 0.6f, 1f), 0.75f, 0.85f);
                var c = Color.Lerp(new Color(0.03f, 0.02f, 0.05f), hue, rim * 0.75f);
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, alpha));
            }
        tex.Apply();
        Directory.CreateDirectory(FxTex);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// Resources/TurboPad: a strip of chevrons running up the road over a soft glow, a trigger a kart's height tall,
    /// and Powerslide's BoostPad (a Turbo's worth of boost and its kick). TurboPad.cs lays them at the start of a race.
    static void TurboPad()
    {
        const float W = 6.5f, L = 11f;
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var root = new GameObject("TurboPad");
        root.AddComponent<KartCup.TurboPad>();
        var boost = root.AddComponent<BoostPad>();
        boost.boostAmount = 3f; boost.boostForce = 5f; boost.delayInterval = 1f;      // the Turbo item's 3 / 5
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true; trigger.center = new Vector3(0, 1.2f, 0); trigger.size = new Vector3(W, 2.4f, L);
        Transform Flat(string name, Material m, float y, float w, float l)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0, y, 0); go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // a quad's up becomes the road's forward
            go.transform.localScale = new Vector3(w, l, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }
        var gold = new Color(1f, 0.62f, 0.12f);
        Flat("Glow", Additive("FX_TurboGlow", Tex("dot"), gold * 0.9f), 0f, W * 1.7f, L * 1.5f);
        // the chevrons are drawn and moved by their own shader (a particle material takes no tiling or offset)
        var shader = Shader.Find("KartCup/TurboPad");
        var chevrons = AssetDatabase.LoadAssetAtPath<Material>(Out + "FX_TurboArrows.mat");
        if (chevrons == null) { chevrons = new Material(shader) { name = "FX_TurboArrows" }; AssetDatabase.CreateAsset(chevrons, Out + "FX_TurboArrows.mat"); }
        chevrons.shader = shader; chevrons.SetColor("_Color", gold); EditorUtility.SetDirty(chevrons);
        Flat("Arrows", chevrons, 0.02f, W, L);
        Directory.CreateDirectory("Assets/_Game/Resources");
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Resources/TurboPad.prefab");
        Object.DestroyImmediate(root);
    }

    /// The four shapes the effects are drawn with, painted here so nothing is borrowed: a soft dot, a four-point
    /// sparkle, a thin ring, and the sun-shaped shockwave (a spiked disc with a bright rim).
    static Texture2D Tex(string kind)
    {
        string path = FxTex + "fx_" + kind + ".png";
        var have = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (have != null) return have;
        Directory.CreateDirectory(FxTex);
        int n = kind == "sunwave" || kind == "ring" ? 256 : 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(u * u + v * v), a = 0f;
                if (kind == "dot") a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                else if (kind == "sparkle")
                {
                    float au = Mathf.Abs(u), av = Mathf.Abs(v);
                    float arms = Mathf.Max(Mathf.Exp(-av * 26f) * Mathf.Clamp01(1f - au), Mathf.Exp(-au * 26f) * Mathf.Clamp01(1f - av));
                    a = Mathf.Clamp01(arms * arms * 1.4f + Mathf.Pow(Mathf.Clamp01(1f - r * 3.2f), 2f));
                }
                else if (kind == "ring") a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.07f) + Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.2f) * 0.35f + (r < 0.88f ? 0.07f : 0f);
                else if (kind == "sunwave")
                {
                    float ang = Mathf.Atan2(v, u), saw = Mathf.Abs(Mathf.Repeat(ang / (Mathf.PI * 2f) * 16f, 1f) - 0.5f) * 2f;   // 0 at a ray's tip
                    float edge = Mathf.Lerp(0.96f, 0.62f, saw);                                                                 // sixteen rays
                    float d = edge - r;
                    a = d < 0f ? 0f : Mathf.Clamp01(1f - d / 0.09f) * 0.95f + Mathf.Clamp01(d / 0.1f) * 0.22f * Mathf.Clamp01(r * 1.6f);
                }
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- the film's launcher, rocket and cradle (tools/blender/export_weapons.py) ----------
    const string Models = "Assets/_Game/Art/Models/Items/";
    const float FilmScale = 1.35f;      // the film's kart is 2.6 m long, ours run 3.3 to 4.5

    /// An exported model under `parent`, its slots given materials BY POSITION (the importer names them all "Lit"),
    /// turned so the film's forward (+Y in Blender) is +Z here, at `scale`.
    static Transform Model(string fbx, Transform parent, string name, float scale, params Material[] slots)
    {
        var wrap = new GameObject(name).transform;
        wrap.SetParent(parent, false);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + fbx + ".fbx");
        if (src == null) { Debug.LogError($"[items] {fbx}.fbx missing: run tools/blender/export_weapons.py"); return wrap; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, wrap);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0, 180, 0) * go.transform.localRotation;   // the importer's own turn stays
        var mr = go.GetComponentInChildren<MeshRenderer>();
        if (mr != null)
        {
            var ms = mr.sharedMaterials;
            if (ms.Length != slots.Length) Debug.LogError($"[items] {fbx}.fbx has {ms.Length} slots, expected {slots.Length}");
            for (int i = 0; i < ms.Length && i < slots.Length; i++) ms[i] = slots[i];
            mr.sharedMaterials = ms; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        wrap.localScale = Vector3.one * scale;
        return wrap;
    }

    static Material Paint => Lit("Pod_Paint", new Color(1f, 0.35f, 0.12f), 0.3f, 0.7f, new Color(1f, 0.30f, 0.08f) * 0.22f);   // the game's orange, lit a little so it reads at night
    static Material Dark => Lit("Pod_Dark", new Color(0.03f, 0.032f, 0.04f), 0.85f, 0.65f, Color.black);
    static Material Steel => Lit("Pod_Steel", new Color(0.55f, 0.57f, 0.6f), 1f, 0.7f, Color.black);
    static Material Red => Lit("Pod_Red", new Color(0.75f, 0.04f, 0.02f), 0.2f, 0.7f, new Color(1f, 0.1f, 0.02f) * 1.4f);
    static Material Blue => Lit("Pod_Blue", new Color(0.03f, 0.25f, 0.8f), 0.2f, 0.7f, new Color(0.05f, 0.5f, 1f) * 1.6f);
    static Material White => Lit("Rocket_White", new Color(0.85f, 0.85f, 0.82f), 0.1f, 0.7f, new Color(0.25f, 0.25f, 0.24f));

    /// Dresses a Powerslide projectile as the film's rocket: the shell (red-nosed, or blue for the homing one), a jet
    /// left in the air behind it, a light that rakes whatever it passes, and a nose that follows its flight.
    static void Rocketize(SpawnedProjectileItem p, Material nose)
    {
        var go = p.gameObject;
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        foreach (var r in go.GetComponents<Renderer>()) Object.DestroyImmediate(r);
        foreach (var f in go.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
        var sphere = go.GetComponent<SphereCollider>(); if (sphere != null) sphere.radius = 0.45f;
        var model = Model("RocketShell", go.transform, "Model", 1.9f, White, nose, Dark);
        go.AddComponent<KartCup.RocketLook>().model = model;
        var dot = Additive("FX_SunDot", Tex("dot"), new Color(1f, 0.34f, 0.05f) * 3f);
        var jet = Particles("Jet", model, dot, new Vector3(0, 0, -0.32f));
        {
            // local: at 60 m/s a world-space jet is a row of dots a metre apart; this one stays a flame on the nozzle
            var m = jet.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.22f); m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); m.maxParticles = 60; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            var e = jet.emission; e.rateOverTime = 120f;
            var sh = jet.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.05f; sh.rotation = new Vector3(180, 0, 0);
            var sz = jet.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            Fade(jet, new Color(1f, 0.95f, 0.8f), new Color(1f, 0.35f, 0.05f));
        }
        var light = new GameObject("Glow").AddComponent<Light>(); light.transform.SetParent(model, false); light.transform.localPosition = new Vector3(0, 0, -0.5f);
        light.color = new Color(1f, 0.55f, 0.2f); light.intensity = 10f; light.range = 8f;
        // heard from where it is: the launch as it leaves, the bang where it ends
        Voice(go, nose == Blue ? "homing_launch" : "rocket_launch", 12f, play: true);
        go.AddComponent<KartCup.BurstOnDestroy>().fx = HitFx("RocketHit", 5f, "rocket_impact");
    }

    const string SfxDir = "Assets/_Game/Audio/SFX/items/";
    static AudioClip Sound(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + name + ".ogg");
        if (c == null) Debug.LogWarning($"[items] sound missing: {name}.ogg (run tools/audio/build-audio.sh)");
        return c;
    }

    /// A 3D source on a thing in the world: mostly positional, full volume within `near` metres.
    static AudioSource Voice(GameObject go, string clip, float near, bool play)
    {
        var a = go.GetComponent<AudioSource>();
        if (a == null) a = go.AddComponent<AudioSource>();   // (not ??: in the editor a missing built-in component is a fake null)
        a.clip = clip != null ? Sound(clip) : null; a.playOnAwake = play; a.loop = false;
        a.spatialBlend = 0.75f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = near; a.maxDistance = near * 8f; a.dopplerLevel = 0f;
        return a;
    }

    /// What a rocket or a bomb leaves where it ends: a flash, sparks thrown out, a light, and its bang.
    static GameObject HitFx(string name, float size, string sound)
    {
        var go = new GameObject(name);
        var dot = Additive("FX_SunDot", Tex("dot"), new Color(1f, 0.34f, 0.05f) * 3f);
        var light = new GameObject("Light").AddComponent<Light>(); light.transform.SetParent(go.transform, false); light.transform.localPosition = Vector3.up * 0.8f;
        light.color = new Color(1f, 0.6f, 0.25f); light.intensity = 45f; light.range = size * 4f;
        var flash = go.AddComponent<KartCup.SunFlash>(); flash.glow = light; flash.fade = 0.45f; flash.life = 2.5f;
        var core = Particles("Core", go.transform, Additive("FX_SunCore", Tex("dot"), new Color(1f, 0.72f, 0.28f) * 2.6f), Vector3.up * 0.4f);
        {
            var m = core.main; m.startLifetime = 0.3f; m.startSpeed = 0f; m.startSize = size; m.maxParticles = 2;
            Burst(core, 1);
            var sz = core.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f)));
            Fade(core, Color.white, new Color(1f, 0.6f, 0.2f));
        }
        var sparks = Particles("Sparks", go.transform, dot, Vector3.up * 0.3f);
        {
            var m = sparks.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.26f); m.gravityModifier = 1.2f; m.maxParticles = 50;
            Burst(sparks, 40);
            var sh = sparks.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.4f; sh.rotation = new Vector3(-90, 0, 0);
            Fade(sparks, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.4f, 0.06f));
            var r = sparks.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.5f;
        }
        Voice(go, sound, 14f, play: true);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Out + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// Resources/ItemMount: the pod on its strut with a muzzle flash, and the cradle with the two things it can
    /// carry. ItemMount.cs puts them on the flanks of whichever kart it is given and plays them.
    static void Mount()
    {
        Directory.CreateDirectory("Assets/_Game/Resources");
        float s = FilmScale;
        var root = new GameObject("ItemMount");
        var mount = root.AddComponent<KartCup.ItemMount>();

        var pod = new GameObject("Pod").transform; pod.SetParent(root.transform, false);
        Model("RocketPod", pod, "Model", s, Paint, Dark, Steel, Red);
        var strut = new GameObject("Strut").transform; strut.SetParent(pod, false); strut.localPosition = new Vector3(0.14f * s, -0.2f * s, -0.12f * s);
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cube); leg.name = "Leg"; Object.DestroyImmediate(leg.GetComponent<Collider>());
        leg.transform.SetParent(strut, false); leg.transform.localPosition = new Vector3(0, -0.4f * s, 0); leg.transform.localScale = new Vector3(0.09f, 0.8f * s, 0.09f);
        leg.GetComponent<MeshRenderer>().sharedMaterial = Steel; leg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(pod, false); muzzle.localPosition = new Vector3(0, 0, 0.52f * s);
        var flash = Particles("Flash", muzzle, Additive("FX_SunDot", Tex("dot"), new Color(1f, 0.34f, 0.05f) * 3f), Vector3.zero);
        {
            var m = flash.main; m.playOnAwake = false; m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f); m.startSpeed = new ParticleSystem.MinMaxCurve(5f, 16f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.75f); m.maxParticles = 30;
            Burst(flash, 18);
            var sh = flash.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 24f; sh.radius = 0.12f;
            Fade(flash, new Color(1f, 0.97f, 0.85f), new Color(1f, 0.4f, 0.06f));
            var r = flash.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.4f;
        }
        var fl = new GameObject("FlashLight").AddComponent<Light>(); fl.transform.SetParent(muzzle, false);
        fl.color = new Color(1f, 0.7f, 0.35f); fl.intensity = 0f; fl.range = 11f; fl.enabled = false;

        var cradle = new GameObject("Cradle").transform; cradle.SetParent(root.transform, false);
        Model("Cradle", cradle, "Model", s, Dark, Paint);
        // the sun bomb as it rides: the same model the throw uses, face to the camera behind, tipped up a little
        var gold = AssetDatabase.LoadAssetAtPath<Material>(Out + "Sun_Gold.mat");
        Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>(Out + n + ".mat");
        var sun = Model("SunBomb", cradle, "SunBomb", s, gold, M("Sun_Groove"), M("Sun_Trim"), M("Sun_Rays"), M("Sun_Face"), M("Sun_Dark"), M("Sun_Rope"), M("Sun_Ember"));
        sun.localRotation = Quaternion.Euler(-14f, 0f, 0f);   // its lit face looks back at the chase camera
        // "gold crawls across the paint": the bomb lights the flank it rides on
        var warm = new GameObject("Warm").AddComponent<Light>(); warm.transform.SetParent(sun, false); warm.transform.localPosition = new Vector3(0, 0.1f, -0.5f);
        warm.color = new Color(1f, 0.72f, 0.2f); warm.intensity = 5f; warm.range = 3.5f;
        var bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere); bomb.name = "Bomb"; Object.DestroyImmediate(bomb.GetComponent<Collider>());
        bomb.transform.SetParent(cradle, false); bomb.transform.localScale = Vector3.one * (0.46f * s);
        bomb.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Out + "Bomb.mat") ?? Dark;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cap.name = "Cap"; Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(bomb.transform, false); cap.transform.localPosition = new Vector3(0, 0.5f, 0); cap.transform.localScale = new Vector3(0.28f, 0.08f, 0.28f);
        cap.GetComponent<MeshRenderer>().sharedMaterial = Red;

        mount.pod = pod; mount.strut = strut; mount.muzzle = muzzle; mount.cradle = cradle; mount.flash = flash; mount.flashLight = fl;
        mount.sunBomb = sun.gameObject; mount.bomb = bomb;
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Resources/ItemMount.prefab");
        Object.DestroyImmediate(root);
    }

    /// A copy of one of Powerslide's spawned-projectile prefabs, retuned.
    static GameObject Variant(string source, string name, System.Action<SpawnedProjectileItem> tune)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(PKP + source + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        var projectile = go.GetComponentInChildren<SpawnedProjectileItem>();
        if (projectile != null) tune(projectile);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Out + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void Projectile(Transform parent, string objectName, string itemName, GameObject prefab, Vector3 offset)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        var item = go.AddComponent<ProjectileItem>();
        item.itemName = itemName; item.itemPrefab = prefab; item.spawnOffset = offset;
    }
}

