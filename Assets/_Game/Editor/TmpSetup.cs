using UnityEditor;
using UnityEngine;

// Imports TextMeshPro's essential resources (TMP Settings, default SDF font, shaders) without the modal dialog.
// ImportPackage is asynchronous even in batch mode, so we exit from the completion callback (run WITHOUT -quit).
public static class TmpSetup
{
    [MenuItem("Build/Import TMP Essentials")]
    public static void Import()
    {
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Resources")) { Debug.Log("[tmp] already imported"); Quit(0); return; }
        var pkg = System.IO.Path.Combine(EditorApplication.applicationContentsPath, "Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
        if (!System.IO.File.Exists(pkg)) { Debug.LogError("[tmp] package not found: " + pkg); Quit(1); return; }
        AssetDatabase.importPackageCompleted += n => { Debug.Log("[tmp] imported " + n + " ok=" + AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Resources")); Quit(0); };
        AssetDatabase.importPackageFailed += (n, e) => { Debug.LogError("[tmp] failed " + n + ": " + e); Quit(1); };
        AssetDatabase.importPackageCancelled += n => { Debug.LogError("[tmp] cancelled " + n); Quit(1); };
        double deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update += () => { if (EditorApplication.timeSinceStartup > deadline) { Debug.LogError("[tmp] timeout"); Quit(1); } };
        AssetDatabase.ImportPackage(pkg, false);
    }
    static void Quit(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }
}
