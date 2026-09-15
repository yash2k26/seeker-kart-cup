using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Probe
{
    [MenuItem("Build/Probe Race Scene")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race.unity", OpenSceneMode.Single);
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
}
