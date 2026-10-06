using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assembles the content the plan describes, headlessly and idempotently: one unlit material per Kenney colormap,
// water/blob/sky materials, four driver prefabs (Blocky body + swapped box head + BlockyDriverPose), kart visual prefabs.
// Run: Unity -batchmode -projectPath . -executeMethod ContentSetup.Build -quit   (menu Build > Assemble Content)
public static class ContentSetup
{
    const string Art = "Assets/_Game/Art/";
    const string Mats = Art + "Materials/";
    const string Prefabs = "Assets/_Game/Prefabs/";

    [MenuItem("Build/Assemble Content")]
    public static void Build()
    {
        Directory.CreateDirectory(Mats); Directory.CreateDirectory(Prefabs + "Drivers"); Directory.CreateDirectory(Prefabs + "Karts"); Directory.CreateDirectory(Prefabs + "World");
        var unlit = Shader.Find("KartCup/Unlit Colormap");
        var water = Shader.Find("KartCup/Water Unlit");
        if (unlit == null || water == null) { Debug.LogError("[content] shaders not compiled yet"); Exit(1); return; }

        // ---- materials: one per colormap ----
        var carkit   = ColormapMat(unlit, "CarKit",     Art + "Textures/Colormaps/carkit_colormap.png");
        var toycar   = ColormapMat(unlit, "ToyCar",     Art + "Textures/Colormaps/toycar_colormap.png");
        var pirate   = ColormapMat(unlit, "Pirate",     Art + "Textures/Colormaps/pirate_colormap.png");
        var craft    = ColormapMat(unlit, "Watercraft", Art + "Textures/Colormaps/watercraft_colormap.png");
        ColormapMat(unlit, "Holiday",  Art + "Textures/Colormaps/holiday_colormap.png");    // for the world builder
        ColormapMat(unlit, "Survival", Art + "Textures/Colormaps/survival_colormap.png");
        var headMat  = ColormapMat(unlit, "HeadPalette", Art + "Textures/Colormaps/head_palette.png");
        CarPaint(carkit, Art + "Textures/Colormaps/carkit_colormap.png");
        var bodyMats = "abcdef".Select(c => ColormapMat(unlit, "Body_" + c, Art + $"Textures/Colormaps/texture-{c}.png")).ToArray();

        var waterMat = LoadOrCreate(Mats + "Water.mat", water);
        waterMat.SetTexture("_WaterA", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/World/water_tile_a.png"));
        waterMat.SetTexture("_WaterB", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/World/water_tile_b.png"));
        waterMat.SetTextureScale("_WaterA", new Vector2(8, 8)); waterMat.SetTextureScale("_WaterB", new Vector2(6, 6));

        var blobMat = LoadOrCreate(Mats + "BlobShadow.mat", Shader.Find("Universal Render Pipeline/Unlit"));
        blobMat.SetFloat("_Surface", 1); blobMat.SetFloat("_Blend", 0); blobMat.SetFloat("_ZWrite", 0);
        blobMat.renderQueue = 3000; blobMat.SetOverrideTag("RenderType", "Transparent");
        blobMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); blobMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/World/blob_shadow.png"));
        blobMat.SetColor("_BaseColor", new Color(0, 0, 0, 0.55f));
        blobMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); blobMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        var skyTex = AssetDatabase.LoadAssetAtPath<Cubemap>(Art + "Textures/Sky/skybox-day.png");
        if (skyTex != null) { var sky = LoadOrCreate(Mats + "Sky.mat", Shader.Find("Skybox/Cubemap")); sky.SetTexture("_Tex", skyTex); }

        // ---- blob shadow prefab ----
        var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(blob.GetComponent<Collider>());
        blob.name = "BlobShadow"; blob.transform.rotation = Quaternion.Euler(90, 0, 0); blob.transform.localScale = new Vector3(2.2f, 2.2f, 1);
        blob.GetComponent<MeshRenderer>().sharedMaterial = blobMat;
        SavePrefab(blob, Prefabs + "World/BlobShadow.prefab");

        // ---- driver prefabs: Kenney Mini Characters, skinned and holding their own "drive" pose ----
        var driverMat = ColormapMat(unlit, "MiniChar", Art + "Textures/Colormaps/minichar_colormap.png");
        // name, body model, caricature head (sculpted and painted by tools/blender/heads.py, chars/<name>.py)
        var drivers = new[] {
            ("Toly",   "character-male-a",   "head_toly"),
            ("Mert",   "character-male-b",   "head_mert"),
            ("Akshay", "character-male-c",   "head_akshay"),
            ("Emmett", "character-male-d",   "head_emmett"),
            ("Bitsy",  "character-female-a", "head_bitsy"),
            ("Pip",    "character-female-b", "head_pip"),
            ("Runa",   "character-female-c", "head_runa"),
            ("Vee",    "character-female-d", "head_vee"),
            ("Chase",  "character-male-e",   "head_chase"),    // from the user's front art (output/imagegen/heads/chase-front.png)
            ("Beeman", "character-male-f",   "head_beeman"),   // bald, sunglasses, grey beard
            ("Maroo",  "character-male-b",   "head_maroo"),    // curly hair, stubble (tools/blender/art/maroo.py from the user's art)
            ("Andrew", "character-male-d",   "head_andrew"),   // bald, black frames, stubble (tools/blender/art/andrew.py)
        };
        foreach (var (name, model, headName) in drivers)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Drivers/{model}.fbx");
            if (fbx == null) { Debug.LogWarning($"[content] missing driver model {model}"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Driver_" + name;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = driverMat;

            // Kenney's kit is not one scale: character-female-a is authored ~43% wider than the other seven
            // (body 1.10 m vs 0.77 m, Build/Probe Heads). At native size its arms hang outside the kart and read
            // as detached blocks. Normalise every driver to the kit's common body width before anything is measured.
            const float kitBodyWidth = 0.77f;
            var bodyR = go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                          .FirstOrDefault(r => r.name.ToLowerInvariant().Contains("body"));
            if (bodyR != null && bodyR.bounds.size.x > 0.001f)
            {
                float norm = kitBodyWidth / bodyR.bounds.size.x;
                if (Mathf.Abs(norm - 1f) > 0.02f)
                {
                    go.transform.localScale = Vector3.one * norm;
                    Debug.Log($"[content] {name}: body normalised x{norm:F2}");
                }
            }

            // The FBX ships every take; the driver only ever needs the seated one, played on loop.
            var drive = AssetDatabase.LoadAllAssetsAtPath(Art + $"Models/Drivers/{model}.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == "drive")
                ?? AssetDatabase.LoadAllAssetsAtPath(Art + $"Models/Drivers/{model}.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == "sit");
            var anim = go.GetComponent<Animation>() ?? go.AddComponent<Animation>();
            if (drive != null)
            {
                var loop = Object.Instantiate(drive);
                loop.name = "drive";
                loop.wrapMode = WrapMode.Loop;
                loop.legacy = true;
                AssetDatabase.CreateAsset(loop, Prefabs + $"Drivers/{name}_drive.anim");
                anim.AddClip(loop, "drive");
                anim.clip = loop;
            }
            anim.playAutomatically = true;
            anim.cullingType = AnimationCullingType.BasedOnRenderers;

            // Swap the stock head for the caricature: the kit head is a skinned mesh, so hide it and hang ours
            // off the `head` bone, whose origin is exactly the neck joint the head mesh starts at.
            var headFbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Heads/{headName}.fbx");
            var headBone = Find(go.transform, "head");
            if (headFbx != null && headBone != null)
            {
                var kitHead = go.GetComponentsInChildren<Renderer>(true)
                                .FirstOrDefault(r => r.name.ToLowerInvariant().Contains("head"));
                if (kitHead != null) kitHead.enabled = false;

                var custom = (GameObject)PrefabUtility.InstantiatePrefab(headFbx);
                PrefabUtility.UnpackPrefabInstance(custom, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                custom.name = "Head";
                custom.transform.SetParent(headBone, false);
                // Authored Z-up with the face on -Y; the FBX round-trip lands them on their back and facing away.
                custom.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // face down +Z, the way the body faces
                custom.transform.localPosition = Vector3.zero;
                // One size for every driver, never squashed to the kit head's box: the rounded skull is the whole point.
                // heads.py authors the skull 0.454 m wide (HEAD_SCALE); the bodies were normalised above, and
                // following each kit head instead left character-female-a's driver with a head a third smaller.
                // The origin is the skull's base, so beards hang over the chest instead of lifting the head.
                custom.transform.localScale = Vector3.one / headBone.lossyScale.x;
                // sculpted heads (tools/blender/heads.py) carry their own painted texture next to the FBX
                var painted = Art + $"Models/Heads/{headName}.png";
                var mat = File.Exists(painted) ? ColormapMat(unlit, "Head_" + name, painted) : headMat;
                foreach (var r in custom.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterial = mat;
                    r.receiveShadows = false;   // the texture carries baked AO; a bill or nose shadow on a painted face reads as a stain
                }
                // the eye windows and lids the FBX carries (eye_R / eye_L, lid_R / lid_L; none under shades) have the
                // head's material, set above; Bind() leaves the lids off until a blink
                int eyes = custom.AddComponent<KartCup.EyeLook>().Bind();
                Debug.Log($"[content] {name}: head {custom.GetComponentInChildren<Renderer>().bounds.size}, eye windows {eyes}");
            }
            else Debug.LogWarning($"[content] no head for {name} ({headName})");

            var (wheel, armTurn, wheelTurn) = drive != null ? SteeringWheel(go, anim.clip, headMat) : (null, 1f, 1f);

            var pose = go.AddComponent<KartCup.BlockyDriverPose>();
            var so = new SerializedObject(pose);
            so.FindProperty("torso").objectReferenceValue = Find(go.transform, "torso");
            so.FindProperty("armLeft").objectReferenceValue = Find(go.transform, "arm-left");
            so.FindProperty("armRight").objectReferenceValue = Find(go.transform, "arm-right");
            so.FindProperty("head").objectReferenceValue = Find(go.transform, "head");
            so.FindProperty("wheel").objectReferenceValue = wheel;
            so.FindProperty("armTurn").floatValue = armTurn;
            so.FindProperty("wheelTurn").floatValue = wheelTurn;
            so.ApplyModifiedPropertiesWithoutUndo();
            SavePrefab(go, Prefabs + $"Drivers/Driver_{name}.prefab");
        }

        // our driver's standing height, so each kart's placeholder can scale it to fit that kart
        float driverHeight = 0f, driverWidth = 0f;
        {
            var sample = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Drivers/Driver_{drivers[0].Item1}.prefab");
            if (sample != null)
            {
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(sample);
                var bounds = new Bounds(probe.transform.position, Vector3.zero);
                foreach (var r in probe.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                driverHeight = bounds.max.y;          // hips sit at y=0 after the re-pivot: this is what shows above the seat
                driverWidth = bounds.size.x;
                Object.DestroyImmediate(probe);
            }
            Debug.Log($"[content] driver height {driverHeight:F2} m");
        }

        // ---- kart visual prefabs: Car Kit body + wheels, seated driver slot, blob shadow ----
        foreach (var kart in new[] { "kart-oobi", "kart-oodi", "kart-ooli", "kart-oopi", "kart-oozi" })
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Karts/{kart}.fbx");
            if (fbx == null) { Debug.LogWarning("[content] missing " + kart); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "KartVisual_" + kart.Substring(5);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = carkit;
            // Kenney's karts ship a placeholder driver: its transform and height are exactly where and how big a
            // driver belongs, so take the seat from it instead of guessing offsets.
            var seated = Find(go.transform, "character");
            var seatPos = new Vector3(0, 0.30f, -0.08f); float seatScale = 0.32f;
            if (seated)
            {
                seatPos = seated.localPosition;
                var placeholder = new Bounds(seated.position, Vector3.zero);
                foreach (var rend in seated.GetComponentsInChildren<Renderer>()) placeholder.Encapsulate(rend.bounds);
                if (placeholder.size.y > 0.01f && driverHeight > 0.01f)
                    seatScale = Mathf.Clamp(Mathf.Min(placeholder.size.y / driverHeight,
                                                      placeholder.size.x / Mathf.Max(driverWidth, 0.01f)), 0.6f, 1.05f);
                Object.DestroyImmediate(seated.gameObject);
            }
            var seat = new GameObject("DriverSeat").transform; seat.SetParent(go.transform, false);
            seat.localPosition = seatPos; seat.localScale = Vector3.one * seatScale;   // a blocky driver reads best at about a third of the kart's length
            var shadow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "World/BlobShadow.prefab"));
            shadow.transform.SetParent(go.transform, false); shadow.transform.localPosition = new Vector3(0, 0.03f, 0);
            SavePrefab(go, Prefabs + $"Karts/{go.name}.prefab");
        }

        BuildVehicles(carkit);

        AssetDatabase.SaveAssets();
        Debug.Log("[content] assembled materials, drivers, karts");
        Exit(0);
    }

    // ---- premium karts: Car Kit vehicles. Their FBX root is an empty with the body (plus spoiler/grill/door parts) and
    // the wheels as children. KartSetup wants one mesh on the root with the pivot at ground level and four wheel
    // children, so the body parts are baked into a single mesh in root space and the wheels re-parented.
    //
    // Kenney drew every vehicle for the same driver that sits in his karts, so a 2.8 m car is meant to be twice the
    // length of a 1.4 m kart. Squeezing them all to the kart's footprint left a 1.7x oversized driver perched on a toy.
    // Each vehicle now keeps part of its real size (footprint, times the kart rig's length) and the driver is authored
    // at one fixed on-screen size. Closed bodies lose their greenhouse over the cabin span [z0, z1]: triangles are
    // clipped at the sill (not dropped, so no pillar stubs or windscreen spikes), and the tub is closed with a dark
    // deck at knee height plus dash and rear walls, so it reads as a convertible's interior rather than a cut.
    // Numbers are metres in the FBX, measured by VehicleProbe.Run (Logs/VehicleProbe.txt): sill = belt line
    // (window bottoms / cowl), z0..z1 = rear glass base .. windscreen base (or the cockpit opening when open),
    // sit = how far above the hips the sill meets the driver (chest in a car; the lap on a tractor, whose driver sits high).
    public static readonly (string name, float footprint, bool roof, float sill, float z0, float z1, float sit)[] Vehicles =
    {
        ("race-future",      1.15f, false, 0.66f, -0.40f, 0.50f, 0.22f),   // open cockpit: the span only places the seat
        ("tractor",          0.90f, true,  1.00f, -0.80f, 0.24f, 0.06f),   // cab roof and glass off (the screen stands at z 0.14-0.21, the bonnet starts past it); the driver sits high, so the tractor is kept a little smaller to fit the garage frame
        ("taxi",             1.30f, true,  0.80f, -0.90f, 0.70f, 0.22f),
        ("van",              1.30f, true,  0.80f,  0.05f, 0.90f, 0.22f),   // cab only; the cargo box behind stays
        ("hatchback-sports", 1.30f, true,  0.80f, -1.15f, 0.62f, 0.22f),   // span reaches the roof spoiler so no lip remains
        ("police",           1.30f, true,  0.80f, -0.95f, 0.75f, 0.22f),
        ("firetruck",        1.30f, true,  1.00f,  0.42f, 1.45f, 0.22f),   // cab at the front; the ladder loses its front end
        ("garbage-truck",    1.30f, true,  0.80f,  0.25f, 1.28f, 0.22f),   // cab too short for a driver: the notch bites into the hopper
    };
    public static float Footprint(string name) { foreach (var v in Vehicles) if (v.name == name) return v.footprint; return 1f; }

    const float RigLength = 3.75f;   // Powerslide's kart mesh; KartSetup measures it and warns if it drifts from this
    const float DriverRig = 2.15f;   // hips-to-crown in rig space on every vehicle: 0.78 of a kart driver (1.05 x 2.63)
    // The seated driver in its own units (VehicleProbe, Driver_Toly at t=0): chest 0.22 above the hips, knees 0.11
    // below, torso back 0.17 behind, steering wheel rim 0.44 ahead.
    const float KneesBelowHips = 0.11f, BackBehindHips = 0.17f, WheelAheadOfHips = 0.44f;

    struct V { public Vector3 p, n; public Vector2 uv; }

    static void BuildVehicles(Material carkit)
    {
        Directory.CreateDirectory(Art + "Models/Karts/Combined");
        // Two-sided copy of the kit material: an open cabin shows its inner walls, which are back faces.
        var open = LoadOrCreate(Mats + "CarKitOpen.mat", carkit.shader);
        open.shader = carkit.shader;
        open.CopyPropertiesFromMaterial(carkit);
        open.shaderKeywords = carkit.shaderKeywords;
        open.SetFloat("_Cull", 0f);
        open.doubleSidedGI = true;
        EditorUtility.SetDirty(open);
        var colormap = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        ImageConversion.LoadImage(colormap, File.ReadAllBytes(Art + "Textures/Colormaps/carkit_colormap.png"));

        foreach (var v in Vehicles)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Models/Karts/{v.name}.fbx");
            if (fbx == null) { Debug.LogWarning("[content] missing vehicle " + v.name); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;

            // "arm" is the garbage truck's front loader: it swings over the cab, straight through a driver sitting in it.
            var parts = inst.GetComponentsInChildren<MeshFilter>()
                            .Where(m => !m.name.StartsWith("wheel") && m.name != "arm" && m.sharedMesh != null).ToArray();
            var combined = new Mesh();
            combined.CombineMeshes(parts.Select(m => new CombineInstance { mesh = m.sharedMesh, transform = m.transform.localToWorldMatrix }).ToArray(), true, true);
            var tris = Tris(combined);
            var b = BoundsOf(tris);

            float fit = RigLength * v.footprint / b.size.z;   // what KartSetup will scale this visual by
            float s = DriverRig / fit;                          // so the driver is authored this big here
            float seatY = Mathf.Max(v.sill - v.sit * s, b.min.y + 0.02f + KneesBelowHips * s);
            float seatZ = Mathf.Max(v.z1 - (WheelAheadOfHips + 0.06f) * s, v.z0 + BackBehindHips * s);   // wheel just short of the dash
            int before = tris.Count;
            if (v.roof)
            {
                var dark = DarkestUv(tris, colormap);
                tris = CutBox(tris, v.sill, v.z0, v.z1);
                float deckY = Mathf.Max(seatY - (KneesBelowHips + 0.01f) * s, b.min.y + 0.03f);
                float w = HalfWidth(tris, v.z0, v.z1, v.sill - 0.25f, v.sill + 0.005f) - 0.03f;   // inside the door tops
                Quad(tris, dark, Vector3.up, new(-w, deckY, v.z0), new(-w, deckY, v.z1), new(w, deckY, v.z1), new(w, deckY, v.z0));
                Wall(tris, dark, v.z0 + 0.01f, +1f, deckY, v.sill, w, HalfWidth(tris, v.z0 - 0.2f, v.z0, v.sill - 0.05f, 9f) - 0.01f, TopOf(tris, v.z0 - 0.2f, v.z0));   // rear wall faces the driver
                Wall(tris, dark, v.z1 - 0.01f, -1f, deckY, v.sill, w, HalfWidth(tris, v.z1, v.z1 + 0.2f, v.sill - 0.05f, 9f) - 0.01f, TopOf(tris, v.z1, v.z1 + 0.2f));   // dash faces back
            }
            var mesh = ToMesh(tris, v.name + "_body");
            var meshPath = Art + $"Models/Karts/Combined/{v.name}_body.asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            var go = new GameObject("KartVisual_" + v.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = v.roof ? open : carkit;
            foreach (var w in new[] { "wheel-back-left", "wheel-back-right", "wheel-front-left", "wheel-front-right" })
            {
                var wt = inst.transform.Find(w);
                if (wt == null) { Debug.LogWarning($"[content] {v.name}: no {w}"); continue; }
                wt.SetParent(go.transform, true);
                foreach (var r in wt.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = carkit;
            }
            var seat = new GameObject("DriverSeat").transform;
            seat.SetParent(go.transform, false);
            seat.localPosition = new Vector3(0f, seatY, seatZ);
            seat.localScale = Vector3.one * s;
            Object.DestroyImmediate(inst);
            SavePrefab(go, Prefabs + $"Karts/{go.name}.prefab");
            Debug.Log($"[content] vehicle {v.name}: body {b.size.x:F2}x{b.size.y:F2}x{b.size.z:F2}, footprint x{v.footprint:F2}, driver x{s:F2}, seat y{seatY:F2} z{seatZ:F2}, tris {before} -> {tris.Count}");
        }
    }

    static List<V[]> Tris(Mesh m)
    {
        var p = m.vertices; var n = m.normals; var uv = m.uv; var t = m.triangles;
        var list = new List<V[]>(t.Length / 3);
        for (int i = 0; i < t.Length; i += 3)
            list.Add(new[] { Vert(p, n, uv, t[i]), Vert(p, n, uv, t[i + 1]), Vert(p, n, uv, t[i + 2]) });
        return list;
    }
    static V Vert(Vector3[] p, Vector3[] n, Vector2[] uv, int i) => new V { p = p[i], n = n.Length > i ? n[i] : Vector3.up, uv = uv.Length > i ? uv[i] : Vector2.zero };

    static Mesh ToMesh(List<V[]> tris, string name)
    {
        var mesh = new Mesh { name = name, indexFormat = tris.Count * 3 > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        var flat = tris.SelectMany(t => t).ToArray();
        mesh.SetVertices(flat.Select(x => x.p).ToArray());
        mesh.SetNormals(flat.Select(x => x.n).ToArray());
        mesh.SetUVs(0, flat.Select(x => x.uv).ToArray());
        mesh.SetTriangles(Enumerable.Range(0, flat.Length).ToArray(), 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static Bounds BoundsOf(List<V[]> tris)
    {
        var b = new Bounds(tris[0][0].p, Vector3.zero);
        foreach (var t in tris) foreach (var x in t) b.Encapsulate(x.p);
        return b;
    }

    /// Removes the box {y > sill, z0 < z < z1}: triangles are first split along its three planes so every piece lies
    /// wholly inside or outside, then the inside pieces go. Faces lying exactly on a plane (a cowl top at sill height) stay.
    static List<V[]> CutBox(List<V[]> tris, float sill, float z0, float z1)
    {
        tris = Split(tris, p => p.y - sill);
        tris = Split(tris, p => p.z - z0);
        tris = Split(tris, p => z1 - p.z);
        const float e = 0.002f;
        return tris.Where(t =>
        {
            var c = (t[0].p + t[1].p + t[2].p) / 3f;
            return !(c.y > sill + e && c.z > z0 + e && c.z < z1 - e);
        }).ToList();
    }

    /// Splits every triangle that crosses the plane d(p) = 0 into pieces on one side each (Sutherland–Hodgman on both halves).
    static List<V[]> Split(List<V[]> tris, System.Func<Vector3, float> d)
    {
        const float e = 1e-4f;
        var result = new List<V[]>(tris.Count + 64);
        var pos = new List<V>(4); var neg = new List<V>(4);
        foreach (var t in tris)
        {
            float d0 = d(t[0].p), d1 = d(t[1].p), d2 = d(t[2].p);
            bool anyPos = d0 > e || d1 > e || d2 > e, anyNeg = d0 < -e || d1 < -e || d2 < -e;
            if (!(anyPos && anyNeg)) { result.Add(t); continue; }
            pos.Clear(); neg.Clear();
            for (int i = 0; i < 3; i++)
            {
                V a = t[i], bb = t[(i + 1) % 3];
                float da = d(a.p), db = d(bb.p);
                if (da >= -e) pos.Add(a);
                if (da <= e) neg.Add(a);
                if ((da > e && db < -e) || (da < -e && db > e))
                {
                    float k = da / (da - db);
                    var m = new V { p = Vector3.Lerp(a.p, bb.p, k), n = Vector3.Slerp(a.n, bb.n, k), uv = Vector2.Lerp(a.uv, bb.uv, k) };
                    pos.Add(m); neg.Add(m);
                }
            }
            Fan(pos, result); Fan(neg, result);
        }
        return result;
    }
    static void Fan(List<V> poly, List<V[]> into) { for (int i = 1; i + 1 < poly.Count; i++) into.Add(new[] { poly[0], poly[i], poly[i + 1] }); }

    /// UV of the darkest colour the body uses (tyre rubber on most, dull metal on the tractor): the interior's colour.
    static Vector2 DarkestUv(List<V[]> tris, Texture2D colormap)
    {
        var best = tris[0][0].uv; float darkest = 9f;
        foreach (var t in tris)
        {
            Color.RGBToHSV(colormap.GetPixelBilinear(t[0].uv.x, t[0].uv.y), out _, out _, out float val);
            if (val < darkest) { darkest = val; best = t[0].uv; }
        }
        return best;
    }

    static float HalfWidth(List<V[]> tris, float z0, float z1, float y0, float y1)
    {
        float w = 0f;
        foreach (var t in tris) foreach (var x in t)
            if (x.p.z >= z0 && x.p.z <= z1 && x.p.y >= y0 && x.p.y <= y1) w = Mathf.Max(w, Mathf.Abs(x.p.x));
        return w;
    }
    static float TopOf(List<V[]> tris, float z0, float z1)
    {
        float top = float.NegativeInfinity;
        foreach (var t in tris) foreach (var x in t) if (x.p.z >= z0 && x.p.z <= z1) top = Mathf.Max(top, x.p.y);
        return top;
    }

    /// Clockwise as seen from the side `n` points to (Unity's front face); the open material draws both sides anyway.
    static void Quad(List<V[]> into, Vector2 uv, Vector3 n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        V A = new() { p = a, n = n, uv = uv }, B = new() { p = b, n = n, uv = uv }, C = new() { p = c, n = n, uv = uv }, D = new() { p = d, n = n, uv = uv };
        into.Add(new[] { A, B, C }); into.Add(new[] { A, C, D });
    }

    /// A wall across the tub at z, facing `dir` (+1 toward the front of the car): the lower part spans the cabin's inner
    /// width from the deck to the sill; if the body behind it stands taller than the sill (a cargo box, a cab roof line),
    /// an upper part at that body's width closes the hole the cut opened into it.
    static void Wall(List<V[]> into, Vector2 uv, float z, float dir, float deckY, float sill, float w, float upperW, float top)
    {
        var n = Vector3.forward * dir;
        if (dir > 0) Quad(into, uv, n, new(w, deckY, z), new(w, sill, z), new(-w, sill, z), new(-w, deckY, z));
        else Quad(into, uv, n, new(-w, deckY, z), new(-w, sill, z), new(w, sill, z), new(w, deckY, z));
        if (top > sill + 0.02f && upperW > 0.05f)
        {
            if (dir > 0) Quad(into, uv, n, new(upperW, sill, z), new(upperW, top, z), new(-upperW, top, z), new(-upperW, sill, z));
            else Quad(into, uv, n, new(-upperW, sill, z), new(-upperW, top, z), new(upperW, top, z), new(upperW, sill, z));
        }
    }

    /// Real car materials from a flat palette: every colour cell in Kenney's colormap is classified by what it is —
    /// dark greys are rubber (matte), light greys are metal (chrome rims, bumpers), whites are glass and lights
    /// (glassy), everything saturated is paint (glossy, faint metallic flake) — and written as a metallic (R) /
    /// smoothness (A) map for URP Lit. The karts stop reading as clay and start reading as painted metal on rubber.
    static void CarPaint(Material carkit, string colormapPath)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var bytes = File.ReadAllBytes(colormapPath);
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (lit == null || !ImageConversion.LoadImage(src, bytes)) { Debug.LogWarning("[content] car paint skipped"); return; }
        var map = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        var px = src.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            Color.RGBToHSV(px[i], out _, out float sat, out float val);
            float metallic, smooth;
            if (val < 0.30f) { metallic = 0f; smooth = 0.34f; }                    // tyres, dark trim: matte rubber
            else if (sat < 0.20f && val < 0.86f) { metallic = 0.55f; smooth = 0.66f; }   // rims, bumpers: brushed metal, not a mirror
            else if (sat < 0.20f) { metallic = 0f; smooth = 0.92f; }                // glass, lights
            else { metallic = 0.06f; smooth = 0.70f; }                              // paint: glossy, a hint of flake
            px[i] = new Color32((byte)(metallic * 255f), 0, 0, (byte)(smooth * 255f));
        }
        map.SetPixels32(px); map.Apply();
        var mapPath = Art + "Textures/Colormaps/carkit_metallic.png";
        File.WriteAllBytes(mapPath, map.EncodeToPNG());
        Object.DestroyImmediate(src); Object.DestroyImmediate(map);
        AssetDatabase.ImportAsset(mapPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(mapPath);
        importer.sRGBTexture = false; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = false;
        importer.SaveAndReimport();

        carkit.shader = lit;
        carkit.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colormapPath));
        carkit.SetColor("_BaseColor", Color.white);
        carkit.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath));
        carkit.SetFloat("_Smoothness", 1f);
        carkit.SetFloat("_SmoothnessTextureChannel", 0f);
        carkit.SetFloat("_SpecularHighlights", 1f);
        carkit.SetFloat("_EnvironmentReflections", 1f);
        carkit.SetFloat("_Surface", 0f); carkit.SetFloat("_Cull", 2f);
        carkit.EnableKeyword("_METALLICSPECGLOSSMAP");
        carkit.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        carkit.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(carkit);
        Debug.Log("[content] car paint: CarKit on URP Lit with a generated metallic/smoothness map");
    }

    /// Hands on the wheel. Kenney's `drive` clip holds both arms out level at the shoulder, which on these big-headed
    /// bodies puts any wheel in front of the chin; the clip is lowered by ArmDrop so the hands grip at chest height.
    /// The wheel then goes exactly between the fists, rim through both at 3 and 9 o'clock. All of it is measured in
    /// the posed skeleton and the bind pose put back, so the prefab saves as before and the clip poses it at runtime.
    /// Returns the wheel pivot, the arm rotation sign that lifts the left fist (a right turn), and how far the wheel
    /// must turn per degree of arm swing for the rim to follow the hands.
    const float ArmDrop = 30f;
    static (Transform, float, float) SteeringWheel(GameObject go, AnimationClip clip, Material mat)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/Heads/steering_wheel.fbx");
        var body = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.ToLowerInvariant().Contains("body"));
        Transform root = Find(go.transform, "root"), armL = Find(go.transform, "arm-left"), armR = Find(go.transform, "arm-right");
        if (fbx == null || body == null || root == null || armL == null || armR == null)
        { Debug.LogWarning($"[content] {go.name}: no steering wheel"); return (null, 1f, 1f); }

        var rest = go.GetComponentsInChildren<Transform>(true).Select(t => (t, t.localPosition, t.localRotation)).ToArray();
        clip.SampleAnimation(go, 0f);
        // Which way about the arm's own Z lowers it depends on the side and the rig; measure rather than assume.
        float LowerSign(Transform arm)
        {
            var q = arm.localRotation;
            arm.localRotation = q * Quaternion.Euler(0f, 0f, 10f); float up = Fist(body, arm).y;
            arm.localRotation = q * Quaternion.Euler(0f, 0f, -10f); float down = Fist(body, arm).y;
            arm.localRotation = q;
            return up < down ? 1f : -1f;
        }
        float sL = LowerSign(armL), sR = LowerSign(armR);
        foreach (var (arm, sign) in new[] { (armL, sL), (armR, sR) })
        {
            arm.localRotation *= Quaternion.Euler(0f, 0f, sign * ArmDrop);
            // rewrite the clip's own rotation curves for this arm (clip.SetCurve silently leaves a legacy clip as it was)
            var q = arm.localRotation; var path = AnimationUtility.CalculateTransformPath(arm, go.transform);
            foreach (var bind in AnimationUtility.GetCurveBindings(clip).Where(c => c.path == path && c.propertyName.StartsWith("m_LocalRotation.")))
                AnimationUtility.SetEditorCurve(clip, bind, AnimationCurve.Constant(0f, clip.length, q["xyzw".IndexOf(bind.propertyName[^1])]));
        }
        EditorUtility.SetDirty(clip);

        Vector3 left = Fist(body, armL), right = Fist(body, armR);
        float radius = Vector3.Distance(left, right) * 0.5f;
        var pivot = new GameObject("SteeringWheel").transform;
        pivot.SetParent(root, false);
        pivot.position = (left + right) * 0.5f;
        // column raked forward-down like a real kart's: the rim's top sits further from the driver than its bottom
        pivot.rotation = go.transform.rotation * Quaternion.Euler(25f, 0f, 0f);
        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(mesh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        mesh.transform.SetParent(pivot, false);
        mesh.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // same FBX round-trip turn as the heads
        mesh.transform.localScale = Vector3.one * (radius / pivot.lossyScale.x);
        foreach (var r in mesh.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;

        float reach = Vector3.Distance(armL.position, left);   // shoulder to fist
        Debug.Log($"[content] {go.name}: wheel r={radius:F3} reach={reach:F3} lower L{sL:+0;-0} R{sR:+0;-0}");
        foreach (var (t, p, r) in rest) { t.localPosition = p; t.localRotation = r; }
        return (pivot, -sL, reach / Mathf.Max(radius, 0.001f));
    }

    /// Where a fist is in the current pose: the arm's far end. Kenney's blocky rig weights each arm vertex wholly to
    /// its bone, so skinning by hand is exact and does not depend on the editor re-skinning a sampled pose.
    static Vector3 Fist(SkinnedMeshRenderer body, Transform arm)
    {
        var mesh = body.sharedMesh;
        int bi = System.Array.IndexOf(body.bones, arm);
        var toWorld = arm.localToWorldMatrix * mesh.bindposes[bi];
        var weights = mesh.boneWeights;
        var forward = body.transform.root.forward;
        var pts = mesh.vertices.Where((v, i) => weights[i].boneIndex0 == bi && weights[i].weight0 > 0.5f)
                               .Select(v => toWorld.MultiplyPoint3x4(v))
                               .OrderByDescending(p => Vector3.Dot(p, forward)).ToList();
        var tip = pts.Take(Mathf.Max(1, pts.Count / 4)).ToList();
        return tip.Aggregate(Vector3.zero, (a, p) => a + p) / tip.Count;
    }

    static Material ColormapMat(Shader s, string name, string texPath)
    {
        var m = LoadOrCreate(Mats + name + ".mat", s);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (t == null) Debug.LogWarning("[content] missing texture " + texPath); else m.SetTexture("_BaseMap", t);
        return m;
    }

    public static Material LoadOrCreate(string path, Shader s)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
        // An existing material keeps its shader: LookSetup owns the move to Simple Lit, and forcing the unlit shader
        // back here silently undid the look pass on every rerun.
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void Exit(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }
}
