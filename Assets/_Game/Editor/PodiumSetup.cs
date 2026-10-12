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
    static readonly (float x, float h)[] Blocks = { (0f, 1.45f), (-2.0f, 1.0f), (2.0f, 0.72f) };
    const float BlockW = 1.95f, BlockD = 1.7f;
    const float Deck = 0.6f;                 // the dais's top: the blocks stand on it
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
        // A show stage in the dark (11 Oct 2026, the user: "lights off ... the platform should be better"): a two-tier round
        // dais ringed in gold light, three stepped blocks capped in their metal with the place lit on the front, a violet
        // wall behind with the Kartel sign in a frame of bulbs, a lighting truss with a can over each block, and a moving
        // head on each tower. All of it is dark until PodiumShow calls it: a cool backlight alone shows their outlines.
        var root = new GameObject("PodiumStage");
        var show = root.AddComponent<PodiumShow>();
        var goldC = Hex("FFC83A"); var silverC = new Color(0.83f, 0.88f, 0.95f); var bronzeC = new Color(0.95f, 0.55f, 0.28f);
        var tints = new[] { goldC, silverC, bronzeC };
        Part(root.transform, "Floor", cylinder, floorMat, new Vector3(0, -0.1f, 0), new Vector3(30, 0.1f, 30));
        // The stage brings its own night: a dark drum round it, drawn from the inside, so the ceremony looks the same
        // on a snowfield at blue hour as on the coast after dark.
        Part(root.transform, "Drum", cylinder, Inside("Podium_Drum", new Color(0.02f, 0.02f, 0.045f)), new Vector3(0, 13f, 0), new Vector3(40f, 15f, 40f));
        var winnerShow = new List<GameObject>();

        // the dais: two tiers, each with a ring of gold light just under its lip (lit with the winner)
        var daisMat = Plain("Podium_Dais", lit, new Color(0.07f, 0.05f, 0.13f), 0.86f);
        Part(root.transform, "DaisLow", cylinder, daisMat, new Vector3(0, 0.15f, 0.3f), new Vector3(12.8f, 0.15f, 12.8f));
        Part(root.transform, "DaisHigh", cylinder, daisMat, new Vector3(0, 0.45f, 0.3f), new Vector3(10.4f, 0.15f, 10.4f));
        var ringGlow = Emit("Podium_RingGlow", lit, goldC, 3f);
        winnerShow.Add(Part(root.transform, "RingLow", cylinder, ringGlow, new Vector3(0, 0.27f, 0.3f), new Vector3(12.95f, 0.02f, 12.95f)).gameObject);
        winnerShow.Add(Part(root.transform, "RingHigh", cylinder, ringGlow, new Vector3(0, 0.57f, 0.3f), new Vector3(10.55f, 0.02f, 10.55f)).gameObject);
        var floorRing = Part(root.transform, "FloorRing", quad, Glow("Podium_Ring", "fx_ring", goldC * 0.6f), new Vector3(0, 0.02f, 0.3f), new Vector3(17f, 17f, 1f));
        floorRing.localRotation = Quaternion.Euler(90, 0, 0);
        winnerShow.Add(floorRing.gameObject);

        // the wall behind, the sign on it and its frame of bulbs (the sign and the bulbs light with the winner)
        Part(root.transform, "Wall", cube, Plain("Podium_Wall", lit, new Color(0.11f, 0.06f, 0.24f), 0.5f), new Vector3(0, 5.1f, 5.6f), new Vector3(17f, 10f, 0.3f));
        var sign = Part(root.transform, "Sign", quad, Sign("Podium_Sign", "Assets/_Game/Art/Textures/UI/kartel_lockup.png"), new Vector3(0, 7.5f, 5.4f), new Vector3(6.4f, 6.4f * 439f / 1199f, 1f));   // high: the gold held up over the winner's head is under it
        winnerShow.Add(sign.gameObject);
        {
            var bulbs = new List<Vector3>();
            float w = 7.6f, h = 3.4f, cy = 7.5f;
            for (float x = -w / 2f; x <= w / 2f + 0.01f; x += 0.43f) { bulbs.Add(new Vector3(x, cy + h / 2f, 5.38f)); bulbs.Add(new Vector3(x, cy - h / 2f, 5.38f)); }
            for (float y = cy - h / 2f + 0.43f; y < cy + h / 2f - 0.2f; y += 0.43f) { bulbs.Add(new Vector3(-w / 2f, y, 5.38f)); bulbs.Add(new Vector3(w / 2f, y, 5.38f)); }
            var go = new GameObject("Bulbs"); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Save(Quads(bulbs, 0.34f), "podium_bulbs");
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Glow("Podium_Bulb", "fx_dot", goldC * 2.6f); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            winnerShow.Add(go);
        }

        // the truss: two towers and a bar across, a can over each block and a moving head on each tower top
        var trussMat = Plain("Podium_Truss", lit, new Color(0.22f, 0.22f, 0.26f), 0.6f);
        foreach (float side in new[] { -1f, 1f }) Part(root.transform, side < 0 ? "TowerL" : "TowerR", cube, trussMat, new Vector3(7.2f * side, 4.6f, 1.2f), new Vector3(0.45f, 9.2f, 0.45f));
        Part(root.transform, "Bar", cube, trussMat, new Vector3(0, 9.2f, 1.2f), new Vector3(14.9f, 0.45f, 0.45f));
        var canMat = Plain("Podium_Can", lit, new Color(0.08f, 0.08f, 0.1f), 0.5f);

        var metals = new[] { gold, silver, bronze };
        var caps = new[] { LitMetal("Podium_CapGold", lit, goldC), LitMetal("Podium_CapSilver", lit, silverC), LitMetal("Podium_CapBronze", lit, bronzeC) };
        var glows = new[] { Emit("Podium_GlowGold", lit, goldC, 2.4f), Emit("Podium_GlowSilver", lit, silverC, 1.8f), Emit("Podium_GlowBronze", lit, bronzeC, 2f) };
        var beamMats = new[] { Beam("Podium_BeamGold", new Color(1f, 0.86f, 0.55f)), Beam("Podium_BeamSilver", new Color(0.82f, 0.9f, 1f)), Beam("Podium_BeamBronze", new Color(1f, 0.7f, 0.45f)) };
        var spots = new Transform[3]; var trophies = new Transform[3]; var placeLights = new Light[3]; var placeShow = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            var (x, h) = Blocks[i];
            float top = Deck + h + 0.14f;
            Part(root.transform, "Block" + Place[i], cube, blockMat, new Vector3(x, Deck + h / 2f, 0), new Vector3(BlockW, h, BlockD));
            Part(root.transform, "Cap" + Place[i], cube, caps[i], new Vector3(x, Deck + h + 0.07f, 0), new Vector3(BlockW + 0.12f, 0.14f, BlockD + 0.12f));
            var number = Text3D(root.transform, "Number" + Place[i], Place[i], Design.Display, Mathf.Min(h * 8f, 9f), tints[i],
                                new Vector3(x, Deck + h * 0.47f, -BlockD / 2f - 0.02f), new Vector2(BlockW, h * 0.9f));
            number.fontStyle = FontStyles.Normal;

            // what lights with this place: the strips up the block's front corners and along its foot, the beam, the pool on top
            var lights = new GameObject("Show" + Place[i]); lights.transform.SetParent(root.transform, false);
            foreach (float side in new[] { -1f, 1f })
                Part(lights.transform, "Strip", cube, glows[i], new Vector3(x + side * (BlockW / 2f - 0.05f), Deck + h / 2f, -BlockD / 2f - 0.03f), new Vector3(0.07f, h, 0.07f));
            Part(lights.transform, "Foot", cube, glows[i], new Vector3(x, Deck + 0.03f, -BlockD / 2f - 0.03f), new Vector3(BlockW, 0.05f, 0.05f));
            var can = new Vector3(x, 8.85f, 1.2f); var aim = new Vector3(x, top + 1f, 0f);
            var beam = new GameObject("Beam"); beam.transform.SetParent(lights.transform, false);
            beam.transform.localPosition = can; beam.transform.localRotation = Quaternion.LookRotation(aim - can, Vector3.up);
            beam.AddComponent<MeshFilter>().sharedMesh = Save(Cone(0.22f, Vector3.Distance(can, aim) + 1.4f, 11f), "podium_beam" + Place[i]);
            var br = beam.AddComponent<MeshRenderer>(); br.sharedMaterial = beamMats[i]; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pool = Part(lights.transform, "Pool", quad, Glow("Podium_Pool" + Place[i], "fx_dot", tints[i] * 0.9f), new Vector3(x, top + 0.02f, 0f), new Vector3(2.6f, 2.6f, 1f));
            pool.localRotation = Quaternion.Euler(90, 0, 0);
            placeShow[i] = lights;

            var fixture = Part(root.transform, "Can" + Place[i], cylinder, canMat, can + Vector3.up * 0.1f, new Vector3(0.42f, 0.22f, 0.42f));
            fixture.localRotation = Quaternion.LookRotation(aim - can, Vector3.up) * Quaternion.Euler(90, 0, 0);
            var l = new GameObject("Light" + Place[i]).AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = can; l.transform.localRotation = Quaternion.LookRotation(aim - can, Vector3.up);
            l.type = LightType.Spot; l.spotAngle = 34f; l.innerSpotAngle = 20f; l.range = 16f; l.intensity = 170f; l.shadows = LightShadows.None;
            l.color = Color.Lerp(Color.white, tints[i], 0.35f);
            placeLights[i] = l;

            var spot = new GameObject("Spot" + Place[i]).transform;
            spot.SetParent(root.transform, false);
            spot.localPosition = new Vector3(x, top, 0.1f);
            spot.localRotation = Quaternion.Euler(0, 180, 0);          // drivers face the camera, at -Z
            spots[i] = spot;

            trophies[i] = Trophy(root.transform, "Trophy" + Place[i], cup, metals[i], plinthMat, cube, new Vector3(x, top, -0.1f));
        }

        // the moving heads: a beam from each tower top, swung by PodiumShow once the winner is called (toward the audience)
        var movers = new List<Transform>();
        foreach (float side in new[] { -1f, 1f })
        {
            var holder = new GameObject(side < 0 ? "HeadL" : "HeadR").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(7.2f * side, 9.6f, 1.2f); holder.localRotation = Quaternion.Euler(0, 180, 0);
            Part(holder, "Body", cube, canMat, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f));
            var pivot = new GameObject("Pivot").transform; pivot.SetParent(holder, false);
            var beam = new GameObject("Beam"); beam.transform.SetParent(pivot, false);
            beam.AddComponent<MeshFilter>().sharedMesh = Save(Cone(0.16f, 19f, 4.5f), "podium_head_beam");
            var br = beam.AddComponent<MeshRenderer>(); br.sharedMaterial = Beam("Podium_BeamHead", new Color(1f, 0.8f, 0.4f), 0.55f); br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            movers.Add(pivot);
            winnerShow.Add(beam);
        }

        // Party poppers either side on the deck, aimed up over the podium; confetti rains once the winner is called.
        var poppers = new List<ParticleSystem>();
        foreach (float side in new[] { -1f, 1f })
        {
            var holder = new GameObject(side < 0 ? "PopperLeft" : "PopperRight").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(3.7f * side, Deck, -2.3f);
            var aim = (new Vector3(0, 4.6f, 0.4f) - holder.localPosition).normalized;
            holder.localRotation = Quaternion.FromToRotation(Vector3.up, aim);
            Part(holder, "Cone", cone, popperMat, Vector3.zero, Vector3.one * 2.6f);
            var burst = Particles(holder, "Burst", confettiMat, quad, rain: false);
            burst.transform.localPosition = new Vector3(0, 0.8f, 0);
            burst.transform.localRotation = Quaternion.Euler(-90, 0, 0);   // the system emits along its +Z: up the cone
            poppers.Add(burst);
        }
        var rain = Particles(root.transform, "Rain", confettiMat, quad, rain: true);
        rain.transform.localPosition = new Vector3(0, 9f, 0.5f);

        // gold dust rising off the deck once the winner is called, each mote flaring and dimming
        {
            var go = new GameObject("Glitter"); go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(0, Deck + 0.2f, 0.5f);
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
            winnerShow.Add(go);
        }

        // The house lights: two warm spots from the front corners and a gold rim from behind, up with the winner. And a cool
        // backlight that stays on, so in the dark the three are outlines against the wall.
        var house = new List<Light>();
        foreach (float side in new[] { -1f, 1f })
        {
            var l = new GameObject(side < 0 ? "StageLightL" : "StageLightR").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(6f * side, 7.5f, -8f);
            l.transform.LookAt(root.transform.position + new Vector3(0, 2.2f, 0));
            l.type = LightType.Spot; l.spotAngle = 85f; l.innerSpotAngle = 50f; l.range = 30f; l.intensity = 120f;   // a 10 m throw
            l.color = new Color(1f, 0.95f, 0.88f); l.shadows = LightShadows.None;
            house.Add(l);
        }
        var rim = new GameObject("StageRim").AddComponent<Light>();
        rim.transform.SetParent(root.transform, false);
        rim.transform.localPosition = new Vector3(0, 6.5f, 5.0f);
        rim.transform.LookAt(root.transform.position + new Vector3(0, 2.4f, 0));
        rim.type = LightType.Spot; rim.spotAngle = 80f; rim.innerSpotAngle = 40f; rim.range = 24f; rim.intensity = 90f;
        rim.color = new Color(1f, 0.74f, 0.3f); rim.shadows = LightShadows.None;
        house.Add(rim);
        var back = new GameObject("Backlight").AddComponent<Light>();
        back.transform.SetParent(root.transform, false);
        back.transform.localPosition = new Vector3(0, 7.5f, 4.6f);
        back.transform.LookAt(root.transform.position + new Vector3(0, 2f, -0.5f));
        back.type = LightType.Spot; back.spotAngle = 70f; back.innerSpotAngle = 40f; back.range = 20f; back.intensity = 28f;
        back.color = new Color(0.45f, 0.55f, 1f); back.shadows = LightShadows.None;

        // the drum roll on its own source (2D), so a tap that skips to the winner can cut it
        var roll = root.AddComponent<AudioSource>();
        roll.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/podium/drumroll.wav");
        roll.playOnAwake = false; roll.spatialBlend = 0f; roll.volume = 0.95f;

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
        // as wide as the leaderboard under it: wider, a winner's name ran into the Kartel sign over the podium
        var subtitle = Words("Big", canvasGo.transform, Design.Hero, "", 168, goldC, new Vector2(0, 1), new Vector2(112, -158), new Vector2(960, 220));
        subtitle.enableAutoSizing = true; subtitle.fontSizeMin = 56; subtitle.fontSizeMax = 168;   // 84 left "AND THE WINNER IS..." 250 px past its box (12 Oct 2026)   // "0.54 SOL" at full size, "YOU TAKE THE CUP!" as wide as the frame allows
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
        // The leaderboard: the whole field in finishing order down the left, under the headline. Each line a card: the place
        // (the podium's three in their metal), the driver's face, the name, the time, and what the place pays.
        var boardRt = UI("Board", canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(120 + 470, -400 - 4 * 74), new Vector2(940, 8 * 74));
        var boardGroup = boardRt.gameObject.AddComponent<CanvasGroup>(); boardGroup.interactable = false; boardGroup.blocksRaycasts = false;
        var rowRts = new List<RectTransform>(); var rowPlace = new List<TMP_Text>(); var rowName = new List<TMP_Text>(); var rowTime = new List<TMP_Text>(); var rowPrize = new List<TMP_Text>();
        var rowFace = new List<UnityEngine.UI.Image>(); var rowBack = new List<UnityEngine.UI.Image>();
        for (int i = 0; i < 8; i++)
        {
            var row = UI("Row" + i, boardRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(470, -33 - i * 74), new Vector2(940, 66));
            row.gameObject.AddComponent<CanvasGroup>();
            var backImg = Design.AsCard(row, new Color(0.06f, 0.05f, 0.14f, 0.86f)); backImg.raycastTarget = false;
            Color placeColour = i < 3 ? tints[i] : Design.Paper;
            if (i < 3) { var bar = UI("Bar", row, new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 0), new Vector2(8, -18)); var bi = bar.gameObject.AddComponent<UnityEngine.UI.Image>(); bi.color = placeColour; bi.raycastTarget = false; }
            var place = Words("Place", row, Design.Display, (i + 1) + "TH", 46, placeColour, new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(120, 60)); place.alignment = TextAlignmentOptions.Left;
            var face = UI("Face", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(178, 0), new Vector2(56, 56));
            var faceImg = face.gameObject.AddComponent<UnityEngine.UI.Image>(); faceImg.preserveAspect = true; faceImg.raycastTarget = false;
            var nm = Words("Name", row, Design.Display, "", 42, Design.Paper, new Vector2(0, 0.5f), new Vector2(222, 0), new Vector2(390, 60)); nm.alignment = TextAlignmentOptions.Left;
            nm.enableAutoSizing = true; nm.fontSizeMin = 26; nm.fontSizeMax = 42;
            var time = Words("Time", row, Design.Strong, "", 36, Design.Paper, new Vector2(0, 0.5f), new Vector2(620, 0), new Vector2(170, 52)); time.alignment = TextAlignmentOptions.Right;
            var prize = Words("Prize", row, Design.Strong, "", 30, goldC, new Vector2(0, 0.5f), new Vector2(800, 0), new Vector2(124, 52)); prize.alignment = TextAlignmentOptions.Right;
            prize.enableAutoSizing = true; prize.fontSizeMin = 20; prize.fontSizeMax = 30;
            rowRts.Add(row); rowPlace.Add(place); rowName.Add(nm); rowTime.Add(time); rowPrize.Add(prize); rowFace.Add(faceImg); rowBack.Add(backImg);
        }
        boardRt.gameObject.SetActive(false);
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
        Fill(so.FindProperty("placeLights"), placeLights);
        Fill(so.FindProperty("placeShow"), placeShow);
        Fill(so.FindProperty("houseLights"), house.ToArray());
        Fill(so.FindProperty("winnerShow"), winnerShow.ToArray());
        Fill(so.FindProperty("movers"), movers.ToArray());
        so.FindProperty("roll").objectReferenceValue = roll;
        so.FindProperty("reveal").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/podium/reveal.wav");
        so.FindProperty("crash").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/podium/crash.wav");
        so.FindProperty("board").objectReferenceValue = boardGroup;
        Fill(so.FindProperty("rows"), rowRts.ToArray());
        Fill(so.FindProperty("rowPlace"), rowPlace.ToArray());
        Fill(so.FindProperty("rowName"), rowName.ToArray());
        Fill(so.FindProperty("rowTime"), rowTime.ToArray());
        Fill(so.FindProperty("rowPrize"), rowPrize.ToArray());
        Fill(so.FindProperty("rowFace"), rowFace.ToArray());
        Fill(so.FindProperty("rowBack"), rowBack.ToArray());
        Fill(so.FindProperty("faces"), Directory.GetFiles("Assets/_Game/Art/Textures/UI/Portraits", "*.png").Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(f => f != null).ToArray());
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
        // A cup with some weight to it: a stepped foot, a stout stem with a knop, a deep bowl with a rolled lip, and
        // ears thick enough to read from across the stage (the first one was a wine glass with wire handles).
        var profile = new[] {
            new Vector2(0.125f, 0f), new Vector2(0.125f, 0.028f), new Vector2(0.098f, 0.04f), new Vector2(0.098f, 0.062f),
            new Vector2(0.062f, 0.078f), new Vector2(0.036f, 0.105f), new Vector2(0.032f, 0.15f), new Vector2(0.058f, 0.168f),
            new Vector2(0.032f, 0.186f), new Vector2(0.042f, 0.215f), new Vector2(0.095f, 0.245f), new Vector2(0.152f, 0.305f),
            new Vector2(0.176f, 0.375f), new Vector2(0.184f, 0.455f), new Vector2(0.19f, 0.497f), new Vector2(0.2f, 0.505f),
            new Vector2(0.198f, 0.518f), new Vector2(0.182f, 0.52f), new Vector2(0.17f, 0.495f), new Vector2(0.162f, 0.45f),
            new Vector2(0f, 0.39f),
        };
        var b = new Builder();
        b.Lathe(profile, 48);
        foreach (float s in new[] { -1f, 1f })
        {
            var path = new List<Vector3>();
            for (int k = 0; k <= 18; k++)
            {
                float a = Mathf.Lerp(-108f, 96f, k / 18f) * Mathf.Deg2Rad;
                path.Add(new Vector3(s * (0.185f + Mathf.Cos(a) * 0.115f), 0.37f + Mathf.Sin(a) * 0.105f, 0f));
            }
            b.Tube(path, 0.024f, 12);
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
        Part(root, "Plinth", cube, plinth, new Vector3(0, 0.035f, 0), new Vector3(0.36f, 0.07f, 0.36f));
        Part(root, "Step", cube, plinth, new Vector3(0, 0.085f, 0), new Vector3(0.29f, 0.03f, 0.29f));
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
        // (lit is the fallback if the shader is missing)
        var polished = Shader.Find("KartCup/Polished");
        var m = Mat(name, polished != null ? polished : lit);
        if (polished == null) { m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 0.8f); return m; }
        Color.RGBToHSV(c, out float h, out float sat, out float v);
        m.SetColor("_Color", c); m.SetColor("_Shade", Color.HSVToRGB(h, Mathf.Min(1f, sat * 1.2f + 0.05f), v * 0.45f)); m.SetFloat("_Glint", 1.5f);
        return m;
    }

    /// A metal that takes the stage's lights (the trophies' Polished paints its own and would shine in the dark).
    static Material LitMetal(string name, Shader lit, Color c)
    {
        var m = Mat(name, lit);
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 0.85f); m.SetFloat("_Smoothness", 0.72f);
        return m;
    }

    /// A picture on a quad, unlit, its own alpha (the Kartel sign), a little past 1 so it blooms.
    static Material Sign(string name, string texture)
    {
        var m = Mat(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture)); m.SetColor("_BaseColor", new Color(1.35f, 1.35f, 1.35f, 1f));
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }

    /// A shaft of light (the lighthouse's beam shader), faded in a metre and a half from the camera instead of six.
    static Material Beam(string name, Color c, float strength = 0.4f)
    {
        var m = Mat(name, Shader.Find("KartCup/Beam") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_Color", c); m.SetFloat("_Strength", strength); m.SetFloat("_Falloff", 0.55f); m.SetFloat("_Edge", 1.6f); m.SetFloat("_Near", 1.5f);
        return m;
    }

    /// An open cone along +Z: `r0` at the lamp, widening at `halfAngle` over `length`.
    static Mesh Cone(float r0, float length, float halfAngle, int segs = 24)
    {
        float r1 = r0 + length * Mathf.Tan(halfAngle * Mathf.Deg2Rad);
        var v = new Vector3[segs * 2]; var nrm = new Vector3[segs * 2]; var uv = new Vector2[segs * 2]; var tris = new int[segs * 6];
        for (int i = 0; i < segs; i++)
        {
            float a = Mathf.PI * 2f * i / segs; var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            v[i] = radial * r0; v[segs + i] = radial * r1 + Vector3.forward * length;
            nrm[i] = nrm[segs + i] = radial;
            uv[i] = new Vector2(i / (float)segs, 0); uv[segs + i] = new Vector2(i / (float)segs, 1);
            int j = (i + 1) % segs, t = i * 6;
            tris[t] = i; tris[t + 1] = j; tris[t + 2] = segs + j; tris[t + 3] = i; tris[t + 4] = segs + j; tris[t + 5] = segs + i;
        }
        var mesh = new Mesh { vertices = v, normals = nrm, uv = uv, triangles = tris };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// Camera-facing (-Z) quads of `size` at each point, one mesh: the sign's bulbs in one draw.
    static Mesh Quads(List<Vector3> at, float size)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
        foreach (var p in at)
        {
            int k = v.Count; float h = size / 2f;
            v.Add(p + new Vector3(-h, -h, 0)); v.Add(p + new Vector3(h, -h, 0)); v.Add(p + new Vector3(-h, h, 0)); v.Add(p + new Vector3(h, h, 0));
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
            tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
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
        var board = new List<PodiumShow.Row>
        {
            new() { name = "Toly", face = "Toly", time = "1:01.22", prize = "+10 PTS" }, new() { name = "Mert", face = "Mert", time = "1:01.80", prize = "+8 PTS" },
            new() { name = "Bitsy", face = "Bitsy", time = "1:02.41", prize = "+6 PTS" }, new() { name = "Andrew", face = "Andrew", time = "1:03.05", prize = "+5 PTS", me = true },
            new() { name = "Akshay", face = "Akshay", time = "1:04.90", prize = "+4 PTS" }, new() { name = "Pip", face = "Pip", time = "RACING", prize = "+3 PTS" },
        };
        RenderSettings.sun = sun;                        // so the ceremony puts it out, as it does a race scene's
        float t0 = Time.unscaledTime;
        show.Winners("TOLY WINS!", top, () => board);
        foreach (var smr in stage.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkinnedMeshRenderer>())) smr.updateWhenOffscreen = true;
        // the ceremony's clock, a still at each beat: the dark and the roll, third's light, second's, the crash, after
        foreach (var (at, shot) in new[] { (0.95f, "podium-dark"), (1.75f, "podium-third"), (2.75f, "podium-second"), (4.4f, "podium-winners"), (7.2f, "podium-winners-late") })
        {
            foreach (var pose in Object.FindObjectsByType<PodiumPose>(FindObjectsSortMode.None)) pose.Apply(t0 + at);
            if (at >= PodiumShow.WinAt) { foreach (var p in show.GetComponentsInChildren<ParticleSystem>(true)) if (p.name == "Burst" || p.name == "Rain") p.Play(); Simulate(systems, 0.6f); }
            show.PoseCamera(at); Canvas.ForceUpdateCanvases(); Shot(cam, shot);
        }

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
