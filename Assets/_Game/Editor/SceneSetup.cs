using System.Linq;
using Solana.Unity.SDK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds Assets/_Game/Scenes/Gate.unity headlessly: the gate-weekend scene (demo lap + dressing + wallet connect + fps).
// Run: Unity -batchmode -projectPath . -executeMethod SceneSetup.BuildGate -quit   (menu Build > Build Gate Scene)
public static class SceneSetup
{
    const string ScenePath = "Assets/_Game/Scenes/Gate.unity";
    const string Prefabs = "Assets/_Game/Prefabs/";
    const string Art = "Assets/_Game/Art/";

    [MenuItem("Build/Build Gate Scene")]
    public static void BuildGate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var unlit = Shader.Find("KartCup/Unlit Colormap");

        // ---- environment ----
        var sky = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Sky.mat");
        var skyTex = AssetDatabase.LoadAssetAtPath<Cubemap>(Art + "Textures/Sky/skybox-day.png");
        if (sky == null && skyTex != null) { sky = new Material(Shader.Find("Skybox/Cubemap")); sky.SetTexture("_Tex", skyTex); AssetDatabase.CreateAsset(sky, Art + "Materials/Sky.mat"); }
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.75f, 0.8f, 0.85f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 120; RenderSettings.fogEndDistance = 300;
        RenderSettings.fogColor = new Color(0.72f, 0.86f, 0.92f);

        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50, -30, 0); light.shadows = LightShadows.None;

        var sand = LoadOrCreate(Art + "Materials/Sand.mat", unlit, new Color(0.96f, 0.86f, 0.62f));
        var road = LoadOrCreate(Art + "Materials/Road.mat", unlit, new Color(0.45f, 0.44f, 0.46f));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Sand"; ground.transform.localScale = new Vector3(40, 1, 40);
        ground.GetComponent<MeshRenderer>().sharedMaterial = sand; ground.isStatic = true;

        // ring road: a flat torus-ish ring built from 48 quads
        var ring = new GameObject("Road"); ring.isStatic = true;
        var mf = ring.AddComponent<MeshFilter>(); var mr = ring.AddComponent<MeshRenderer>(); mr.sharedMaterial = road;
        mf.sharedMesh = RingMesh(18f, 27f, 48); ring.transform.position = new Vector3(0, 0.02f, 0);

        var waterMat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Water.mat");
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane); water.name = "Water"; water.transform.position = new Vector3(0, -0.35f, 0);
        water.transform.localScale = new Vector3(120, 1, 120); water.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
        Object.DestroyImmediate(water.GetComponent<Collider>());
        // island: raise the sand so the water shows only beyond ~150 m … cheap trick: scale sand smaller than water
        ground.transform.localScale = new Vector3(16, 1, 16);   // 160 m island, water beyond

        // ---- dressing: palms, rocks, buoys, item boxes ----
        var pirate = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Pirate.mat");
        var toycar = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/ToyCar.mat");
        var craft = AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/Watercraft.mat");
        var dressing = new GameObject("Dressing"); dressing.isStatic = true;
        var palms = new[] { "palm-straight", "palm-bend", "palm-detailed-straight", "palm-detailed-bend" };
        var rocks = new[] { "rocks-sand-a", "rocks-sand-b", "rocks-sand-c" };
        var rnd = new System.Random(7);
        for (int i = 0; i < 60; i++)
        {
            float a = i / 60f * Mathf.PI * 2f, r = (i % 2 == 0) ? 30f + (float)rnd.NextDouble() * 8f : 9f + (float)rnd.NextDouble() * 5f;
            Place(dressing.transform, Art + $"Models/Props/Pirate/{palms[i % palms.Length]}.fbx", pirate, new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), (float)rnd.NextDouble() * 360f, 0.85f + (float)rnd.NextDouble() * 0.45f);
        }
        for (int i = 0; i < 24; i++)
        {
            float a = (i + 0.5f) / 24f * Mathf.PI * 2f, r = 40f + (float)rnd.NextDouble() * 12f;
            Place(dressing.transform, Art + $"Models/Props/Pirate/{rocks[i % rocks.Length]}.fbx", pirate, new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), (float)rnd.NextDouble() * 360f, 1.0f + (float)rnd.NextDouble() * 0.9f);
        }
        for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; Place(dressing.transform, Art + "Models/Props/ToyCar/item-box.fbx", toycar, new Vector3(Mathf.Sin(a) * 22.5f, 0.9f, Mathf.Cos(a) * 22.5f), 0, 1.4f).AddComponent<KartCup.Spin>(); }
        for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f + 0.3f; Place(dressing.transform, Art + "Models/Props/Watercraft/buoy.fbx", craft, new Vector3(Mathf.Sin(a) * 95f, -0.2f, Mathf.Cos(a) * 95f), 0, 1.5f); }
        Place(dressing.transform, Art + "Models/Props/Pirate/tower-complete-large.fbx", pirate, new Vector3(-52, 0, 60), 20, 1.4f);
        Place(dressing.transform, Art + "Models/Props/Pirate/structure-platform-dock.fbx", pirate, new Vector3(70, 0, -10), 90, 2f);
        Place(dressing.transform, Art + "Models/Props/Pirate/ship-wreck.fbx", pirate, new Vector3(-75, -0.3f, -40), 140, 1.6f);

        // ---- kart + driver on a demo lap ----
        var kartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Karts/KartVisual_oobi.prefab");
        var kart = (GameObject)PrefabUtility.InstantiatePrefab(kartPrefab); kart.name = "PlayerKart";
        var rb = kart.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
        var driverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Drivers/Driver_Wizard.prefab");
        var driver = (GameObject)PrefabUtility.InstantiatePrefab(driverPrefab);
        var seat = kart.GetComponentsInChildren<Transform>().First(t => t.name == "DriverSeat");
        driver.transform.SetParent(seat, false);
        var pose = driver.GetComponent<KartCup.BlockyDriverPose>();
        SetRef(pose, "kartBody", rb);
        var wheels = kart.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("wheel")).ToArray();
        var drive = kart.AddComponent<KartCup.DemoDrive>();
        var so = new SerializedObject(drive);
        so.FindProperty("driver").objectReferenceValue = pose; so.FindProperty("rb").objectReferenceValue = rb;
        var wp = so.FindProperty("wheels"); wp.arraySize = wheels.Length; for (int i = 0; i < wheels.Length; i++) wp.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // AI karts on the same lap, offset — six karts on screen is the real load
        var kartNames = new[] { "oodi", "ooli", "oopi", "oozi", "oobi" };
        var driverNames = new[] { "NodeRunner", "Builder", "Generic", "Generic", "Generic" };
        for (int i = 0; i < 5; i++)
        {
            var kp = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/KartVisual_{kartNames[i]}.prefab");
            var k = (GameObject)PrefabUtility.InstantiatePrefab(kp); k.name = "AIKart_" + i;
            var d = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Drivers/Driver_{driverNames[i]}.prefab"));
            d.transform.SetParent(k.GetComponentsInChildren<Transform>().First(t => t.name == "DriverSeat"), false);
            var dd = k.AddComponent<KartCup.DemoDrive>();
            var s2 = new SerializedObject(dd);
            s2.FindProperty("radius").floatValue = 20f + i * 1.2f; s2.FindProperty("speed").floatValue = 12f + i * 0.7f;
            s2.FindProperty("driver").objectReferenceValue = d.GetComponent<KartCup.BlockyDriverPose>();
            var ws = k.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("wheel")).ToArray();
            var wp2 = s2.FindProperty("wheels"); wp2.arraySize = ws.Length; for (int j = 0; j < ws.Length; j++) wp2.GetArrayElementAtIndex(j).objectReferenceValue = ws[j];
            s2.ApplyModifiedPropertiesWithoutUndo();
            k.transform.position = new Vector3(0, 0, 20f + i * 1.2f); k.transform.Rotate(0, i * 25f, 0);
        }

        // ---- camera: chase, vertical FOV 62 ----
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 56; cam.nearClipPlane = 0.3f; cam.farClipPlane = 320; cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var follow = camGo.AddComponent<KartCup.ChaseCam>(); SetRef(follow, "target", kart.transform);
        camGo.AddComponent<KartCup.CameraFeel>();

        // ---- systems ----
        var boot = new GameObject("Boot"); boot.AddComponent<KartCup.GameBoot>(); boot.AddComponent<KartCup.HitStop>();
        var web3 = new GameObject("Web3").AddComponent<Web3>();
        web3.rpcCluster = RpcCluster.MainNet;
        web3.customRpc = "https://kartcup.bharadwaj465.workers.dev/rpc";     // TODO: your Worker's /rpc once the domain exists
        web3.solanaWalletAdapterOptions ??= new SolanaWalletAdapterOptions();
        web3.solanaWalletAdapterOptions.solanaMobileWalletAdapterOptions ??= new SolanaMobileWalletAdapterOptions();
        var mwa = web3.solanaWalletAdapterOptions.solanaMobileWalletAdapterOptions;
        mwa.identityUri = "https://kartcup.bharadwaj465.workers.dev/"; mwa.iconUri = "/icon.png"; mwa.name = "Seeker Kart Cup"; mwa.keepConnectionAlive = true;
        var wallet = new GameObject("WalletFlow").AddComponent<KartCup.WalletFlow>();

        // ---- UI (legacy uGUI Text; TMP resources come with the real HUD) ----
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(2400, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var safe = new GameObject("SafeArea", typeof(RectTransform)); safe.transform.SetParent(canvasGo.transform, false); safe.AddComponent<KartCup.SafeArea>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var fps = MakeText(safe.transform, "Fps", font, 34, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -16), new Vector2(1400, 50));
        var fc = boot.AddComponent<KartCup.FpsCounter>(); SetRef(fc, "label", fps);
        var status = MakeText(safe.transform, "Status", font, 34, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(24, 90), new Vector2(1400, 50));
        status.text = "Not connected";
        var btn = MakeButton(safe.transform, "Connect", font, "Use installed wallet", new Vector2(0, 0), new Vector2(24, 16), new Vector2(520, 64));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, wallet.Login);
        var relay = status.gameObject.AddComponent<KartCup.TextRelay>(); SetRef(relay, "label", status);
        // wire onStatus → relay.Set via SerializedProperty (persistent listener with a dynamic string arg)
        UnityEditor.Events.UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent<string>)wallet.GetType().GetField("onStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(wallet), relay.Set);

        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[scene] Gate scene built: " + ScenePath);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static GameObject Place(Transform parent, string fbxPath, Material mat, Vector3 pos, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) { Debug.LogWarning("[scene] missing " + fbxPath); return new GameObject("missing"); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * scale;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    static Material LoadOrCreate(string path, Shader s, Color tint)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); m.SetColor("_BaseColor", tint); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static Mesh RingMesh(float rIn, float rOut, int segs)
    {
        var v = new Vector3[segs * 2]; var uv = new Vector2[segs * 2]; var n = new Vector3[segs * 2]; var tri = new int[segs * 6];
        for (int i = 0; i < segs; i++)
        {
            float a = i / (float)segs * Mathf.PI * 2f; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            v[i * 2] = d * rIn; v[i * 2 + 1] = d * rOut; uv[i * 2] = new Vector2(0.03f, 0.5f); uv[i * 2 + 1] = new Vector2(0.03f, 0.5f); n[i * 2] = n[i * 2 + 1] = Vector3.up;
            int j = (i + 1) % segs;
            tri[i * 6] = i * 2; tri[i * 6 + 1] = i * 2 + 1; tri[i * 6 + 2] = j * 2;
            tri[i * 6 + 3] = i * 2 + 1; tri[i * 6 + 4] = j * 2 + 1; tri[i * 6 + 5] = j * 2;
        }
        var m = new Mesh { name = "Ring", vertices = v, uv = uv, normals = n, triangles = tri };
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, Art + "Models/Props/RingRoad.asset");
        return m;
    }

    /// A SerializedObject edit is a no-op until Apply — without this the refs silently stayed null (NRE in FpsCounter, ChaseCam never followed).
    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor anchor, Vector2 anchorPt, Vector2 offset, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = anchorPt; rt.anchoredPosition = offset; rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
        var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    static Button MakeButton(Transform parent, string name, Font font, string label, Vector2 anchorPt, Vector2 offset, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = anchorPt; rt.anchoredPosition = offset; rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>(); img.color = new Color(0.06f, 0.49f, 0.53f, 0.95f);
        var b = go.AddComponent<Button>();
        var t = MakeText(go.transform, "Label", font, 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, sizeDelta);
        t.text = label; ((RectTransform)t.transform).anchorMin = Vector2.zero; ((RectTransform)t.transform).anchorMax = Vector2.one; ((RectTransform)t.transform).sizeDelta = Vector2.zero;
        return b;
    }
}

