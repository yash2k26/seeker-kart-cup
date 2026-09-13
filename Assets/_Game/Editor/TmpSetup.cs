using UnityEditor;
using UnityEngine;

// Imports TextMeshPro's essential resources (TMP Settings, default SDF font, shaders) without the modal dialog.
public static class TmpSetup
{
    [MenuItem("Build/Import TMP Essentials")]
    public static void Import()
    {
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Resources")) { Debug.Log("[tmp] already imported"); Quit(); return; }
        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        AssetDatabase.Refresh();
        Debug.Log("[tmp] essentials imported: " + AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Resources"));
        Quit();
    }
    static void Quit() { if (Application.isBatchMode) EditorApplication.Exit(0); }
}
