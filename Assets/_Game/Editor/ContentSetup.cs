using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assembles the content the plan describes, headlessly and idempotently: one unlit material per Kenney colormap,
// water/blob/sky materials, four driver prefabs (Blocky body + swapped box head + BlockyDriverPose), kart visual prefabs.
// Run: Unity -batchmode -projectPath . -executeMethod ContentSetup.Build -quit   (menu Build > Assemble Content)
public static class ContentSetup
{
    const string Art = "Assets/_Game/Art/";
    const string Mats = Art + "Materials/";
    const string Prefabs = "Assets/_Game/Prefabs/";

    [MenuItem("Build/Assemble Content")]
    public static void Build()
    {
        Directory.CreateDirectory(Mats); Directory.CreateDirectory(Prefabs + "Drivers"); Directory.CreateDirectory(Prefabs + "Karts"); Directory.CreateDirectory(Prefabs + "World");
        var unlit = Shader.Find("KartCup/Unlit Colormap");
        var water = Shader.Find("KartCup/Water Unlit");
        if (unlit == null || water == null) { Debug.LogError("[content] shaders not compiled yet"); Exit(1); return; }

        // ---- materials: one per colormap ----
        var carkit   = ColormapMat(unlit, "CarKit",     Art + "Textures/Colormaps/carkit_colormap.png");
        var toycar   = ColormapMat(unlit, "ToyCar",     Art + "Textures/Colormaps/toycar_colormap.png");
        var pirate   = ColormapMat(unlit, "Pirate",     Art + "Textures/Colormaps/pirate_colormap.png");
        var craft    = ColormapMat(unlit, "Watercraft", Art + "Textures/Colormaps/watercraft_colormap.png");
        var headMat  = ColormapMat(unlit, "HeadPalette", Art + "Textures/Colormaps/head_palette.png");
        var bodyMats = "abcdef".Select(c => ColormapMat(unlit, "Body_" + c, Art + $"Textures/Colormaps/texture-{c}.png")).ToArray();

        var waterMat = LoadOrCreate(Mats + "Water.mat", water);
        waterMat.SetTexture("_WaterA", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/World/water_tile_a.png"));
        waterMat.SetTexture("_WaterB", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/World/water_tile_b.png"));
        waterMat.SetTextureScale("_WaterA", new Vector2(8, 8)); waterMat.SetTextureScale("_WaterB", new Vector2(6, 6));

        var blobMat = LoadOrCreate(Mats + "BlobShadow.mat", Shader.Find("Universal Render Pipeline/Unlit"));
        blobMat.SetFloat("_Surface", 1); blobMat.SetFloat("_Blend", 0); blobMat.SetFloat("_ZWrite", 0);
        blobMat.renderQueue = 3000; blobMat.SetOverrideTag("RenderType", "Transparent");
        blobMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); blobMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/World/blob_shadow.png"));
        blobMat.SetColor("_BaseColor", new Color(0, 0, 0, 0.55f));
        blobMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); blobMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        var skyTex = AssetDatabase.LoadAssetAtPath<Cubemap>(Art + "Textures/Sky/skybox-day.png");
        if (skyTex != null) { var sky = LoadOrCreate(Mats + "Sky.mat", Shader.Find("Skybox/Cubemap")); sky.SetTexture("_Tex", skyTex); }

        // ---- blob shadow prefab ----
        var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(blob.GetComponent<Collider>());
        blob.name = "BlobShadow"; blob.transform.rotation = Quaternion.Euler(90, 0, 0); blob.transform.localScale = new Vector3(2.2f, 2.2f, 1);
        blob.GetComponent<MeshRenderer>().sharedMaterial = blobMat;
        SavePrefab(blob, Prefabs + "World/BlobShadow.prefab");

        // ---- driver prefabs: Blocky body + head mesh swap ----
        var drivers = new[] {
            ("Wizard",     "a", "head_wizard"),      // Toly caricature; name-swap flag lives in DriverRoster
            ("NodeRunner", "b", "head_noderunner"),  // Mert
            ("Builder",    "c", "head_builder"),     // Akshay BD
            ("Generic",    "d", "head_generic"),
        };
        foreach (var (name, body, head) in drivers)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Characters/character-{body}.fbx");
            var headMesh = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Heads/{head}.fbx")?.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            if (fbx == null || headMesh == null) { Debug.LogWarning($"[content] missing body/head for {name}"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Driver_" + name;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = bodyMats["abcdef".IndexOf(body)];
            var headT = Find(go.transform, "head");
            var headMf = headT.GetComponent<MeshFilter>(); headMf.sharedMesh = headMesh;
            headT.GetComponent<MeshRenderer>().sharedMaterial = headMat;
            foreach (var anim in go.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(anim);   // we pose the nodes ourselves
            var pose = go.AddComponent<KartCup.BlockyDriverPose>();
            var so = new SerializedObject(pose);
            so.FindProperty("torso").objectReferenceValue = Find(go.transform, "torso");
            so.FindProperty("armLeft").objectReferenceValue = Find(go.transform, "arm-left");
            so.FindProperty("armRight").objectReferenceValue = Find(go.transform, "arm-right");
            so.FindProperty("head").objectReferenceValue = headT;
            so.ApplyModifiedPropertiesWithoutUndo();
            ApplyDrivePose(go.transform);
            SavePrefab(go, Prefabs + $"Drivers/Driver_{name}.prefab");
        }

        // ---- kart visual prefabs: Car Kit body + wheels, seated driver slot, blob shadow ----
        foreach (var kart in new[] { "kart-oobi", "kart-oodi", "kart-ooli", "kart-oopi", "kart-oozi" })
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Karts/{kart}.fbx");
            if (fbx == null) { Debug.LogWarning("[content] missing " + kart); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "KartVisual_" + kart.Substring(5);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = carkit;
            var seated = Find(go.transform, "character"); if (seated) Object.DestroyImmediate(seated.gameObject);   // Kenney's placeholder driver
            var seat = new GameObject("DriverSeat").transform; seat.SetParent(go.transform, false); seat.localPosition = new Vector3(0, 0.28f, -0.07f); seat.localScale = Vector3.one * 0.38f;
            var shadow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "World/BlobShadow.prefab"));
            shadow.transform.SetParent(go.transform, false); shadow.transform.localPosition = new Vector3(0, 0.03f, 0);
            SavePrefab(go, Prefabs + $"Karts/{go.name}.prefab");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[content] assembled materials, drivers, karts");
        Exit(0);
    }

    /// Blocky `drive` clip is a static pose: root lowered 0.15, arms swung ±60° about Y. Bake it as the rest pose.
    static void ApplyDrivePose(Transform root)
    {
        var r = Find(root, "root"); if (r) r.localPosition += new Vector3(0, -0.15f, 0);
        var al = Find(root, "arm-left"); if (al) al.localRotation = Quaternion.Euler(0, -60, 0) * al.localRotation;
        var ar = Find(root, "arm-right"); if (ar) ar.localRotation = Quaternion.Euler(0, 60, 0) * ar.localRotation;
        var ll = Find(root, "leg-left"); if (ll) ll.localRotation = Quaternion.Euler(-75, 0, 0) * ll.localRotation;
        var lr = Find(root, "leg-right"); if (lr) lr.localRotation = Quaternion.Euler(-75, 0, 0) * lr.localRotation;
    }

    static Material ColormapMat(Shader s, string name, string texPath)
    {
        var m = LoadOrCreate(Mats + name + ".mat", s);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (t == null) Debug.LogWarning("[content] missing texture " + texPath); else m.SetTexture("_BaseMap", t);
        return m;
    }

    static Material LoadOrCreate(string path, Shader s)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != s) m.shader = s;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void Exit(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }
}
