using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the road under Palm Bay's first lamp three ways (the wet road shader, the same shader dry, and URP's own
// Lit with the same textures) so a lighting problem can be pinned on the shader or on the scene, and prints what the
// shader compiler had to say. Run: ./tools/unity.sh NightProbe.Run   -> Builds/probe-*.png
public static class NightProbe
{
    [MenuItem("Build/Night Probe")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Race1.unity", OpenSceneMode.Single);
        var shader = Shader.Find("KartCup/Wet Road");
        Debug.Log($"[probe] quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}");
        if (shader != null)
        {
            Debug.Log($"[probe] shader supported={shader.isSupported} passes={shader.passCount} errors={ShaderUtil.ShaderHasError(shader)}");
            foreach (var m in ShaderUtil.GetShaderMessages(shader)) Debug.Log($"[probe] shader message: {m.severity} {m.message} ({m.file}:{m.line}) {m.messageDetails}");
        }
        // "Track" is also the HUD's label: go by the renderer named Road, like RoadProbe does
        var road = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Road");
        var lamp = GameObject.Find("Lamp02");     // Lamp00 stands at the start chequer, which covers the road under it
        if (road == null || lamp == null) { Debug.LogError($"[probe] road={road != null} lamp={lamp != null}"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        var mesh = road.GetComponent<MeshFilter>().sharedMesh;
        Debug.Log($"[probe] road mesh verts={mesh.vertexCount} tangents={mesh.tangents.Length} uv0={mesh.uv.Length} bounds={road.bounds} material={road.sharedMaterial.name} shader={road.sharedMaterial.shader.name}");
        var light = lamp.GetComponentInChildren<Light>();
        Debug.Log($"[probe] lamp light {light.type} i={light.intensity} r={light.range} at={light.transform.position} fwd={light.transform.forward}");
        // the road chunk nearest the lamp
        var chunks = road.transform.parent.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Road")).ToArray();
        var near = chunks.OrderBy(r => r.bounds.SqrDistance(light.transform.position)).First();
        Debug.Log($"[probe] nearest road chunk {near.name} bounds={near.bounds}");

        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        var main = Camera.main;
        if (main != null) cam.CopyFrom(main);
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        var under = light.transform.position; under.y = road.bounds.center.y;
        var toRoad = lamp.transform.forward; toRoad.y = 0; toRoad.Normalize();   // the lamp's +Z faces the road
        var at = under + toRoad * 7f;                                              // well onto the asphalt
        camGo.transform.position = at - toRoad * 11f + Vector3.up * 5f + Vector3.Cross(Vector3.up, toRoad) * 5f;
        camGo.transform.LookAt(at);
        cam.fieldOfView = 50f;

        var wet = near.sharedMaterial;
        Shot(cam, "probe-wet");
        var dry = new Material(wet); dry.SetFloat("_Wet", 0f);
        near.sharedMaterial = dry; Shot(cam, "probe-dry");
        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lit.SetTexture("_BaseMap", wet.GetTexture("_BaseMap")); lit.SetTexture("_BumpMap", wet.GetTexture("_BumpMap"));
        lit.EnableKeyword("_NORMALMAP"); lit.SetFloat("_Smoothness", 0.8f); lit.SetColor("_BaseColor", wet.GetColor("_Tint"));
        near.sharedMaterial = lit; Shot(cam, "probe-urplit");
        near.sharedMaterial = wet;
        // and a point light straight over the asphalt, on the wet shader and on URP Lit: no cone edge to argue about
        var probeLight = new GameObject("ProbeLight").AddComponent<Light>();
        probeLight.type = LightType.Point; probeLight.range = 22f; probeLight.intensity = 60f; probeLight.color = new Color(1f, 0.7f, 0.3f);
        probeLight.transform.position = at + Vector3.up * 4f;
        Shot(cam, "probe-wet-point");
        near.sharedMaterial = lit; Shot(cam, "probe-urplit-point");
        near.sharedMaterial = wet;
        Object.DestroyImmediate(probeLight.gameObject);
        // the two things a road chunk has that a kerb chunk lacks
        Object.DestroyImmediate(near.GetComponent<MeshCollider>()); Object.DestroyImmediate(near.GetComponent<PowerslideKartPhysics.GroundSurface>());
        Shot(cam, "probe-wet-nocol");
        // and the lamp as a point light instead of a spot
        light.type = LightType.Point;
        Shot(cam, "probe-wet-lamppoint");
        light.type = LightType.Spot;
        Object.DestroyImmediate(camGo);
        Debug.Log("[probe] wrote Builds/probe-{wet,dry,urplit,wet-point,urplit-point,wet-nocol,wet-lamppoint}.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static void Shot(Camera cam, string name)
    {
        Preview.Batcher(false);
        int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Preview.Batcher(true);
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/{name}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
