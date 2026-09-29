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

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(PKP + "Items.prefab");
        if (src == null) { Debug.LogError("[items] Powerslide Items.prefab missing"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Items";
        Projectile(go.transform, "RocketItem", "Rocket", rocket, new Vector3(0, 0.5f, 0));
        Projectile(go.transform, "BombItem", "Bomb", bomb, new Vector3(0, 0.5f, -2.5f));
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
