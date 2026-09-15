using System.IO;
using System.Linq;
using PowerslideKartPhysics;
using UnityEditor;
using UnityEngine;

// Builds playable kart prefabs: Powerslide's tuned physics rig with our Kenney art dropped in.
// Physics numbers are never touched — the art is scaled to the rig, not the other way round.
// Run: Unity -batchmode -projectPath . -executeMethod KartSetup.Build -quit   (menu Build > Assemble Karts)
public static class KartSetup
{
    const string Base = "Assets/ThirdParty/PowerslideKartPhysics/Prefabs/Karts/Kart Drift Auto.prefab";
    const string Prefabs = "Assets/_Game/Prefabs/Karts/";
    public static readonly string[] Karts = { "oobi", "oodi", "ooli", "oopi", "oozi" };

    [MenuItem("Build/Assemble Karts")]
    public static void Build()
    {
        Directory.CreateDirectory(Prefabs);
        foreach (var kart in Karts) BuildOne(kart);
        AssetDatabase.SaveAssets();
        Debug.Log($"[karts] built {Karts.Length} playable karts");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void BuildOne(string name)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Base);
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"KartVisual_{name}.prefab");
        if (basePrefab == null || visualPrefab == null) { Debug.LogError($"[karts] missing source for {name}"); return; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Kart_" + name;

        var kart = go.GetComponent<Kart>();
        var holder = Find(go.transform, "Visual Holder");

        // ---- fit: the kit kart is scaled to the rig's length, then the rig's wheels are moved onto the kit's
        // own wheel positions. Wheel placement is geometry, not tuned force constants, so the physics still holds.
        var oldBody = holder.GetComponent<MeshFilter>();
        var rigLength = oldBody.sharedMesh.bounds.size.z;
        oldBody.GetComponent<MeshRenderer>().enabled = false;

        var art = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
        PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var artWheels = "back-left,back-right,front-left,front-right".Split(',')
            .Select(w => art.transform.Find("wheel-" + w)).ToArray();
        var artSeat = art.transform.Find("DriverSeat");

        var bodyMesh = art.GetComponent<MeshFilter>().sharedMesh;
        float fit = rigLength / bodyMesh.bounds.size.z;
        float wheelRadius = artWheels[0].GetComponent<MeshFilter>().sharedMesh.bounds.size.y * 0.5f;   // kit wheels are modelled sitting on the ground
        float axleY = holder.GetComponentInChildren<KartWheel>().transform.localPosition.y;

        var body = new GameObject("Body");
        body.transform.SetParent(holder, false);
        body.transform.localScale = Vector3.one * fit;
        body.transform.localPosition = new Vector3(0, axleY - wheelRadius * fit, 0);   // line the kit's axle height up with the rig's
        body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
        body.AddComponent<MeshRenderer>().sharedMaterials = art.GetComponent<MeshRenderer>().sharedMaterials;

        // ---- wheels: the rig's suspension, moved and sized to the kit's own wheels ----
        foreach (var wheel in go.GetComponentsInChildren<KartWheel>())
        {
            var visual = wheel.visualWheel != null ? wheel.visualWheel : wheel.transform.Find("Visual Mesh");
            bool front = wheel.name.Contains("F"), left = wheel.name.Contains("L");
            var src = artWheels[(front ? 2 : 0) + (left ? 0 : 1)];
            var mf = visual.GetComponent<MeshFilter>();
            mf.sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
            visual.localScale = new Vector3(fit * (left ? -1 : 1), fit, fit);          // kit wheels face +x: mirror the left pair
            visual.localPosition = Vector3.zero;

            var p = src.localPosition * fit;
            wheel.transform.localPosition = new Vector3(p.x, axleY, p.z);
            float scaledRadius = wheelRadius * fit;
            wheel.suspensionDistance *= scaledRadius / wheel.radius;                   // ride height follows the wheel size
            wheel.radius = scaledRadius;
            wheel.width = src.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * fit;
        }

        // ---- driver seat: keep the kit's own offset, scaled onto the rig ----
        var seat = new GameObject("DriverSeat").transform;
        seat.SetParent(body.transform, false);
        if (artSeat != null) { seat.localPosition = artSeat.localPosition; seat.localScale = artSeat.localScale; }

        // ---- blob shadow under the body, flattened to the ground ----
        var blobSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/World/BlobShadow.prefab");
        if (blobSrc != null)
        {
            var blob = (GameObject)PrefabUtility.InstantiatePrefab(blobSrc);
            blob.transform.SetParent(holder, false);
            blob.transform.localPosition = new Vector3(0, 0.02f, 0);
            blob.transform.localScale = Vector3.one * rigLength * 0.9f;
        }

        // input is added by the spawner (touch for the player, waypoint AI for the rest)
        foreach (var input in go.GetComponents<KartInput>()) Object.DestroyImmediate(input);
        kart.wheels = go.GetComponentsInChildren<KartWheel>();
        var presets = go.GetComponent<KartPresetControl>();
        if (presets != null) presets.loadOnAwake = false;    // otherwise a preset reload would stamp over the per-character stats

        Object.DestroyImmediate(art);
        PrefabUtility.SaveAsPrefabAsset(go, Prefabs + go.name + ".prefab");
        Object.DestroyImmediate(go);
        Debug.Log($"[karts] {name}: fit x{fit:F2}, rig {rigLength:F2} m, wheel r={wheelRadius * fit:F2}");
    }

    static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
}
