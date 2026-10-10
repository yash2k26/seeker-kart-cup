using KartCup;
using UnityEditor;
using UnityEngine;

/// Resources/Chicken and one animal per circuit (Critter.cs loads Resources/Critter_<track id>): a blocky crab on the
/// beach, a penguin on the ice, a duck by the forest lake, in the drivers' style at the race's toy scale (karts are
/// 3.75 m long), each with the trigger Critter.cs needs and the squawk pitched for it. The Speedway keeps the hen.
///     tools/unity.sh ChickenSetup.Build
public static class ChickenSetup
{
    const string Mats = "Assets/_Game/Art/Materials/";

    [MenuItem("Build/Critters")]
    public static void Build()
    {
        Chicken(); Crab(); Penguin(); Duck();
        AssetDatabase.SaveAssets();
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Chicken()
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
        Save(root, "Chicken", 0.55f, new Vector3(0, 0.55f, 0.05f), 1f, false, new Color(1f, 0.98f, 0.94f));
    }

    /// Palm Bay's: a crab that crosses the road side-on, claws up (Beach Buggy's Crab Cove).
    static void Crab()
    {
        var shell = Mat("Critter_Crab", new Color(1f, 0.42f, 0.15f), 0.35f);
        var dark = Mat("Critter_CrabDark", new Color(0.86f, 0.24f, 0.09f), 0.35f);
        // Palm Bay is after dark: a little of the shell's own colour so the crab reads as orange, not brown, under the moon
        foreach (var glow in new[] { shell, dark }) { glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", glow.GetColor("_BaseColor") * 0.22f); EditorUtility.SetDirty(glow); }
        var white = Mat("Chicken_White", new Color(0.97f, 0.95f, 0.9f), 0.15f);
        var black = Mat("Chicken_Eye", new Color(0.05f, 0.05f, 0.06f), 0.6f);
        var root = new GameObject("Critter_beach");
        var m = new GameObject("Model").transform; m.SetParent(root.transform, false);
        Box(m, "Shell", shell, new Vector3(0, 0.42f, 0), new Vector3(1.0f, 0.32f, 0.72f));
        Box(m, "Shell Top", shell, new Vector3(0, 0.6f, -0.02f), new Vector3(0.8f, 0.12f, 0.56f));
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Box(m, "Stalk " + side, dark, new Vector3(s * 0.16f, 0.72f, 0.28f), new Vector3(0.06f, 0.24f, 0.06f));
            Box(m, "Eye " + side, white, new Vector3(s * 0.16f, 0.86f, 0.28f), new Vector3(0.13f, 0.13f, 0.13f));
            Box(m, "Pupil " + side, black, new Vector3(s * 0.16f, 0.86f, 0.35f), new Vector3(0.07f, 0.07f, 0.02f));
            Box(m, "Arm " + side, dark, new Vector3(s * 0.54f, 0.46f, 0.32f), new Vector3(0.14f, 0.12f, 0.32f), new Vector3(0, s * -25f, 0));
            Box(m, "Claw " + side, shell, new Vector3(s * 0.64f, 0.54f, 0.54f), new Vector3(0.28f, 0.24f, 0.26f), new Vector3(0, 0, s * 12f));
            for (int k = 0; k < 3; k++)
                Box(m, $"Leg {side}{k}", dark, new Vector3(s * 0.6f, 0.22f, -0.22f + k * 0.2f), new Vector3(0.34f, 0.07f, 0.07f), new Vector3(0, 0, s * -35f));
        }
        Save(root, "Critter_beach", 0.62f, new Vector3(0, 0.45f, 0), 1.9f, true, new Color(0.86f, 0.78f, 0.6f));
    }

    /// Frost Peak's: a penguin, waddling.
    static void Penguin()
    {
        var black = Mat("Critter_PenguinBlack", new Color(0.08f, 0.09f, 0.13f), 0.45f);
        var white = Mat("Chicken_White", new Color(0.97f, 0.95f, 0.9f), 0.15f);
        var orange = Mat("Chicken_Orange", new Color(1f, 0.62f, 0.12f), 0.25f);
        var eye = Mat("Chicken_Eye", new Color(0.05f, 0.05f, 0.06f), 0.6f);
        var root = new GameObject("Critter_alpine");
        var m = new GameObject("Model").transform; m.SetParent(root.transform, false);
        Box(m, "Body", black, new Vector3(0, 0.62f, 0), new Vector3(0.6f, 0.9f, 0.5f));
        Box(m, "Belly", white, new Vector3(0, 0.58f, 0.21f), new Vector3(0.46f, 0.72f, 0.1f));
        Box(m, "Head", black, new Vector3(0, 1.18f, 0.02f), new Vector3(0.44f, 0.36f, 0.42f));
        Box(m, "Face", white, new Vector3(0, 1.16f, 0.2f), new Vector3(0.34f, 0.2f, 0.06f));
        Box(m, "Beak", orange, new Vector3(0, 1.12f, 0.3f), new Vector3(0.12f, 0.08f, 0.16f));
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Box(m, "Eye " + side, eye, new Vector3(s * 0.09f, 1.2f, 0.235f), new Vector3(0.06f, 0.07f, 0.02f));
            Box(m, "Flipper " + side, black, new Vector3(s * 0.34f, 0.68f, 0), new Vector3(0.08f, 0.5f, 0.26f), new Vector3(0, 0, s * 12f));
            Box(m, "Foot " + side, orange, new Vector3(s * 0.13f, 0.04f, 0.12f), new Vector3(0.16f, 0.06f, 0.24f));
        }
        Save(root, "Critter_alpine", 0.55f, new Vector3(0, 0.65f, 0), 0.75f, false, new Color(0.95f, 0.97f, 1f));
    }

    /// Pine Ridge's: a mallard from the lake.
    static void Duck()
    {
        var body = Mat("Critter_DuckBody", new Color(0.55f, 0.47f, 0.38f), 0.2f);
        var wing = Mat("Critter_DuckWing", new Color(0.40f, 0.34f, 0.28f), 0.2f);
        var head = Mat("Critter_DuckHead", new Color(0.08f, 0.40f, 0.20f), 0.55f);
        var white = Mat("Chicken_White", new Color(0.97f, 0.95f, 0.9f), 0.15f);
        var bill = Mat("Critter_DuckBill", new Color(1f, 0.76f, 0.16f), 0.3f);
        var orange = Mat("Chicken_Orange", new Color(1f, 0.62f, 0.12f), 0.25f);
        var eye = Mat("Chicken_Eye", new Color(0.05f, 0.05f, 0.06f), 0.6f);
        var root = new GameObject("Critter_forest");
        var m = new GameObject("Model").transform; m.SetParent(root.transform, false);
        Box(m, "Body", body, new Vector3(0, 0.45f, -0.02f), new Vector3(0.56f, 0.42f, 0.86f));
        Box(m, "Tail", wing, new Vector3(0, 0.6f, -0.48f), new Vector3(0.3f, 0.2f, 0.16f), new Vector3(-30f, 0, 0));
        Box(m, "Ring", white, new Vector3(0, 0.74f, 0.3f), new Vector3(0.24f, 0.06f, 0.24f));
        Box(m, "Head", head, new Vector3(0, 0.92f, 0.32f), new Vector3(0.3f, 0.32f, 0.32f));
        Box(m, "Bill", bill, new Vector3(0, 0.86f, 0.54f), new Vector3(0.2f, 0.07f, 0.2f));
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Box(m, "Wing " + side, wing, new Vector3(s * 0.3f, 0.5f, -0.05f), new Vector3(0.06f, 0.28f, 0.56f));
            Box(m, "Eye " + side, eye, new Vector3(s * 0.152f, 0.97f, 0.38f), new Vector3(0.02f, 0.06f, 0.06f));
            Box(m, "Leg " + side, orange, new Vector3(s * 0.12f, 0.13f, 0), new Vector3(0.06f, 0.26f, 0.06f));
            Box(m, "Foot " + side, orange, new Vector3(s * 0.12f, 0.02f, 0.07f), new Vector3(0.18f, 0.04f, 0.2f));
        }
        Save(root, "Critter_forest", 0.55f, new Vector3(0, 0.5f, 0.05f), 1.25f, false, new Color(1f, 0.98f, 0.94f));
    }

    /// Toy scale, the trigger, the Critter with its squawk, shadows on, saved to Resources.
    static void Save(GameObject root, string name, float radius, Vector3 centre, float pitch, bool sideways, Color puff)
    {
        root.transform.localScale = Vector3.one * 1.8f;   // the size of a driver's head: big enough to aim at, or round
        var col = root.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = radius; col.center = centre;
        var c = root.AddComponent<Critter>();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/chicken_squawk.wav");
        new SerializedObject(c).Do(s =>
        {
            s.FindProperty("squawk").objectReferenceValue = clip;
            s.FindProperty("pitch").floatValue = pitch;
            s.FindProperty("sideways").boolValue = sideways;
            s.FindProperty("puffColor").colorValue = puff;
        });
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(root, $"Assets/_Game/Resources/{name}.prefab");
        Object.DestroyImmediate(root);
        Debug.Log($"[items] {name} prefab written (squawk {(clip != null ? "set" : "MISSING")})");
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
