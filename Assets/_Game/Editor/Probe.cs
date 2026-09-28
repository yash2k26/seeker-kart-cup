using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Probe
{
    [MenuItem("Build/Probe Race Scene")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race1.unity", OpenSceneMode.Single);
        var slot = GameObject.Find("StartGrid").transform.GetChild(0);
        Debug.Log($"[p] slot0 {slot.position}");
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                     .Where(r => Vector3.Distance(r.bounds.ClosestPoint(slot.position), slot.position) < 30f)
                     .OrderBy(r => Vector3.Distance(r.bounds.center, slot.position)).Take(14))
        {
            var m = r.sharedMaterial;
            Debug.Log($"[p] {r.name,-14} shader={m?.shader.name,-26} colour={(m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")} map={(m != null && m.HasProperty("_BaseMap") ? (m.GetTexture("_BaseMap")?.name ?? "none") : "-")} bounds={r.bounds.center} size={r.bounds.size}");
        }
        var sun = Object.FindFirstObjectByType<Light>();
        Debug.Log($"[p] sun {sun.type} shadows={sun.shadows} intensity={sun.intensity} ambient={RenderSettings.ambientMode} sky={RenderSettings.ambientSkyColor}");
        var urp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        Debug.Log($"[p] urp shadows={urp.supportsMainLightShadows} dist={urp.shadowDistance} cascades={urp.shadowCascadeCount} msaa={urp.msaaSampleCount}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
    /// What the stock head actually measures on each driver model, versus the hardcoded 0.454 the head swap assumes.
    [MenuItem("Build/Probe Heads")]
    public static void Heads()
    {
        foreach (var model in new[] { "character-male-a", "character-male-b", "character-male-c", "character-male-d",
                                      "character-female-a", "character-female-b", "character-female-c", "character-female-d" })
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Art/Models/Drivers/{model}.fbx");
            if (fbx == null) { Debug.Log($"[head] {model} MISSING"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .FirstOrDefault(r => r.name.ToLowerInvariant().Contains("head"));
            var bone = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head");
            var body = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .FirstOrDefault(r => r.name.ToLowerInvariant().Contains("body"));
            if (smr != null)
            {
                smr.updateWhenOffscreen = true;
                var baked = new Mesh(); smr.BakeMesh(baked, true);
                var ls = smr.transform.lossyScale;
                var b = baked.bounds;
                Debug.Log($"[head] {model} baked size {Vector3.Scale(b.size, ls)} min {Vector3.Scale(b.min, ls)} | meshBounds {smr.sharedMesh.bounds.size} | rendererLossy {ls} | bonePos {(bone != null ? bone.position.ToString() : "-")} boneLossy {(bone != null ? bone.lossyScale.ToString() : "-")} | bodyWorld {(body != null ? body.bounds.size.ToString() : "-")}");
                Object.DestroyImmediate(baked);
            }
            Object.DestroyImmediate(go);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// Seat scales, so the sink measured in driver space can be converted to kart space.
    [MenuItem("Build/Probe Seats")]
    public static void Seats()
    {
        foreach (var path in System.IO.Directory.GetFiles("Assets/_Game/Prefabs/Karts", "Kart_*.prefab"))
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var seat = src.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
            if (seat != null)
                Debug.Log($"[seat] {System.IO.Path.GetFileNameWithoutExtension(path)} localPos {seat.localPosition} localScale {seat.localScale} lossy {seat.lossyScale}");
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// How far the seated "drive" clip drops the driver relative to the bind pose the editor preview shows.
    [MenuItem("Build/Probe Driver Sink")]
    public static void DriverSink()
    {
        foreach (var path in System.IO.Directory.GetFiles("Assets/_Game/Prefabs/Drivers", "Driver_*.prefab"))
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var root = skin != null && skin.rootBone != null ? skin.rootBone : go.transform;
            Vector3 bind = root.localPosition;
            var bindB = skin != null ? skin.bounds : new Bounds();

            var name = System.IO.Path.GetFileNameWithoutExtension(path).Replace("Driver_", "");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Game/Prefabs/Drivers/{name}_drive.anim");
            if (clip != null) clip.SampleAnimation(go, 0f);
            if (skin != null) { skin.updateWhenOffscreen = true; }
            Vector3 posed = root.localPosition;
            var posedB = skin != null ? skin.bounds : new Bounds();

            Debug.Log($"[sink] {name} root {bind.y:F3} -> {posed.y:F3} (d={posed.y - bind.y:F3})  prefabScale {go.transform.localScale.y:F3}  rootParentScale {root.parent.lossyScale.y:F3}");
            Object.DestroyImmediate(go);
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

}