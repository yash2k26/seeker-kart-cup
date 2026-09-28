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
    // The free five, then the garage. Order here is the garage order.
    public static readonly string[] Karts = { "oobi", "oodi", "ooli", "oopi", "oozi",
                                              "taxi", "van", "hatchback-sports", "police", "firetruck", "garbage-truck", "tractor", "race-future" };

    // id, shown name, blurb, sku ("" = free). Names and blurbs mirror worker/src/catalog.ts, which holds the prices.
    static readonly (string id, string name, string blurb, string sku)[] Garage =
    {
        ("oobi", "Oobi", "The one everybody learns on.", ""),
        ("oodi", "Oodi", "Short wheelbase, quick hands.", ""),
        ("ooli", "Ooli", "Sits flat through the sweepers.", ""),
        ("oopi", "Oopi", "Big tyres, big attitude.", ""),
        ("oozi", "Oozi", "All gas, some brakes.", ""),
        ("taxi", "Mempool", "Takes everyone for a ride.", "kart.taxi"),
        ("van", "Airdrop", "Delivers. Eventually.", "kart.van"),
        ("hatchback-sports", "Hot Hatch", "Point it and it goes.", "kart.hatchback-sports"),
        ("police", "Slasher", "Enforces the racing line.", "kart.police"),
        ("firetruck", "Hotfix", "A fire truck. In a kart race. Yes.", "kart.firetruck"),
        ("garbage-truck", "Garbage Chain", "Hauls the whole mempool.", "kart.garbage-truck"),
        ("tractor", "Yield Farmer", "Slow off the line, unstoppable after.", "kart.tractor"),
        ("race-future", "Photon", "Low, wide, glued to the road.", "kart.race-future"),
    };

    [MenuItem("Build/Assemble Karts")]
    public static void Build()
    {
        Directory.CreateDirectory(Prefabs);
        foreach (var kart in Karts) BuildOne(kart);
        Database();
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
        // Car Kit vehicles keep part of their real size (ContentSetup.Vehicles); the kit karts fill the rig exactly.
        float footprint = ContentSetup.Footprint(name);
        float fit = rigLength * footprint / bodyMesh.bounds.size.z;
        if (Mathf.Abs(rigLength - 3.75f) > 0.05f) Debug.LogWarning($"[karts] rig is {rigLength:F2} m, ContentSetup authors vehicle seats against 3.75: re-check ContentSetup.RigLength");
        // The rig's box collider grows with a longer body, so a truck's nose stops at the wall where it appears to.
        if (footprint != 1f)
            foreach (var box in go.GetComponentsInChildren<BoxCollider>())
            {
                var size = box.size; size.z *= footprint; box.size = size;
                var centre = box.center; centre.z *= footprint; box.center = centre;
            }
        // Kart wheels are drawn a fifth bigger than the kit models them: chunky tyres are what makes a kart read as a
        // racing machine rather than a toy. The body rides higher by the extra radius; physics follows the visual.
        const float WheelScale = 1.2f;
        float wheelRadius = artWheels[0].GetComponent<MeshFilter>().sharedMesh.bounds.size.y * 0.5f * WheelScale;   // kit wheels are modelled sitting on the ground
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
            // The rig spins a wheel whose axle runs along the visual's +Z; Kenney models theirs along +X, so the mesh
            // goes on a child turned 90 degrees. Mirroring the left pair with a negative scale reversed the spin.
            var mf = visual.GetComponent<MeshFilter>();
            mf.sharedMesh = null;
            visual.GetComponent<MeshRenderer>().enabled = false;
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;

            // GetComponent returns Unity's fake-null, which ?? happily accepts: check explicitly.
            var spinner = visual.Find("Tyre");
            if (spinner == null) spinner = new GameObject("Tyre").transform;
            spinner.SetParent(visual, false);
            // Each wheel frame points outwards; preserve the source mesh's signed axle offset.
            spinner.localRotation = Quaternion.Euler(0, left ? 90f : -90f, 0);
            spinner.localScale = Vector3.one * fit * WheelScale;
            var spinMesh = spinner.GetComponent<MeshFilter>();
            if (spinMesh == null) spinMesh = spinner.gameObject.AddComponent<MeshFilter>();
            var spinRenderer = spinner.GetComponent<MeshRenderer>();
            if (spinRenderer == null) spinRenderer = spinner.gameObject.AddComponent<MeshRenderer>();
            spinMesh.sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
            spinRenderer.sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;

            var p = src.localPosition * fit;
            // Mixed wheel sizes (the tractor): every tyre's bottom must sit on the same ground line, so a smaller
            // wheel's hub is lowered by the radius difference.
            float srcRadius = src.GetComponent<MeshFilter>().sharedMesh.bounds.size.y * 0.5f * WheelScale;
            wheel.transform.localPosition = new Vector3(p.x, axleY - (wheelRadius - srcRadius) * fit, p.z);
            float scaledRadius = srcRadius * fit;
            wheel.suspensionDistance *= scaledRadius / wheel.radius;                   // ride height follows the wheel size
            wheel.radius = scaledRadius;
            wheel.width = src.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * fit * WheelScale;
        }

        // ---- rear wing on the karts: a kit spoiler sized to the body, over the engine ----
        if (name.StartsWith("oo"))
        {
            var wingFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/Karts/debris-spoiler-a.fbx");
            if (wingFbx != null)
            {
                var wing = (GameObject)PrefabUtility.InstantiatePrefab(wingFbx);
                PrefabUtility.UnpackPrefabInstance(wing, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                wing.name = "Body";   // named like the body so the racer's paint (CharacterDef.Paint) covers it too
                wing.transform.SetParent(body.transform, false);
                var wb = wing.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                var bb = bodyMesh.bounds;
                float ws = bb.size.x * 0.72f / Mathf.Max(0.01f, wb.size.x);
                wing.transform.localScale = Vector3.one * ws;
                wing.transform.localPosition = new Vector3(0f, bb.max.y - wb.min.y * ws + 0.02f, bb.min.z + bb.size.z * 0.10f);
                wing.transform.localRotation = Quaternion.identity;
                foreach (var r in wing.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterials = art.GetComponent<MeshRenderer>().sharedMaterials;
            }
        }

        // ---- driver seat: keep the kit's own offset, scaled onto the rig ----
        var seat = new GameObject("DriverSeat").transform;
        seat.SetParent(body.transform, false);
        if (artSeat != null) { seat.localPosition = artSeat.localPosition; seat.localScale = artSeat.localScale; }
        // The kit's seat empty is authored for a standing root, but every driver plays a seated "drive" clip that
        // drops the skeleton root by 0.15 in driver space (Build/Probe Driver Sink). Without this the torso sits
        // inside the bodywork and only the head shows. Compensating here fixes the podium, the grid and the race at once.
        seat.localPosition += Vector3.up * (0.15f * seat.localScale.y);

        // ---- blob shadow under the body, flattened to the ground ----
        var blobSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/World/BlobShadow.prefab");
        if (blobSrc != null)
        {
            var blob = (GameObject)PrefabUtility.InstantiatePrefab(blobSrc);
            blob.transform.SetParent(holder, false);
            blob.transform.localPosition = new Vector3(0, axleY - wheelRadius * fit + 0.025f, 0);
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

    /// Karts.asset: what the garage shows and the spawner reads. Rebuilt every run so a renamed kart never goes stale.
    static void Database()
    {
        const string dir = "Assets/_Game/Data/";
        Directory.CreateDirectory(dir);
        var defs = new System.Collections.Generic.List<KartCup.KartDef>();
        foreach (var g in Garage)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Kart_{g.id}.prefab") == null) continue;   // no prefab, no garage entry
            var path = dir + $"Kart_{g.id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<KartCup.KartDef>(path);
            if (def == null) { def = ScriptableObject.CreateInstance<KartCup.KartDef>(); AssetDatabase.CreateAsset(def, path); }
            def.id = g.id; def.displayName = g.name; def.blurb = g.blurb; def.sku = g.sku;
            def.speed = def.accel = def.handling = def.weight = 0;   // cosmetic: the stats live on the drivers
            EditorUtility.SetDirty(def);
            defs.Add(def);
        }
        var dbPath = dir + "Karts.asset";
        var db = AssetDatabase.LoadAssetAtPath<KartCup.KartDatabase>(dbPath);
        if (db == null) { db = ScriptableObject.CreateInstance<KartCup.KartDatabase>(); AssetDatabase.CreateAsset(db, dbPath); }
        db.karts = defs.ToArray();
        EditorUtility.SetDirty(db);
        Debug.Log($"[karts] garage: {defs.Count} karts ({defs.Count(d => !d.IsFree)} for sale)");
    }

    static Transform Find(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
}
