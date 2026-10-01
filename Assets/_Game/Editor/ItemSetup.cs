using System.IO;
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
        // A rocket is the homing missile with the homing off and the speed up; a bomb is the trap, bigger, black,
        // dropped behind the kart and hitting three spins hard.
        var rocket = Variant("HomingItem", "Rocket", p => { p.homingAccuracy = 0f; p.startSpeed = 32f; p.targetSpeed = 70f; p.accel = 24f; p.kartSpinCount = 2; });
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
        });

        var sun = SunBomb();

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(PKP + "Items.prefab");
        if (src == null) { Debug.LogError("[items] Powerslide Items.prefab missing"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Items";
        Projectile(go.transform, "RocketItem", "Rocket", rocket, new Vector3(0, 0.5f, 0));
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

    /// The sun bomb (Radiants' ask, 1 Oct 2026): the homing missile's physics with its brain swapped for SunBomb —
    /// lobbed ahead, a short fuse, an area burst.
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

        // ponytail: drawn by hand (a lit ball that pulses, a flash that swells); Cartoon FX's Sun and Explosion looked
        // right in the editor but its soft particles need the depth texture the phone's RP asset does not render.
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball"; ball.transform.SetParent(go.transform, false); ball.transform.localScale = Vector3.one * 0.9f;
        Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.GetComponent<Renderer>().sharedMaterial = Flash();
        var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false);
        l.color = new Color(1f, 0.7f, 0.3f); l.intensity = 8f; l.range = 8f;
        bomb.flashMaterial = Flash();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Out + "SunBomb.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// The burst's material: unlit, transparent, so the flash can fade. An asset, so the shader ships in the build.
    static Material Flash()
    {
        const string path = Out + "SunFlash.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "SunFlash" };
        m.SetColor("_BaseColor", new Color(1f, 0.9f, 0.5f, 1f));
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        AssetDatabase.CreateAsset(m, path);
        return m;
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

