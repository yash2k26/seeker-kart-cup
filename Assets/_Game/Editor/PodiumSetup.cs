using System.Collections.Generic;
using System.IO;
using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;

/// The ceremony stage PodiumShow drives: three podium blocks, gold, silver and bronze trophies, a backdrop with the
/// title, two party poppers, falling confetti and its own camera. Built into Resources/PodiumStage, so every race scene
/// makes one at runtime and none of the generated scenes needs rebuilding.
///   ./tools/unity.sh PodiumSetup.Build      then      ./tools/unity.sh PodiumSetup.Capture   (Builds/podium-*.png)
public static class PodiumSetup
{
    const string Dir = "Assets/_Game/Art/Models/Props/Podium/";
    const string ClipDir = "Assets/_Game/Prefabs/Podium/";
    const string PrefabPath = "Assets/_Game/Resources/PodiumStage.prefab";
    const string Drivers = "Assets/_Game/Prefabs/Drivers/";

    // The podium, in the stage's space: the camera is at -Z looking +Z, so second place stands on the left.
    static readonly (float x, float h)[] Blocks = { (0f, 1.25f), (-1.8f, 0.85f), (1.8f, 0.6f) };
    const float BlockW = 1.7f, BlockD = 1.5f;
    static readonly string[] Place = { "1", "2", "3" };

    [MenuItem("Build/Podium Stage")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir); Directory.CreateDirectory(ClipDir); Directory.CreateDirectory("Assets/_Game/Resources");
        Design.Fonts();

        // ---- clips and measurements off the real rigs ----
        var rigs = new List<PodiumShow.Rig>(); float standH = 1f;
        foreach (var path in Directory.GetFiles(Drivers, "Driver_*.prefab").OrderBy(p => p))
        {
            var prefabName = Path.GetFileNameWithoutExtension(path);
            var model = ModelOf(path);
            if (model == null) continue;
            var clip = Clip(model, "holding-both", $"podium_hold_{prefabName.Replace("Driver_", "")}");
            if (clip == null) continue;
            var (rig, height) = Measure(prefabName, clip);
            rigs.Add(rig);
            standH = Mathf.Max(standH, height);
            Debug.Log($"[podium] {prefabName}: over-head {rig.overHead:F3} side R{rig.sideRight:F3} L{rig.sideLeft:F3} stand {height:F2} m");
        }

        // ---- materials and meshes ----
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var gold = Metal("Trophy_Gold", lit, new Color(1f, 0.74f, 0.2f));
        var silver = Metal("Trophy_Silver", lit, new Color(0.83f, 0.86f, 0.9f));
        var bronze = Metal("Trophy_Bronze", lit, new Color(0.78f, 0.45f, 0.22f));
        var plinthMat = Plain("Podium_Plinth", lit, Design.Ink900, 0.45f);
        var blockMat = Plain("Podium_Block", lit, new Color(0.05f, 0.07f, 0.09f), 0.82f);
        var floorMat = Plain("Podium_Floor", lit, new Color(0.02f, 0.03f, 0.04f), 0.55f);
        var popperMat = Metal("Podium_Popper", lit, new Color(1f, 0.74f, 0.2f));
        var confettiMat = Confetti("Podium_Confetti");
        var cup = Save(CupMesh(), "trophy_cup");
        var cone = Save(PopperMesh(), "popper_cone");
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

        // ---- the stage ----
        // A night stage in the game's own gold: a black gloss floor ringed in light, the sun turning slowly behind the
        // podium, the blocks dark with their place lit on the front, glitter rising through all of it. Nothing
        // borrowed from a paddock (the banner towers, the flags and the red carpet are gone).
        var root = new GameObject("PodiumStage");
        var show = root.AddComponent<PodiumShow>();
        var goldC = Hex("FFC83A"); var silverC = new Color(0.83f, 0.88f, 0.95f); var bronzeC = new Color(0.95f, 0.55f, 0.28f);
        Part(root.transform, "Floor", cylinder, floorMat, new Vector3(0, -0.1f, 0), new Vector3(30, 0.1f, 30));
        // The stage brings its own night: a dark drum round it, drawn from the inside, so the ceremony looks the same
        // on a snowfield at blue hour as on the coast after dark.
        var drum = Part(root.transform, "Drum", cylinder, Inside("Podium_Drum", new Color(0.025f, 0.04f, 0.065f)), new Vector3(0, 13f, 0), new Vector3(40f, 15f, 40f));
        var ringMat = Glow("Podium_Ring", "fx_ring", goldC * 0.75f);
        var ring = Part(root.transform, "FloorRing", quad, ringMat, new Vector3(0, 0.02f, 0.2f), new Vector3(13f, 13f, 1f));
        ring.localRotation = Quaternion.Euler(90, 0, 0);
        var sunMat = Glow("Podium_Sun", SunTexture(), goldC * 0.62f);
        var sun = Part(root.transform, "Sun", quad, sunMat, new Vector3(0, 4.6f, 5.2f), new Vector3(9.5f, 9.5f, 1f));
        var turn = sun.gameObject.AddComponent<Rotor>(); turn.period = 46f; turn.axis = Vector3.forward;
        Part(root.transform, "SunHalo", quad, Glow("Podium_Halo", "fx_dot", goldC * 0.42f), new Vector3(0, 4.6f, 5.6f), new Vector3(22f, 22f, 1f));

        var metals = new[] { gold, silver, bronze };
        var glows = new[] { Emit("Podium_GlowGold", lit, goldC, 2.2f), Emit("Podium_GlowSilver", lit, silverC, 1.6f), Emit("Podium_GlowBronze", lit, bronzeC, 1.8f) };
        var tints = new[] { goldC, silverC, bronzeC };
        var spots = new Transform[3]; var trophies = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var (x, h) = Blocks[i];
            Part(root.transform, "Block" + Place[i], cube, blockMat, new Vector3(x, h / 2f, 0), new Vector3(BlockW, h, BlockD));
            // a lit edge along the top and the foot of each block, in its metal
            Part(root.transform, "Trim" + Place[i], cube, glows[i], new Vector3(x, h - 0.03f, -BlockD / 2f - 0.02f), new Vector3(BlockW, 0.06f, 0.04f));
            Part(root.transform, "Foot" + Place[i], cube, glows[i], new Vector3(x, 0.03f, -BlockD / 2f - 0.02f), new Vector3(BlockW, 0.06f, 0.04f));
            var number = Text3D(root.transform, "Number" + Place[i], Place[i], Design.Display, Mathf.Min(h * 9f, 9f), tints[i],
                                new Vector3(x, h * 0.47f, -BlockD / 2f - 0.02f), new Vector2(BlockW, h * 0.9f));
            number.fontStyle = FontStyles.Normal;

            var spot = new GameObject("Spot" + Place[i]).transform;
            spot.SetParent(root.transform, false);
            spot.localPosition = new Vector3(x, h, 0.1f);
            spot.localRotation = Quaternion.Euler(0, 180, 0);          // drivers face the camera, at -Z
            spots[i] = spot;

            trophies[i] = Trophy(root.transform, "Trophy" + Place[i], cup, metals[i], plinthMat, cube, new Vector3(x, h, -0.1f));
        }

        // Party poppers either side, aimed up over the podium; confetti rains through the ceremony.
        var poppers = new List<ParticleSystem>();
        foreach (float side in new[] { -1f, 1f })
        {
            var holder = new GameObject(side < 0 ? "PopperLeft" : "PopperRight").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(3.3f * side, 0.05f, -1.9f);
            var aim = (new Vector3(0, 4.2f, 0.4f) - holder.localPosition).normalized;
            holder.localRotation = Quaternion.FromToRotation(Vector3.up, aim);
            Part(holder, "Cone", cone, popperMat, Vector3.zero, Vector3.one * 2.6f);
            var burst = Particles(holder, "Burst", confettiMat, quad, rain: false);
            burst.transform.localPosition = new Vector3(0, 0.8f, 0);
            burst.transform.localRotation = Quaternion.Euler(-90, 0, 0);   // the system emits along its +Z: up the cone
            poppers.Add(burst);
        }
        var rain = Particles(root.transform, "Rain", confettiMat, quad, rain: true);
        rain.transform.localPosition = new Vector3(0, 9f, 0.5f);

        // gold dust rising off the floor the whole time, each mote flaring and dimming
        {
            var go = new GameObject("Glitter"); go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(0, 0.3f, 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main; m.loop = true; m.playOnAwake = true; m.prewarm = true; m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f); m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.38f); m.maxParticles = 90;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
            var e = ps.emission; e.rateOverTime = 16f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(13f, 0.4f, 7f); sh.rotation = new Vector3(-90, 0, 0);
            var tw = new AnimationCurve(); for (int i = 0; i <= 8; i++) tw.AddKey(i / 8f, i == 0 || i == 8 ? 0f : i % 2 == 0 ? 0.3f : 1f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, tw);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Glow("Podium_Glitter", "fx_sparkle", goldC * 3.2f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Stage light: the ceremony plays in the race scene, and after dark the coast's moon is not enough to see a
        // face by. Two warm spots from the front corners, a gold rim from behind; no shadows.
        foreach (float side in new[] { -1f, 1f })
        {
            var l = new GameObject(side < 0 ? "StageLightL" : "StageLightR").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(6f * side, 7.5f, -8f);
            l.transform.LookAt(root.transform.position + new Vector3(0, 1.6f, 0));
            l.type = LightType.Spot; l.spotAngle = 85f; l.innerSpotAngle = 50f; l.range = 30f; l.intensity = 120f;   // a 10 m throw
            l.color = new Color(1f, 0.95f, 0.88f); l.shadows = LightShadows.None;
        }
        var rim = new GameObject("StageRim").AddComponent<Light>();          // the sun behind them, on their shoulders
        rim.transform.SetParent(root.transform, false);
        rim.transform.localPosition = new Vector3(0, 6.5f, 5.5f);
        rim.transform.LookAt(root.transform.position + new Vector3(0, 1.8f, 0));
        rim.type = LightType.Spot; rim.spotAngle = 80f; rim.innerSpotAngle = 40f; rim.range = 24f; rim.intensity = 90f;
        rim.color = new Color(1f, 0.74f, 0.3f); rim.shadows = LightShadows.None;

        var camGo = new GameObject("PodiumCam");
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0, 3.2f, -11.5f);
        camGo.transform.LookAt(root.transform.position + Vector3.up * 2.4f);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 150f; cam.depth = 10f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.enabled = false;

        // ---- the words: a canvas on the stage's own camera (2400 x 1080, fitted by height like the menus) ----
        var canvasGo = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 0.6f; canvas.sortingOrder = 5;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(2400, 1080); scaler.matchWidthOrHeight = 1f;
        var group = canvasGo.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;

        // a soft dark wedge behind the words, so they read over anything the camera finds
        var scrim = UI("Scrim", canvasGo.transform, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1500, 0));
        scrim.pivot = new Vector2(0, 0.5f);
        var scrimImg = scrim.gameObject.AddComponent<UnityEngine.UI.Image>(); scrimImg.sprite = Gradient("podium_scrim"); scrimImg.color = new Color(0.02f, 0.04f, 0.06f, 0.82f); scrimImg.raycastTarget = false;

        var title = Words("Kicker", canvasGo.transform, Design.Strong, "PRIZE POOL", 40, Design.Paper, new Vector2(0, 1), new Vector2(120, -96), new Vector2(1200, 56));
        title.characterSpacing = 14f;
        var subtitle = Words("Big", canvasGo.transform, Design.Hero, "", 168, goldC, new Vector2(0, 1), new Vector2(112, -158), new Vector2(1500, 220));
        subtitle.enableAutoSizing = true; subtitle.fontSizeMin = 84; subtitle.fontSizeMax = 168;   // "0.54 SOL" at full size, "YOU TAKE THE CUP!" as wide as the frame allows
        var hint = Words("Hint", canvasGo.transform, Design.Strong, "TAP TO SKIP", 28, Design.Muted, new Vector2(1, 0), new Vector2(-110, 64), new Vector2(500, 40));
        hint.alignment = TextAlignmentOptions.Right; hint.characterSpacing = 10f;

        var cards = new RectTransform[3]; var mains = new TMP_Text[3]; var notes = new TMP_Text[3];
        string[] ordinal = { "1ST", "2ND", "3RD" };
        for (int i = 0; i < 3; i++)
        {
            var card = UI("Card" + Place[i], canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120 + 270, -540 - i * 164), new Vector2(540, 148));
            Design.AsCard(card, new Color(Design.Ink900.r, Design.Ink900.g, Design.Ink900.b, 0.9f)).raycastTarget = false;
            var bar = UI("Bar", card, new Vector2(0, 0), new Vector2(0, 1), new Vector2(18, 0), new Vector2(10, -40));
            var barImg = bar.gameObject.AddComponent<UnityEngine.UI.Image>(); barImg.color = tints[i]; barImg.raycastTarget = false;
            var place = Words("Place", card, Design.Display, ordinal[i], 88, tints[i], new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(180, 104));
            place.alignment = TextAlignmentOptions.Left;
            mains[i] = Words("Main", card, Design.Display, "", 66, Design.Paper, new Vector2(0, 0.5f), new Vector2(214, 0), new Vector2(310, 92));
            mains[i].alignment = TextAlignmentOptions.Left; mains[i].enableAutoSizing = true; mains[i].fontSizeMin = 34; mains[i].fontSizeMax = 66;
            notes[i] = Words("Note", card, Design.Strong, "", 36, goldC, new Vector2(0, 0.5f), new Vector2(216, -40), new Vector2(306, 44));
            notes[i].alignment = TextAlignmentOptions.Left;
            cards[i] = card;
        }
        canvasGo.SetActive(false);                       // PodiumShow raises it with its camera

        var so = new SerializedObject(show);
        so.FindProperty("cam").objectReferenceValue = cam;
        Fill(so.FindProperty("spots"), spots);
        Fill(so.FindProperty("trophies"), trophies);
        Fill(so.FindProperty("plates"), mains);
        Fill(so.FindProperty("cardNotes"), notes);
        Fill(so.FindProperty("cards"), cards);
        so.FindProperty("overlay").objectReferenceValue = group;
        so.FindProperty("hint").objectReferenceValue = hint;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("subtitle").objectReferenceValue = subtitle;
        Fill(so.FindProperty("poppers"), poppers.ToArray());
        so.FindProperty("rain").objectReferenceValue = rain;
        var rigsProp = so.FindProperty("rigs"); rigsProp.arraySize = rigs.Count;
        for (int i = 0; i < rigs.Count; i++)
        {
            var e = rigsProp.GetArrayElementAtIndex(i); var g = rigs[i];
            e.FindPropertyRelative("driver").stringValue = g.driver;
            e.FindPropertyRelative("hold").objectReferenceValue = g.hold;
            e.FindPropertyRelative("overHead").vector3Value = g.overHead;
            e.FindPropertyRelative("sideRight").vector3Value = g.sideRight;
            e.FindPropertyRelative("sideLeft").vector3Value = g.sideLeft;
            e.FindPropertyRelative("raiseLeft").quaternionValue = g.raiseLeft;
            e.FindPropertyRelative("raiseRight").quaternionValue = g.raiseRight;
            e.FindPropertyRelative("outLeft").quaternionValue = g.outLeft;
            e.FindPropertyRelative("outRight").quaternionValue = g.outRight;
        }
        so.FindProperty("pop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/podium/popper.wav");
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[podium] wrote " + PrefabPath);
    }

    // ---------- the rigs ----------

    /// The Kenney body model a driver prefab was built from (ContentSetup names the prefab, the mesh keeps the model).
    internal static string ModelOf(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var body = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault();
        var mesh = body != null ? AssetDatabase.GetAssetPath(body.sharedMesh) : null;
        return string.IsNullOrEmpty(mesh) ? null : mesh;
    }

    internal static AnimationClip Clip(string fbxPath, string take, string name, bool loop = true)
    {
        var src = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == take);
        if (src == null) { Debug.LogWarning($"[podium] {fbxPath} has no {take}"); return null; }
        var clip = Object.Instantiate(src);
        clip.name = name; clip.legacy = true; clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        var path = ClipDir + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing); EditorUtility.SetDirty(existing);   // in place: the menu builds these every time, and a new asset is a new guid
        return existing;
    }

    /// On one driver's own rig, in the hold pose and the torso's space: the point just over the head (Kenney's short
    /// arms never clear these big heads, so the gold is held up there, arms reaching for it in a V); where a trophy
    /// held out to either side sits; the arm turns to those poses; and how tall the driver stands.
    static (PodiumShow.Rig rig, float height) Measure(string prefabName, AnimationClip hold)
    {
        var d = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Drivers + prefabName + ".prefab"));
        hold.SampleAnimation(d, 0f);
        var body = d.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.ToLowerInvariant().Contains("body"));
        Transform torso = Find(d.transform, "torso"), armL = Find(d.transform, "arm-left"), armR = Find(d.transform, "arm-right");
        var headR = d.GetComponentsInChildren<Renderer>().Where(x => x.enabled && x.GetComponentInParent<EyeLook>() != null).ToList();
        var headBounds = headR[0].bounds; foreach (var x in headR) headBounds.Encapsulate(x.bounds);
        // Arms up in a V, outward as well as up: these heads are wider than the arms are long, so arms straight up
        // vanish inside the head. Each raise is the turn, in the arm's own space, from the hold pose to the V.
        Quaternion Raise(Transform arm, Vector3 target)
        {
            var q0 = arm.localRotation;
            var from = (End(body, arm) - arm.position).normalized;
            var world = Quaternion.FromToRotation(from, target.normalized) * arm.rotation;
            var local = Quaternion.Inverse(arm.parent.rotation) * world;
            return Quaternion.Inverse(q0) * local;
        }
        // the winner: both arms up and out in a wide V, clear of the head, reaching for the gold overhead
        var l = Raise(armL, new Vector3(-1f, 0.8f, 0.2f));
        var r = Raise(armR, new Vector3(1f, 0.8f, 0.2f));
        // second and third: the trophy arm held out to the side and a little up, so the hand (and the trophy in it)
        // stands clear of the head; the base a little below the hand, which grips the stem. (The driver faces +Z
        // here, so its right hand is on +X.)
        var outL = Raise(armL, new Vector3(-1f, 0.35f, 0.15f));
        var outR = Raise(armR, new Vector3(1f, 0.35f, 0.15f));
        var q0L = armL.localRotation; var q0R = armR.localRotation;
        armL.localRotation = q0L * outL; armR.localRotation = q0R * outR;
        var sideRight = torso.InverseTransformPoint(End(body, armR) + new Vector3(0.03f, -0.07f, 0f));
        var sideLeft = torso.InverseTransformPoint(End(body, armL) + new Vector3(-0.03f, -0.07f, 0f));
        armL.localRotation = q0L; armR.localRotation = q0R;
        var overHead = torso.InverseTransformPoint(new Vector3(torso.position.x, headBounds.max.y + 0.03f, torso.position.z + 0.04f));
        float height = d.GetComponentsInChildren<Renderer>().Where(x => x.enabled).Select(x => x.bounds.max.y).Max() - d.transform.position.y;
        Object.DestroyImmediate(d);
        return (new PodiumShow.Rig { driver = prefabName, hold = hold, overHead = overHead, sideRight = sideRight, sideLeft = sideLeft,
                                     raiseLeft = l, raiseRight = r, outLeft = outL, outRight = outR }, height);
    }

    /// An arm's far end in the current pose: Kenney's rig weights each arm vertex wholly to its bone, so skinning by
    /// hand is exact (the same trick as ContentSetup.Fist, but measured from the shoulder, not along the kart).
    internal static Vector3 End(SkinnedMeshRenderer body, Transform arm)
    {
        var mesh = body.sharedMesh;
        int bi = System.Array.IndexOf(body.bones, arm);
        var toWorld = arm.localToWorldMatrix * mesh.bindposes[bi];
        var weights = mesh.boneWeights;
        var pts = mesh.vertices.Where((v, i) => weights[i].boneIndex0 == bi && weights[i].weight0 > 0.5f)
                               .Select(v => toWorld.MultiplyPoint3x4(v))
                               .OrderByDescending(p => (p - arm.position).sqrMagnitude).ToList();
        var tip = pts.Take(Mathf.Max(1, pts.Count / 4)).ToList();
        return tip.Aggregate(Vector3.zero, (a, p) => a + p) / tip.Count;
    }

    // ---------- meshes ----------

    /// A turned trophy: stem, knop and bowl from one profile (metres, the gold one's size), two handles swept on.
    static Mesh CupMesh()
    {
        var profile = new[] {
            new Vector2(0.085f, 0f), new Vector2(0.085f, 0.022f), new Vector2(0.05f, 0.036f), new Vector2(0.028f, 0.06f),
            new Vector2(0.022f, 0.13f), new Vector2(0.042f, 0.15f), new Vector2(0.022f, 0.17f), new Vector2(0.03f, 0.2f),
            new Vector2(0.07f, 0.23f), new Vector2(0.115f, 0.28f), new Vector2(0.136f, 0.34f), new Vector2(0.144f, 0.40f),
            new Vector2(0.15f, 0.44f), new Vector2(0.132f, 0.445f), new Vector2(0.122f, 0.40f), new Vector2(0f, 0.35f),
        };
        var b = new Builder();
        b.Lathe(profile, 40);
        foreach (float s in new[] { -1f, 1f })
        {
            var path = new List<Vector3>();
            for (int k = 0; k <= 14; k++)
            {
                float a = Mathf.Lerp(-75f, 80f, k / 14f) * Mathf.Deg2Rad;
                path.Add(new Vector3(s * (0.13f + Mathf.Cos(a) * 0.11f), 0.335f + Mathf.Sin(a) * 0.075f, 0f));
            }
            b.Tube(path, 0.014f, 10);
        }
        return b.Mesh("trophy_cup");
    }

    /// A party popper: a paper cone, open end up.
    static Mesh PopperMesh()
    {
        var b = new Builder();
        b.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0.01f), new Vector2(0.075f, 0.3f), new Vector2(0.085f, 0.31f), new Vector2(0.07f, 0.3f), new Vector2(0.012f, 0.03f) }, 24);
        return b.Mesh("popper_cone");
    }

    sealed class Builder
    {
        readonly List<Vector3> v = new(); readonly List<Vector3> n = new(); readonly List<int> t = new();

        public void Lathe(Vector2[] p, int seg)
        {
            int start = v.Count;
            for (int i = 0; i < p.Length; i++)
            {
                // smooth normal from the two neighbouring profile segments (r out, y up)
                Vector2 a = i > 0 ? p[i] - p[i - 1] : p[1] - p[0], c = i < p.Length - 1 ? p[i + 1] - p[i] : p[i] - p[i - 1];
                var tan = (a.normalized + c.normalized).normalized;
                var nrm = new Vector2(tan.y, -tan.x);
                for (int s = 0; s <= seg; s++)
                {
                    float ang = s * Mathf.PI * 2f / seg, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    v.Add(new Vector3(p[i].x * cs, p[i].y, p[i].x * sn));
                    n.Add(new Vector3(nrm.x * cs, nrm.y, nrm.x * sn).normalized);
                }
            }
            for (int i = 0; i < p.Length - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a0 = start + i * (seg + 1) + s, a1 = a0 + 1, b0 = a0 + seg + 1, b1 = b0 + 1;
                    t.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
        }

        public void Tube(List<Vector3> path, float r, int sides)
        {
            int start = v.Count;
            for (int i = 0; i < path.Count; i++)
            {
                var fwd = (i < path.Count - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                var side = Vector3.Cross(fwd, Vector3.forward).normalized;
                var up = Vector3.Cross(side, fwd);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    var dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(path[i] + dir * r); n.Add(dir);
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = start + i * (sides + 1) + s, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }
        }

        public Mesh Mesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    static Mesh Save(Mesh m, string name)
    {
        var path = Dir + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------- parts ----------

    static Transform Trophy(Transform parent, string name, Mesh cup, Material metal, Material plinth, Mesh cube, Vector3 at)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = at;
        Part(root, "Plinth", cube, plinth, new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.1f, 0.24f));
        Part(root, "Cup", cup, metal, new Vector3(0, 0.1f, 0), Vector3.one);
        return root;
    }

    static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // one stage, no shadow maps to pay for
        return go.transform;
    }

    static void Kit(Transform parent, string name, Vector3 pos, float yaw, float height)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Art/Models/Props/Racing/{name}.fbx");
        if (fbx == null) { Debug.LogWarning("[podium] no racing kit " + name); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y > 0.001f) go.transform.localScale = Vector3.one * (height / b.size.y);
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.localPosition = pos + new Vector3(-(b.center.x - go.transform.position.x), -(b.min.y - go.transform.position.y), -(b.center.z - go.transform.position.z));
    }

    static TextMeshPro Text3D(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Vector3 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = box;
        go.transform.localPosition = pos;
        return t;
    }

    static ParticleSystem Particles(Transform parent, string name, Material mat, Mesh quad, bool rain)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = rain; main.duration = rain ? 4f : 1f;
        main.startLifetime = rain ? new ParticleSystem.MinMaxCurve(5f, 7f) : new ParticleSystem.MinMaxCurve(3f, 4.5f);
        main.startSpeed = rain ? new ParticleSystem.MinMaxCurve(0.5f, 1.2f) : new ParticleSystem.MinMaxCurve(8f, 13f);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.12f, 0.18f);
        main.startSizeZ = 1f;
        main.startRotation3D = true;
        main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = rain ? 0.06f : 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = rain ? 360 : 260;
        main.startColor = new ParticleSystem.MinMaxGradient(ConfettiColours()) { mode = ParticleSystemGradientMode.RandomColor };

        var em = ps.emission;
        em.rateOverTime = rain ? 45f : 0f;
        if (!rain) em.SetBursts(new[] { new ParticleSystem.Burst(0f, 170), new ParticleSystem.Burst(0.07f, 60) });

        var shape = ps.shape;
        if (rain) { shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(12f, 5f, 0.1f); shape.rotation = new Vector3(90f, 0f, 0f); }
        else { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 16f; shape.radius = 0.05f; }

        var rot = ps.rotationOverLifetime;
        rot.enabled = true; rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-7f, 7f); rot.y = new ParticleSystem.MinMaxCurve(-7f, 7f); rot.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

        if (!rain)
        {
            var drag = ps.limitVelocityOverLifetime;   // air: the burst slows, then the confetti drifts down
            drag.enabled = true; drag.limit = 2.2f; drag.dampen = 0.07f;
        }
        var noise = ps.noise;
        noise.enabled = true; noise.strength = rain ? 0.7f : 0.5f; noise.frequency = 0.6f; noise.scrollSpeed = 0.4f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = quad; r.sharedMaterial = mat;
        r.alignment = ParticleSystemRenderSpace.World;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    /// Five crisp colours, not a blend: a fixed gradient that RandomColor picks from.
    static Gradient ConfettiColours()
    {
        var cols = new[] { Hex("FF7F16"), Hex("FFC83A"), Design.Paper, Hex("2EC4B6"), Hex("3A86FF"), Hex("FF4FA3") };
        var g = new Gradient { mode = GradientMode.Fixed };
        g.SetKeys(cols.Select((c, i) => new GradientColorKey(c, (i + 1f) / cols.Length)).ToArray(), new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // ---------- the overlay's pieces ----------

    static RectTransform UI(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Vector2 dims)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.anchoredPosition = offset; rt.sizeDelta = dims;
        return rt;
    }

    static TextMeshProUGUI Words(string name, Transform parent, TMP_FontAsset font, string text, float size, Color colour, Vector2 anchor, Vector2 offset, Vector2 dims)
    {
        var rt = UI(name, parent, anchor, anchor, offset, dims);
        rt.pivot = anchor;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = colour; t.alignment = TextAlignmentOptions.TopLeft;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    /// A sprite that is solid at its left edge and gone at its right: the wedge of dark the words sit on.
    static Sprite Gradient(string name)
    {
        string path = Dir + name + ".png";
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
        {
            var t = new Texture2D(256, 4, TextureFormat.RGBA32, false);
            for (int x = 0; x < 256; x++) for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(1f - x / 255f, 1.6f)));
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false; imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- materials ----------

    /// The sun behind the podium: sixteen rays round a soft disc, painted at 1024 so its edge stays clean nine metres wide.
    static string SunTexture()
    {
        string path = Dir + "podium_sun.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
        {
            const int n = 1024;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(u * u + v * v);
                    float saw = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(v, u) / (Mathf.PI * 2f) * 16f, 1f) - 0.5f) * 2f;   // 0 at a ray's tip
                    float edge = Mathf.Lerp(0.95f, 0.66f, saw), d = edge - r;
                    float rays = Mathf.Clamp01(d / 0.006f) * (0.30f + 0.5f * Mathf.Clamp01(1f - d / 0.07f));                // a bright rim, a soft body
                    float disc = Mathf.Clamp01((0.44f - r) / 0.006f) * 0.5f;
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Max(rays, disc)));
                }
            t.SetPixels(px);
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.maxTextureSize = 1024; imp.SaveAndReimport();
        }
        return path;
    }

    /// A dark surface seen from inside (front faces culled): the drum the stage stands in.
    static Material Inside(string name, Color c)
    {
        var m = Mat(name, Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Cull", 1f);
        return m;
    }

    /// Light drawn on top of the frame: one of the effect shapes (ItemSetup paints them, or a path to our own) in a colour past 1.
    static Material Glow(string name, string texture, Color hdr)
    {
        var m = Mat(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texture.EndsWith(".png") ? texture : $"Assets/_Game/Art/Textures/FX/{texture}.png");
        if (tex == null) Debug.LogWarning($"[podium] {texture}.png missing: run ItemSetup.Build first");
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", hdr);
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }

    static Material Emit(string name, Shader lit, Color c, float strength)
    {
        var m = Mat(name, lit);
        m.SetColor("_BaseColor", c * 0.4f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.5f);
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * strength); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return m;
    }

    static Material Metal(string name, Shader lit, Color c)
    {
        var m = Mat(name, lit);
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 0.8f);
        return m;
    }

    static Material Plain(string name, Shader lit, Color c, float smooth)
    {
        var m = Mat(name, lit);
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Material Confetti(string name)
    {
        var m = Mat(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetFloat("_Cull", 0f);                     // paper has two sides
        m.SetColor("_BaseColor", Color.white);
        return m;
    }

    static Material Mat(string name, Shader s)
    {
        var path = Dir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
        m.shader = s;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static void Fill(SerializedProperty p, Object[] items)
    {
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    // ---------- preview ----------

    /// Both shots rendered from the stage's own camera, drivers posed by sampling their clips (edit mode does not run
    /// animation) and the confetti simulated forward: Builds/podium-prizes*.png and Builds/podium-winners*.png.
    [MenuItem("Build/Capture Podium")]
    public static void Capture()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Sky_beach.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Sky.mat");
        if (sky != null) { RenderSettings.skybox = sky; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox; DynamicGI.UpdateEnvironment(); }
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50, -35, 0); sun.intensity = 1.1f; sun.color = new Color(1f, 0.96f, 0.9f);
        Preview.Batcher(false);

        var stage = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        // a plain clone, as at runtime: the editor will not re-parent a prefab instance's children into a driver's hands
        PrefabUtility.UnpackPrefabInstance(stage, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var show = stage.GetComponent<PodiumShow>();
        var cam = stage.GetComponentInChildren<Camera>(true);
        var systems = stage.GetComponentsInChildren<ParticleSystem>(true);

        show.Prizes("PRIZE POOL", "0.54 SOL", new[] { "1ST\n0.351 SOL", "2ND\n0.189 SOL", "3RD\n—" });
        Simulate(systems, 0.7f);
        show.PoseCamera(0.9f); Canvas.ForceUpdateCanvases(); Shot(cam, "podium-prizes-close");
        show.PoseCamera(4f); Canvas.ForceUpdateCanvases(); Shot(cam, "podium-prizes");
        show.Prizes("RACE FOR GOLD", "PALM BAY", new[] { "1ST", "2ND", "3RD" });
        show.PoseCamera(4f); Canvas.ForceUpdateCanvases(); Shot(cam, "podium-solo");

        var top = new[] { "Driver_Toly", "Driver_Mert", "Driver_Bitsy" }.Select((n, k) => new PodiumShow.Winner {
            driver = AssetDatabase.LoadAssetAtPath<GameObject>(Drivers + n + ".prefab"), name = n.Replace("Driver_", ""), prize = k == 0 ? "+0.351 SOL" : k == 1 ? "+0.189 SOL" : "" }).ToList();
        show.Winners("TOLY WINS!", top);
        foreach (var pose in Object.FindObjectsByType<PodiumPose>(FindObjectsSortMode.None)) pose.Apply(0.26f);   // top of a hop
        foreach (var smr in stage.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>())) smr.updateWhenOffscreen = true;
        Simulate(systems, 1.1f);
        show.PoseCamera(1f); Canvas.ForceUpdateCanvases(); Shot(cam, "podium-winners");
        show.PoseCamera(5f); Canvas.ForceUpdateCanvases(); Shot(cam, "podium-winners-close");
        Simulate(systems, 3.2f);
        foreach (var pose in Object.FindObjectsByType<PodiumPose>(FindObjectsSortMode.None)) pose.Apply(0.03f);   // landing
        show.PoseCamera(3f); Canvas.ForceUpdateCanvases(); Shot(cam, "podium-winners-late");

        Preview.Batcher(true);
        Debug.Log("[podium] wrote Builds/podium-*.png");
    }

    static void Simulate(ParticleSystem[] systems, float seconds)
    {
        foreach (var ps in systems) { ps.Simulate(seconds, true, true); }
    }

    static void Shot(Camera cam, string name)
    {
        int w = 1600, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory("Builds");
        File.WriteAllBytes($"Builds/{name}.png", tex.EncodeToPNG());
    }
}
