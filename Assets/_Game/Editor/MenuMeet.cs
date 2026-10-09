using System.Collections.Generic;
using System.IO;
using System.Linq;
using KartCup;
using UnityEditor;
using UnityEngine;

// The title's street: a stretch of the coast road after dark, three of the drivers standing about with their karts
// under a tall hoarding that plays the teaser. Built from what the race scenes already made (Palm Bay's wet road,
// its sodium lamp and halo), so it is the same night. MenuBackdrop tracks the camera along it; MeetActor keeps the
// drivers busy and their heads on the camera.
public static class MenuMeet
{
    const string Art = "Assets/_Game/Art/", MeshDir = Art + "Models/Props/Menu/", Mats = Art + "Materials/";
    const float HalfRoad = 7f, RoadY = 0.05f, Length = 90f;

    // What a driver is up to on the street: by their kart (Lean, Wipe, Sit) or about their own business (the rest). 9 Oct
    // 2026, the user: "every character shouldn't have karts, that is repetitive; someone can be playing on the arcade
    // machine, someone eating a burger". Beach Buggy's character screen gives each racer their own spot and prop.
    enum Act { Lean, Wipe, Sit, Arcade, Burger, Phone, Bench, Crate, Kick }
    static bool ByKart(Act a) => a == Act.Lean || a == Act.Wipe || a == Act.Sit;
    static readonly Dictionary<string, Act> Business = new()
    {
        ["Toly"] = Act.Lean, ["Mert"] = Act.Arcade, ["Akshay"] = Act.Sit, ["Emmett"] = Act.Burger, ["Bitsy"] = Act.Wipe,
        ["Pip"] = Act.Crate, ["Chase"] = Act.Lean, ["Beeman"] = Act.Phone, ["Maroo"] = Act.Bench, ["Andrew"] = Act.Kick,
    };
    const float DriverScale = 2.76f;   // every kart's seat scale: a driver on foot is the size of the one who sits in it

    // Where each stands on the road (metres from the meet's centre: +X along the road to the camera's right, +Z away
    // from the camera). The whole roster, in select-screen order, two staggered lanes 3.25 m apart, so ten bays fit
    // within 15 m of the hoarding and the title's dolly keeps both it and the chosen driver in frame. The driver select
    // drives the camera from bay to bay (MenuBackdrop.Spot). A kart bay's yaw is the kart's heading (180 = nose at the
    // camera); a prop bay lays itself out.
    const float Spacing = 3.75f, NearZ = -2.2f, FarZ = 4.6f;
    static (string who, Vector3 at, float yaw, Act act)[] Crew()
    {
        var roster = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        var names = roster != null && roster.characters.Length > 0 ? roster.characters.Where(c => c != null).Select(c => c.displayName).ToArray() : new[] { "Toly", "Mert", "Akshay" };
        var kartActs = new[] { (Act.Lean, 212f), (Act.Wipe, 258f), (Act.Sit, 228f) };
        return names.Select((who, i) =>
        {
            var act = Business.TryGetValue(who, out var b) ? b : kartActs[i % kartActs.Length].Item1;
            float yaw = ByKart(act) ? kartActs.First(k => k.Item1 == act).Item2 : 0f;
            float x = (i - (names.Length - 1) / 2f) * Spacing;
            if (x > 12f) x += 3.5f;   // the two bays right of the home stage (x 11.2) step along the street: a clear pocket behind the hero
            return (who, new Vector3(x, 0f, i % 2 == 0 ? NearZ : FarZ), yaw, act);
        }).ToArray();
    }

    /// True where the meet stands: the menu keeps its palms and rocks off the road and out of the camera's way.
    public static bool Covers(Vector3 centre, Vector3 p) =>
        Mathf.Abs(p.x - centre.x) < Length / 2f + 4f && p.z > centre.z - 34f && p.z < centre.z + HalfRoad + 9f;

    public static Transform Build(Vector3 at)
    {
        Directory.CreateDirectory(MeshDir);
        var root = new GameObject("Meet").transform;
        root.position = at;
        Street(root);
        Hoarding(root);
        foreach (var c in Crew())
        {
            if (ByKart(c.act)) Park(root, c.who, c.at, c.yaw, c.act); else Scene(root, c.who, c.at, c.act);
            // each bay's own warm key from the camera's side: the select screen stands close, and the two meet lights
            // only reach the middle of the row
            Spot(root, "BayKey_" + c.who, c.at + new Vector3(-2.5f, 6.5f, -7.5f), c.at + new Vector3(0f, 1.4f, 0f), new Color(1f, 0.88f, 0.72f), 110f, 48f);
        }
        // The home stage (10 Oct 2026, Beach Buggy's hub): a plinth in the near lane's gap between Chase and Maroo, where
        // the title shows the chosen driver beside the chosen kart (HomeStage fills it at runtime; Mount grounds tyres at
        // the stage's height, so the stage sits on the plinth's top face). The title's dolly centres on HomeAim, well left
        // of it, so the pair stand in the right half of the screen, 16 m off, with the hoarding centred behind them.
        var stage = new GameObject("HomeStage").transform; stage.SetParent(root, false); stage.localPosition = new Vector3(11.2f, RoadY + 0.16f, -5f);   // the pocket where both lanes have a gap behind it (Chase | Maroo, Beeman | Andrew)
        var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(plinth.GetComponent<Collider>());
        plinth.name = "Plinth"; plinth.transform.SetParent(stage, false); plinth.transform.localPosition = new Vector3(0, -0.08f, 0); plinth.transform.localScale = new Vector3(4.4f, 0.08f, 4.4f);   // radius 2.2 m: clear of Chase's kart and Maroo's bench
        plinth.GetComponent<MeshRenderer>().sharedMaterial = Mat("Turntable") ?? Solid("Plinth", new Color(0.16f, 0.17f, 0.19f), 0.38f);
        var aim = new GameObject("HomeAim").transform; aim.SetParent(root, false); aim.localPosition = new Vector3(8.5f, 0f, -5f);   // MenuBackdrop clamps the dolly's offset to 9 m
        Spot(root, "HomeKey", new Vector3(8f, 7.5f, -12.5f), new Vector3(11.2f, 1.5f, -5f), new Color(1f, 0.9f, 0.76f), 190f, 50f);
        // The lamps stand behind the karts and rim them; the faces need light from the camera's side. A warm key from
        // high on the left, a cool fill low on the right: the film's meet.
        Spot(root, "MeetKey", new Vector3(-10f, 10f, -17f), new Vector3(2f, 2f, 0f), new Color(1f, 0.86f, 0.68f), 230f, 52f);
        Spot(root, "MeetFill", new Vector3(15f, 4f, -15f), new Vector3(3f, 2f, 0f), new Color(0.62f, 0.76f, 1f), 70f, 60f);
        return root;
    }

    // ---------- the road, its pavement and lamps ----------

    static void Street(Transform root)
    {
        float l = Length / 2f;
        // uv in metres, x across from the centreline and y along: what the wet road shader reads
        var mesh = new Mesh
        {
            name = "MeetRoad",
            vertices = new[] { new Vector3(-l, RoadY, -HalfRoad), new Vector3(l, RoadY, -HalfRoad), new Vector3(l, RoadY, HalfRoad), new Vector3(-l, RoadY, HalfRoad) },
            normals = Enumerable.Repeat(Vector3.up, 4).ToArray(),
            tangents = Enumerable.Repeat(new Vector4(0, 0, 1, 1), 4).ToArray(),
            uv = new[] { new Vector2(-HalfRoad, -l), new Vector2(-HalfRoad, l), new Vector2(HalfRoad, l), new Vector2(HalfRoad, -l) },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
        };
        var road = new GameObject("Road");
        road.transform.SetParent(root, false);
        road.AddComponent<MeshFilter>().sharedMesh = Save(mesh);
        road.AddComponent<MeshRenderer>().sharedMaterial =
            AssetDatabase.LoadAssetAtPath<Material>(Art + "Models/Track/Race1/Road_beach.mat") ?? Mat("Road");

        var line = Mat("RoadLine");
        foreach (float side in new[] { -1f, 1f })
            Box(root, "Edge", line, new Vector3(0, RoadY + 0.006f, side * (HalfRoad - 0.4f)), new Vector3(Length, 0.012f, 0.16f));
        for (float x = -l + 3f; x < l; x += 9f)
            Box(root, "Dash", line, new Vector3(x, RoadY + 0.006f, 0), new Vector3(3f, 0.012f, 0.16f));

        var paving = ContentSetup.LoadOrCreate(Mats + "Meet_Paving.mat", Shader.Find("Universal Render Pipeline/Lit"));
        paving.SetColor("_BaseColor", new Color(0.23f, 0.235f, 0.25f)); paving.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(paving);
        Box(root, "Pavement", paving, new Vector3(0, 0.1f, HalfRoad + 2.5f), new Vector3(Length, 0.2f, 5f));

        // Palm Bay's posts either side of the hoarding, arms out over the far lane: they back-light the karts.
        var halo = Mat("Lamp_Halo");
        var colour = halo != null ? halo.GetColor("_Color") : new Color(1f, 0.62f, 0.28f);
        var heads = new List<Vector3>();
        // one out of frame on the left (its halo would sit behind the wordmark); the right one behind the hoarding's plane since
        // 10 Oct 2026, so its head hides behind the screen and its halo stays out of the title's top bar, while its light
        // still rims the far lane from behind
        foreach (var (x, z) in new[] { (-24f, HalfRoad + 0.9f), (9f, HalfRoad + 6.5f) })
            heads.Add(TrackSetup.LampPost(root, "Lamp", root.TransformPoint(new Vector3(x, 0.2f, z)), root.rotation * Quaternion.Euler(0, 180f, 0), Mat("Lamp_Pole"), Mat("Lamp_Glow"), colour));
        if (halo == null) { Debug.LogWarning("[menu] no lamp halo: build the race scenes first"); return; }
        var glow = new GameObject("LampGlows");
        glow.transform.SetParent(root, false);
        glow.AddComponent<MeshFilter>().sharedMesh = Save(TrackSetup.GlowMesh(root, heads), "MeetGlows");
        var mr = glow.AddComponent<MeshRenderer>(); mr.sharedMaterial = halo; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------- the hoarding ----------

    /// Two posts, a dark frame and a 16:9 screen with the teaser on it (the same clip and render texture the title
    /// used to show flat). Silent: the menu has its own music. The screen throws its light down on the road.
    static void Hoarding(Transform root)
    {
        const float W = 13.2f, H = 7.43f, Foot = 4.1f;      // tall: these drivers stand over three metres
        var board = new GameObject("Hoarding").transform;
        board.SetParent(root, false);
        board.localPosition = new Vector3(3.2f, 0.2f, HalfRoad + 4.4f); board.localRotation = Quaternion.Euler(0, 7f, 0);
        var steel = Mat("Lamp_Pole");
        float mid = Foot + H / 2f;
        foreach (float x in new[] { -4.5f, 4.5f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.name = "Post"; post.transform.SetParent(board, false);
            post.transform.localPosition = new Vector3(x, mid / 2f, 0.6f); post.transform.localScale = new Vector3(0.7f, mid / 2f, 0.7f);
            post.GetComponent<MeshRenderer>().sharedMaterial = steel;
        }
        Box(board, "Frame", steel, new Vector3(0, mid, 0.25f), new Vector3(W + 0.7f, H + 0.7f, 0.46f));
        Box(board, "Walkway", steel, new Vector3(0, Foot - 0.6f, -0.5f), new Vector3(W + 0.7f, 0.1f, 1.5f));
        Box(board, "Rail", steel, new Vector3(0, Foot + 0.3f, -1.2f), new Vector3(W + 0.7f, 0.07f, 0.07f));

        const string rtPath = "Assets/_Game/Video/TeaserRT.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null) { rt = new RenderTexture(1280, 720, 0) { name = "TeaserRT" }; AssetDatabase.CreateAsset(rt, rtPath); }
        var lit = ContentSetup.LoadOrCreate(Mats + "Meet_Screen.mat", Shader.Find("Universal Render Pipeline/Unlit"));
        lit.SetTexture("_BaseMap", rt); lit.SetColor("_BaseColor", Color.white * 1.25f);   // a little past 1: the bloom makes it a lit screen, not a poster
        EditorUtility.SetDirty(lit);
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);      // a quad faces -Z: toward the road
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        screen.name = "Screen"; screen.transform.SetParent(board, false);
        screen.transform.localPosition = new Vector3(0, mid, 0.01f); screen.transform.localScale = new Vector3(W, H, 1f);
        var smr = screen.GetComponent<MeshRenderer>(); smr.sharedMaterial = lit; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/_Game/Video/teaser.mp4");
        if (clip == null) Debug.LogWarning("[menu] Assets/_Game/Video/teaser.mp4 missing: the hoarding stays dark");
        var player = screen.AddComponent<UnityEngine.Video.VideoPlayer>();
        player.clip = clip; player.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture; player.targetTexture = rt;
        player.isLooping = true; player.playOnAwake = true; player.waitForFirstFrame = true; player.skipOnDrop = true;
        player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;

        // ponytail: one steady cool light for the screen's throw. Following the film's own colour means reading the
        // render texture back every frame; do that if the steady light ever reads as fake.
        var throwLight = new GameObject("ScreenLight").AddComponent<Light>();
        throwLight.transform.SetParent(board, false);
        throwLight.transform.localPosition = new Vector3(0, mid, -1f);
        throwLight.transform.LookAt(root.TransformPoint(new Vector3(2.5f, 0f, -1f)));
        throwLight.type = LightType.Spot; throwLight.spotAngle = 120f; throwLight.innerSpotAngle = 60f; throwLight.range = 40f;
        throwLight.intensity = 200f; throwLight.color = new Color(0.62f, 0.78f, 1f); throwLight.shadows = LightShadows.None;
    }

    // ---------- the drivers: by their karts ----------

    static System.Func<string, bool, AnimationClip> Takes(GameObject driverPrefab, string who)
    {
        var model = PodiumSetup.ModelOf(AssetDatabase.GetAssetPath(driverPrefab));
        return (take, loop) => PodiumSetup.Clip(model, take, $"meet_{take}_{who}", loop);
    }

    static (CharacterDef def, GameObject driver) Cast(string who)
    {
        var def = AssetDatabase.LoadAssetAtPath<CharacterDef>($"Assets/_Game/Data/Character_{who}.asset");
        var driver = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Drivers/Driver_{def.driverId}.prefab") : null;
        return (def, driver);
    }

    static void Park(Transform root, string who, Vector3 at, float yaw, Act act)
    {
        var (def, driverPrefab) = Cast(who);
        var kartPrefab = def != null ? AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Karts/Kart_{def.kartId}.prefab") : null;
        if (kartPrefab == null || driverPrefab == null) { Debug.LogWarning($"[menu] meet: no kart or driver for {who}"); return; }

        var slot = new GameObject("Kart_" + who).transform;
        slot.SetParent(root, false);
        slot.localPosition = at + Vector3.up * RoadY; slot.localRotation = Quaternion.Euler(0, yaw, 0);
        // The select screen's display kart (tyres on the ground, nothing driving it), then stripped to its meshes: a
        // kart saved in a scene would wake its physics and race scripts in the menu.
        var kart = CharacterCarousel.Mount(kartPrefab, null, slot, Color.white, false);
        var seat = kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
        float scale = seat != null ? seat.lossyScale.x : DriverScale;       // the driver as big as the one who sits in it
        foreach (var ps in kart.GetComponentsInChildren<ParticleSystem>(true)) if (ps != null) Object.DestroyImmediate(ps.gameObject);
        for (int pass = 0; pass < 4; pass++)                        // some require each other: what one pass cannot remove, the next can
            foreach (var c in kart.GetComponentsInChildren<Component>(true))
                if (c != null && !(c is Transform || c is MeshFilter || c is Renderer)) Object.DestroyImmediate(c);
        if (def.tint != Color.white)                                // CharacterDef.Paint, on material assets: instances made in edit mode leak into the scene
            foreach (var r in kart.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Body"))
                r.sharedMaterials = r.sharedMaterials.Select((m, i) =>
                {
                    var p = ContentSetup.LoadOrCreate(Mats + $"Meet_{who}_{i}.mat", m.shader);
                    p.CopyPropertiesFromMaterial(m); p.SetColor("_BaseColor", def.tint); EditorUtility.SetDirty(p);
                    return p;
                }).ToArray();

        // the kart's box in its slot's frame: X across (left is -X), Z along (the nose is +Z)
        var box = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
        foreach (var r in kart.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Body"))
        {
            var lb = r.localBounds; var m = slot.worldToLocalMatrix * r.transform.localToWorldMatrix;
            for (int k = 0; k < 8; k++)
            {
                var p = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? lb.min.x : lb.max.x, (k & 2) == 0 ? lb.min.y : lb.max.y, (k & 4) == 0 ? lb.min.z : lb.max.z));
                if (any) box.Encapsulate(p); else { box = new Bounds(p, Vector3.zero); any = true; }
            }
        }

        // A soft dark patch under the kart and, below, under the driver: the moon's shadow falls metres away, and
        // without contact they hover.
        Contact(slot, new Vector3(box.center.x, 0.04f, box.center.z), new Vector2(box.size.x, box.size.z) * 1.55f);

        var Take = Takes(driverPrefab, who);
        var holder = new GameObject("Driver_" + who).transform;
        holder.SetParent(slot, false);
        holder.localScale = Vector3.one * scale;
        AnimationClip main; AnimationClip[] others; float floor = 0f;      // what the driver stands (or sits) on, over the road
        switch (act)
        {
            case Act.Lean:      // hips against the left flank, back to the kart, weight on it
                holder.localPosition = new Vector3(box.min.x - 0.1f * scale, 0f, box.center.z - 0.35f);
                holder.localRotation = Quaternion.Euler(-9f, -90f, 0f);
                main = Take("idle", true); others = new[] { Take("emote-yes", false) };
                break;
            case Act.Wipe:      // at the nose, on its far corner and half turned to the camera, polishing it with the near hand
                holder.localPosition = new Vector3(0.42f * scale, 0f, box.max.z + 0.16f * scale);
                holder.localRotation = Quaternion.Euler(0f, 215f, 0f);
                main = Take("interact-right", true); others = new[] { Take("pick-up", false), Take("emote-yes", false) };
                break;
            default:            // sat on the nose, legs out in front
                holder.localPosition = new Vector3(0f, 0f, box.max.z - 0.7f);
                holder.localRotation = Quaternion.identity;
                main = Take("sit", true); others = new AnimationClip[0];
                floor = TopAt(kart, slot, new Vector2(0f, box.max.z - 0.7f), 0.5f);
                break;
        }
        var d = Stand(slot, holder, driverPrefab, main, others, floor, act == Act.Wipe, out var body);
        if (d == null) return;
        if (act == Act.Wipe)
        {
            var rag = Box(Find(d.transform, "arm-right"), "Rag", RagMaterial(), Vector3.zero, Vector3.one);
            rag.position = PodiumSetup.End(body, Find(d.transform, "arm-right"));
            rag.rotation = slot.rotation; SetWorldScale(rag, new Vector3(0.1f, 0.03f, 0.13f) * scale);
        }
        float tyres = kart.GetComponentsInChildren<MeshRenderer>().Min(r => r.bounds.min.y) - slot.position.y;
        Debug.Log($"[menu] meet: {who} {act} by a {def.kartId} ({box.size.x:F2} x {box.size.z:F2} m, top {box.max.y:F2}, lowest {tyres:F2}), driver scale {scale:F2}, on {floor:F2}");
    }

    /// The driver on their feet (or seat) in `holder`: the takes, the rig skinned by hand down onto `floor`, the contact
    /// patch, the moon's shadow for everything in the bay, the actor. Null when the main take is missing.
    static GameObject Stand(Transform slot, Transform holder, GameObject driverPrefab, AnimationClip main, AnimationClip[] others, float floor, bool glances, out SkinnedMeshRenderer body)
    {
        body = null;
        if (main == null) return null;
        var d = (GameObject)PrefabUtility.InstantiatePrefab(driverPrefab, holder);
        PrefabUtility.UnpackPrefabInstance(d, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var pose in d.GetComponentsInChildren<BlockyDriverPose>(true)) Object.DestroyImmediate(pose);
        foreach (var t in d.GetComponentsInChildren<Transform>(true).Where(t => t != null && t.name.ToLowerInvariant().Contains("steering")).ToList())
            if (t != null) Object.DestroyImmediate(t.gameObject);                       // nobody stands about holding a steering wheel
        var anim = d.GetComponent<Animation>();
        others = others.Where(c => c != null).ToArray();
        foreach (var c in others.Prepend(main)) anim.AddClip(c, c.name);
        anim.clip = main; anim.playAutomatically = true; anim.cullingType = AnimationCullingType.AlwaysAnimate;
        main.SampleAnimation(d, 0f);                                 // the pose the scene opens (and is previewed) in
        body = d.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.ToLowerInvariant().Contains("body"));
        foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        // Down onto it: the rig's origin is the hips, and each take stands them at its own height. Skinned by hand
        // (Kenney's rig weights every vertex wholly to one bone, as PodiumSetup.End relies on): exact in edit mode.
        var skin = body.sharedMesh; var weights = skin.boneWeights;
        var toWorld = body.bones.Select((bone, i) => bone.localToWorldMatrix * skin.bindposes[i]).ToArray();
        var ys = skin.vertices.Select((v, i) => toWorld[weights[i].boneIndex0].MultiplyPoint3x4(v).y).ToList();
        d.transform.position += Vector3.up * (slot.position.y + floor - ys.Min());
        if (floor == 0f) Contact(slot, new Vector3(holder.localPosition.x, 0.045f, holder.localPosition.z), Vector2.one * 0.75f * holder.localScale.x);
        // The race turns its karts' shadows off (twelve of them, on a phone) and lets the blob stand in. A street of
        // parked ones can afford the moon's: long, toward the camera.
        foreach (var r in slot.GetComponentsInChildren<Renderer>().Where(r => r.name != "BlobShadow" && r.name != "Contact"))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        d.AddComponent<MeetActor>().Bind(anim, Find(d.transform, "head"), others.Select(c => c.name).ToArray(), glances);
        return d;
    }

    // ---------- the drivers: about their own business ----------

    /// A bay without a kart: a prop at the bay's origin and the driver using it. Every prop faces so the driver is
    /// side-on or three-quarter to the camera (MeetActor turns the head the rest of the way).
    static void Scene(Transform root, string who, Vector3 at, Act act)
    {
        var (def, driverPrefab) = Cast(who);
        if (driverPrefab == null) { Debug.LogWarning($"[menu] meet: no driver for {who}"); return; }
        var slot = new GameObject("Bay_" + who).transform;
        slot.SetParent(root, false);
        slot.localPosition = at + Vector3.up * RoadY;
        float scale = DriverScale;
        var Take = Takes(driverPrefab, who);
        var holder = new GameObject("Driver_" + who).transform;
        holder.SetParent(slot, false);
        holder.localScale = Vector3.one * scale;
        AnimationClip main; AnimationClip[] others; float floor = 0f; bool glances = false;
        switch (act)
        {
            case Act.Arcade:    // the cabinet faces the road (its screen and marquee read); the driver hangs at its side, Beach Buggy's Rez
                Arcade(slot);
                holder.localPosition = new Vector3(1.55f, 0f, -0.15f); holder.localRotation = Quaternion.Euler(0f, 192f, 0f);
                main = Take("idle", true); others = new[] { Take("emote-yes", false), Take("emote-no", false) };
                break;
            case Act.Burger:    // standing at a crate table with a bottle on it, both hands on the burger
                var table = Prop(slot, "Survival/box-large.fbx", "Survival", new Vector3(-0.95f * scale * 0.5f - 0.7f, 0f, 0.5f), 15f, 1.3f);
                if (table != null) Prop(slot, "Survival/bottle.fbx", "Survival", new Vector3(table.transform.localPosition.x, TopAt(table, slot, new Vector2(table.transform.localPosition.x, 0.5f), 0.4f, null), 0.5f), 0f, 1.3f);
                holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.Euler(0f, 200f, 0f);
                main = Take("holding-both", true); others = new[] { Take("emote-yes", false) };
                break;
            case Act.Phone:     // reading something, now and then looking up
                holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.Euler(0f, 192f, 0f);
                main = Take("holding-right", true); others = new[] { Take("emote-no", false) }; glances = true;
                break;
            case Act.Bench:     // on a park bench, facing the road
            {
                var bench = Prop(slot, "Holiday/bench.fbx", "Holiday", Vector3.zero, 180f, scale * 0.9f);
                var bb = BoundsOf(bench, slot);                                  // the kit's pivots sit at a corner: work from the bounds
                float x = bb.center.x, z = bb.min.z + bb.size.z * 0.4f;           // the hips over the seat, not the backrest
                holder.localPosition = new Vector3(x, 0f, z); holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                main = Take("sit", true); others = new[] { Take("emote-yes", false) };
                floor = TopAt(bench, slot, new Vector2(x, z), 0.25f * bb.size.x, null);
                if (floor <= 0f) floor = bb.min.y + bb.size.y * 0.45f;
                break;
            }
            case Act.Crate:     // sat on a crate, a case of bottles beside
            {
                var crate = Prop(slot, "Survival/box-large.fbx", "Survival", Vector3.zero, 10f, 4f);        // the kit's box is a quarter metre: four of them make a seat
                Prop(slot, "Pirate/crate-bottles.fbx", "Pirate", new Vector3(1.9f, 0f, 0.5f), -20f, 2f);
                var cb = BoundsOf(crate, slot);
                holder.localPosition = new Vector3(cb.center.x, 0f, cb.min.z + 0.3f); holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                main = Take("sit", true); others = new[] { Take("emote-no", false) };
                floor = cb.max.y;
                break;
            }
            default:            // Kick: a traffic cone, kicked and kicked again; the kicker three-quarters to the camera, the cone along his kick
                Prop(slot, "ToyCar/item-cone.fbx", "ToyCar", new Vector3(0.75f, 0f, -0.75f), 0f, 2.2f);
                holder.localPosition = new Vector3(-0.8f, 0f, 0.8f); holder.localRotation = Quaternion.Euler(0f, 135f, 0f);
                main = Take("attack-kick-right", true); others = new[] { Take("emote-no", false) };
                break;
        }
        var d = Stand(slot, holder, driverPrefab, main, others, floor, glances, out var body);
        if (d == null) { Debug.LogWarning($"[menu] meet: {who}'s take for {act} is missing"); return; }
        if (act == Act.Burger) BurgerAt(d, body, scale);
        if (act == Act.Phone) PhoneAt(d, body, scale);
        Debug.Log($"[menu] meet: {who} {act}, driver scale {scale:F2}, on {floor:F2}");
    }

    /// A Kenney prop under the bay, in one of the kit materials.
    static GameObject Prop(Transform slot, string fbx, string mat, Vector3 at, float yaw, float scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/Props/" + fbx);
        if (prefab == null) { Debug.LogWarning($"[menu] meet: no prop {fbx}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
        go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
        var m = Mat(mat);
        if (m != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
        return go;
    }

    static Bounds BoundsOf(GameObject go, Transform slot)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
        if (go == null) return b;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null))
        {
            var m = slot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices) { var p = m.MultiplyPoint3x4(v); if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; } }
        }
        return b;
    }

    /// An arcade cabinet built from primitives, the front facing the bay's +X: a violet shell, the lockup on the marquee,
    /// a track on the screen (lit, and lighting the player), two sticks and four buttons on a sloped panel.
    static void Arcade(Transform slot)
    {
        var cab = new GameObject("Arcade").transform; cab.SetParent(slot, false); cab.localScale = Vector3.one * 1.15f;   // front to -Z: the road and the camera
        var shell = Solid("Cabinet", new Color(0.18f, 0.04f, 0.44f), 0.35f);
        var dark = Solid("CabinetDark", new Color(0.05f, 0.02f, 0.12f), 0.25f);
        var gold = Solid("CabinetGold", new Color(1f, 0.69f, 0.15f), 0.55f);
        var red = Solid("CabinetRed", new Color(0.9f, 0.12f, 0.1f), 0.5f);
        Box(cab, "Body", shell, new Vector3(0, 1.0f, 0.2f), new Vector3(1.3f, 2.0f, 1.0f));
        Box(cab, "Head", shell, new Vector3(0, 2.5f, 0.42f), new Vector3(1.3f, 1.0f, 0.56f));
        Box(cab, "Marquee", dark, new Vector3(0, 2.78f, 0.1f), new Vector3(1.3f, 0.44f, 0.1f));
        var panel = Box(cab, "Panel", dark, new Vector3(0, 1.5f, -0.33f), new Vector3(1.3f, 0.1f, 0.6f)); panel.localRotation = Quaternion.Euler(-14f, 0, 0);
        Box(cab, "Trim", gold, new Vector3(0, 2.02f, -0.29f), new Vector3(1.32f, 0.05f, 0.05f));
        var screenTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/UI/Tracks/beach.png");
        Quad(cab, "Screen", Unlit("ArcadeScreen", screenTex, 1.3f), new Vector3(0, 2.1f, -0.265f), Quaternion.Euler(-8f, 0, 0), new Vector2(1.08f, 0.82f));
        var lockup = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/UI/kartel_lockup.png");
        Quad(cab, "MarqueeFace", Unlit("ArcadeMarquee", lockup, 1.15f), new Vector3(0, 2.78f, 0.045f), Quaternion.identity, new Vector2(1.2f, 0.44f));
        foreach (float x in new[] { -0.36f, 0.2f })
        {
            Cyl(cab, "Stick", dark, new Vector3(x, 1.66f, -0.42f), 0.03f, 0.1f);
            Ball(cab, "Knob", red, new Vector3(x, 1.8f, -0.42f), 0.07f);
        }
        for (int i = 0; i < 4; i++) Ball(cab, "Button", i % 2 == 0 ? red : gold, new Vector3(-0.2f + i * 0.14f, 1.6f, -0.52f), 0.045f);
        var glow = new GameObject("ScreenGlow").AddComponent<Light>(); glow.transform.SetParent(cab, false);
        glow.transform.localPosition = new Vector3(0, 2.0f, -0.7f);
        glow.type = LightType.Point; glow.range = 3.6f; glow.intensity = 28f; glow.color = new Color(0.55f, 0.75f, 1f); glow.shadows = LightShadows.None;
    }

    /// A burger in both hands: buns, patty, lettuce; rides the right arm bone like the rag.
    static void BurgerAt(GameObject d, SkinnedMeshRenderer body, float scale)
    {
        var r = Find(d.transform, "arm-right"); var l = Find(d.transform, "arm-left");
        if (r == null) return;
        var at = l != null ? (PodiumSetup.End(body, r) + PodiumSetup.End(body, l)) / 2f : PodiumSetup.End(body, r);
        var burger = new GameObject("Burger").transform; burger.SetParent(r, true);
        burger.position = at + d.transform.forward * 0.1f * scale; burger.rotation = d.transform.rotation; SetWorldScale(burger, Vector3.one);   // in front of the hand blocks, not inside them
        float s = 0.075f * scale;
        var bun = Solid("Bun", new Color(0.87f, 0.62f, 0.3f), 0.3f);
        Cyl(burger, "BunBottom", bun, new Vector3(0, -0.55f * s, 0), s, 0.28f * s);
        Cyl(burger, "Patty", Solid("Patty", new Color(0.33f, 0.16f, 0.07f), 0.2f), new Vector3(0, -0.12f * s, 0), 1.05f * s, 0.16f * s);
        Cyl(burger, "Lettuce", Solid("Lettuce", new Color(0.36f, 0.72f, 0.22f), 0.2f), new Vector3(0, 0.1f * s, 0), 1.18f * s, 0.05f * s);
        var top = Ball(burger, "BunTop", bun, new Vector3(0, 0.45f * s, 0), s); top.localScale = new Vector3(2f * s, 1.3f * s, 2f * s);
    }

    /// A phone in the right hand, its screen lit toward the face, and its light on it.
    static void PhoneAt(GameObject d, SkinnedMeshRenderer body, float scale)
    {
        var r = Find(d.transform, "arm-right");
        if (r == null) return;
        var slab = Box(r, "Phone", Solid("PhoneBody", new Color(0.08f, 0.08f, 0.1f), 0.6f), Vector3.zero, Vector3.one);
        slab.position = PodiumSetup.End(body, r) + d.transform.forward * 0.12f * scale + Vector3.up * 0.04f * scale;   // in front of the hand block
        slab.rotation = d.transform.rotation * Quaternion.Euler(-40f, 0f, 0f);
        SetWorldScale(slab, new Vector3(0.07f, 0.14f, 0.008f) * scale);
        var face = Quad(slab, "Glow", Unlit("PhoneGlow", null, 1f, new Color(0.62f, 0.8f, 1f)), new Vector3(0, 0, -0.52f), Quaternion.identity, new Vector2(0.86f, 0.9f));
        var light = new GameObject("PhoneLight").AddComponent<Light>(); light.transform.SetParent(slab, false); light.transform.localPosition = new Vector3(0, 0, -2f);
        light.type = LightType.Point; light.range = 1.6f * scale; light.intensity = 9f; light.color = new Color(0.62f, 0.8f, 1f); light.shadows = LightShadows.None;
    }

    // ---------- primitives and materials ----------

    static Material Solid(string name, Color colour, float smooth)
    {
        var m = ContentSetup.LoadOrCreate(Mats + "Meet_" + name + ".mat", Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", colour); m.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(m);
        return m;
    }
    static Material Unlit(string name, Texture2D tex, float gain, Color? tint = null)
    {
        var m = ContentSetup.LoadOrCreate(Mats + "Meet_" + name + ".mat", Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", (tint ?? Color.white) * gain); EditorUtility.SetDirty(m);
        return m;
    }
    static Transform Primitive(PrimitiveType kind, Transform parent, string name, Material mat, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(kind);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }
    static Transform Quad(Transform parent, string name, Material mat, Vector3 pos, Quaternion rot, Vector2 size)
    {
        var q = Primitive(PrimitiveType.Quad, parent, name, mat, pos);
        q.localRotation = rot; q.localScale = new Vector3(size.x, size.y, 1f);
        q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return q;
    }
    static Transform Cyl(Transform parent, string name, Material mat, Vector3 pos, float radius, float halfHeight)
    {
        var c = Primitive(PrimitiveType.Cylinder, parent, name, mat, pos); c.localScale = new Vector3(radius * 2f, halfHeight, radius * 2f); return c;
    }
    static Transform Ball(Transform parent, string name, Material mat, Vector3 pos, float radius)
    {
        var b = Primitive(PrimitiveType.Sphere, parent, name, mat, pos); b.localScale = Vector3.one * radius * 2f; return b;
    }

    /// The highest point of the kart's meshes within `radius` of a spot (slot frame, X and Z), as a height over the slot.
    static float TopAt(GameObject kart, Transform slot, Vector2 spot, float radius, string only = "Body")
    {
        float top = 0f;
        if (kart == null) return top;
        foreach (var mf in kart.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null && (only == null || f.name == only)))
        {
            var m = slot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = m.MultiplyPoint3x4(v);
                if ((new Vector2(p.x, p.z) - spot).sqrMagnitude < radius * radius) top = Mathf.Max(top, p.y);
            }
        }
        return top;
    }

    // ---------- helpers ----------

    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Mats + name + ".mat");

    static Material RagMaterial()
    {
        var m = ContentSetup.LoadOrCreate(Mats + "Meet_Rag.mat", Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.95f, 0.78f, 0.2f)); m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Box(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static void Spot(Transform root, string name, Vector3 at, Vector3 target, Color colour, float intensity, float angle)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(root, false);
        l.transform.localPosition = at; l.transform.LookAt(root.TransformPoint(target));
        l.type = LightType.Spot; l.spotAngle = angle; l.innerSpotAngle = angle * 0.6f; l.range = 44f; l.intensity = intensity;
        l.color = colour; l.shadows = LightShadows.None;
    }

    /// A soft dark patch flat on the road (KartCup/Contact). The race's blob shadow is a small dot under the middle
    /// of a kart; a parked one, seen from the kerb, wants its whole footprint dark and a soft edge past it.
    static void Contact(Transform parent, Vector3 at, Vector2 size)
    {
        var shader = Shader.Find("KartCup/Contact");
        var mat = ContentSetup.LoadOrCreate(Mats + "Meet_Contact.mat", shader);
        mat.shader = shader; mat.SetFloat("_Strength", 0.8f); EditorUtility.SetDirty(mat);   // LoadOrCreate keeps whatever shader an existing material has
        var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(blob.GetComponent<Collider>());
        blob.name = "Contact"; blob.transform.SetParent(parent, false);
        blob.transform.localPosition = at; blob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        blob.transform.localScale = new Vector3(size.x, size.y, 1f);
        var mr = blob.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    static void SetWorldScale(Transform t, Vector3 world)
    {
        var p = t.parent.lossyScale;
        t.localScale = new Vector3(world.x / p.x, world.y / p.y, world.z / p.z);
    }

    static Mesh Save(Mesh mesh, string name = null)
    {
        if (name != null) mesh.name = name;
        AssetDatabase.CreateAsset(mesh, MeshDir + mesh.name + ".asset");
        return mesh;
    }

    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
}
