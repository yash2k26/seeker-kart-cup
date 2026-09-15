using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Powerslide ships built-in-pipeline materials (pink under URP: tyre marks, particles, demo props).
// Remap them once to URP shaders. Run: -executeMethod PowerslideUrp.Convert  (menu Build > Convert Powerslide Materials)
public static class PowerslideUrp
{
    [MenuItem("Build/Convert Powerslide Materials")]
    public static void Convert()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        int converted = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ThirdParty/PowerslideKartPhysics" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            var name = mat.shader.name;
            if (name.StartsWith("Universal Render Pipeline")) continue;

            var colour = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            var tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            bool transparent = name.Contains("Particle") || name.Contains("Transparent") || mat.renderQueue >= 3000 || colour.a < 0.99f;

            mat.shader = name.Contains("Particle") ? particle : transparent ? unlit : lit;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_ZWrite", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
            converted++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[urp] converted {converted} Powerslide materials");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
