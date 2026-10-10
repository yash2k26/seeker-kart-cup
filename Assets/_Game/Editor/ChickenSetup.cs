using KartCup;
using UnityEditor;
using UnityEngine;

/// Resources/Chicken: a blocky hen in the drivers' style (white body, red comb and wattle, orange beak and legs), at
/// the race's toy scale (karts are 3.75 m long), with the trigger Chicken.cs needs and its squawk.
///     tools/unity.sh ChickenSetup.Build
public static class ChickenSetup
{
    const string Mats = "Assets/_Game/Art/Materials/";

    [MenuItem("Build/Chicken")]
    public static void Build()
    {
        var white = Mat("Chicken_White", new Color(0.97f, 0.95f, 0.9f), 0.15f);
        var red = Mat("Chicken_Red", new Color(0.86f, 0.12f, 0.1f), 0.3f);
        var orange = Mat("Chicken_Orange", new Color(1f, 0.62f, 0.12f), 0.25f);
        var black = Mat("Chicken_Eye", new Color(0.05f, 0.05f, 0.06f), 0.6f);
        var root = new GameObject("Chicken");
        var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
        Box(model, "Body", white, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 0.5f, 0.78f));
        Box(model, "Tail", white, new Vector3(0, 0.74f, -0.4f), new Vector3(0.4f, 0.4f, 0.2f), new Vector3(-25f, 0, 0));
        Box(model, "Wing L", white, new Vector3(-0.34f, 0.52f, -0.02f), new Vector3(0.08f, 0.32f, 0.5f));
        Box(model, "Wing R", white, new Vector3(0.34f, 0.52f, -0.02f), new Vector3(0.08f, 0.32f, 0.5f));
        Box(model, "Head", white, new Vector3(0, 0.92f, 0.3f), new Vector3(0.34f, 0.38f, 0.34f));
        Box(model, "Comb", red, new Vector3(0, 1.16f, 0.3f), new Vector3(0.08f, 0.14f, 0.26f));
        Box(model, "Beak", orange, new Vector3(0, 0.9f, 0.52f), new Vector3(0.14f, 0.1f, 0.14f));
        Box(model, "Wattle", red, new Vector3(0, 0.78f, 0.47f), new Vector3(0.08f, 0.12f, 0.06f));
        Box(model, "Eye L", black, new Vector3(-0.172f, 0.98f, 0.38f), new Vector3(0.02f, 0.07f, 0.07f));
        Box(model, "Eye R", black, new Vector3(0.172f, 0.98f, 0.38f), new Vector3(0.02f, 0.07f, 0.07f));
        Box(model, "Leg L", orange, new Vector3(-0.13f, 0.14f, 0), new Vector3(0.07f, 0.28f, 0.07f));
        Box(model, "Leg R", orange, new Vector3(0.13f, 0.14f, 0), new Vector3(0.07f, 0.28f, 0.07f));
        Box(model, "Foot L", orange, new Vector3(-0.13f, 0.02f, 0.06f), new Vector3(0.14f, 0.04f, 0.18f));
        Box(model, "Foot R", orange, new Vector3(0.13f, 0.02f, 0.06f), new Vector3(0.14f, 0.04f, 0.18f));
        root.transform.localScale = Vector3.one * 1.8f;   // a hen the size of a driver's head: big enough to aim at, or round
        var col = root.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.55f; col.center = new Vector3(0, 0.55f, 0.05f);
        var c = root.AddComponent<Critter>();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/chicken_squawk.wav");
        new SerializedObject(c).Do(s => s.FindProperty("squawk").objectReferenceValue = clip);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Resources/Chicken.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[items] chicken prefab written (squawk {(clip != null ? "set" : "MISSING")})");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Material Mat(string name, Color c, float smooth)
    {
        var m = ContentSetup.LoadOrCreate(Mats + name + ".mat", Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(m);
        return m;
    }

    static void Box(Transform parent, string name, Material mat, Vector3 pos, Vector3 size, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = size; go.transform.localEulerAngles = euler;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void Do(this SerializedObject so, System.Action<SerializedObject> edit) { edit(so); so.ApplyModifiedPropertiesWithoutUndo(); }
}
