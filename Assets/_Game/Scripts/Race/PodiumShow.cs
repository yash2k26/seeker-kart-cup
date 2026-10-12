using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The two shots round a race. Before it: a few b-roll cuts of the circuit (the race camera off its rig: its
    /// critters, tunnel, landmark, a ramp, the shortcut; then a crane down behind the grid) with the words over it: what the race is for, the prize pool counting up and
    /// what each place takes. After it: the ceremony stage, the top three standing on their blocks holding their
    /// trophies (gold, biggest, to the winner, who is too excited to stand still), under confetti. A scene with no
    /// waypoints shows the trophies on the stage before the race instead, as it used to.
    ///
    /// One prefab (Resources/PodiumStage, built by PodiumSetup), hung high above the start line with its own camera:
    /// the same stage on every circuit, nothing of the track in shot, nothing of the scenery in the way. While a shot is
    /// up it takes over from the race camera and hides the race HUD; End() gives both back.
    public sealed class PodiumShow : MonoBehaviour
    {
        public static PodiumShow Instance { get; private set; }

        [SerializeField] Camera cam;
        [SerializeField] Transform[] spots = new Transform[3];      // where each place stands: 0 is the winner, centre
        [SerializeField] Transform[] trophies = new Transform[3];   // gold, silver, bronze, resting on their blocks
        // The words are a screen overlay on the stage's own camera, not text standing in the scene: a kicker and one
        // big gold line top left (the pot counts up into it), and a card per place along the bottom carrying who or
        // what that place gets. Everything is posed from the shot's clock, so a still at t is the animation at t.
        [SerializeField] TMP_Text title, subtitle;                  // the kicker, and the big line
        [SerializeField] TMP_Text[] plates = new TMP_Text[3];       // each card's main line: the prize, or the racer's name
        [SerializeField] TMP_Text[] cardNotes = new TMP_Text[3];    // each card's second line (the prize under a name)
        [SerializeField] RectTransform[] cards = new RectTransform[3];
        [SerializeField] CanvasGroup overlay;
        [SerializeField] TMP_Text hint;
        [SerializeField] ParticleSystem[] poppers = new ParticleSystem[0];
        [SerializeField] ParticleSystem rain;
        [SerializeField] AudioClip pop;
        /// Measured by PodiumSetup on each driver's own rig (every body model has its own proportions): its
        /// holding-both clip; where the gold sits over the head and where a trophy held out to either side sits, in the
        /// torso's space; and the arm turns from the hold pose to arms up in a V and to a trophy arm held out.
        [System.Serializable] public struct Rig
        {
            public string driver; public AnimationClip hold;
            public Vector3 overHead, sideRight, sideLeft;
            public Quaternion raiseLeft, raiseRight, outLeft, outRight;
        }
        [SerializeField] Rig[] rigs = new Rig[0];
        [SerializeField] float[] trophyScale = { 1f, 0.8f, 0.68f };
        // ---- the winners' ceremony, lights down (11 Oct 2026, the user: "make a drum roll, lights off ... the platform should
        // be better ... we can also have a leaderboard"). The stage starts dark under a drum roll while the leaderboard fills
        // from the back of the field; a spotlight slams on over third, then over second; on the roll's last stroke the crash:
        // the winner's light, every stage light up, the moving heads sweeping, the poppers and the winner's name.
        [SerializeField] Light[] placeLights = new Light[3];          // the spot over each block, winner first
        [SerializeField] GameObject[] placeShow = new GameObject[3];  // what comes on with it: its beam, the pool, the block's strips
        [SerializeField] Light[] houseLights = new Light[0];          // the key lights and the rim, up with the winner
        [SerializeField] GameObject[] winnerShow = new GameObject[0]; // the backdrop's sign and bulbs, the rings, the glitter, the heads
        [SerializeField] Transform[] movers = new Transform[0];       // the moving heads on the towers
        [SerializeField] AudioSource roll;                            // the drum roll on its own source, so a skip can cut it
        [SerializeField] AudioClip reveal, crash;
        [SerializeField] CanvasGroup board;                           // the leaderboard, left of the podium
        [SerializeField] RectTransform[] rows = new RectTransform[0];
        [SerializeField] TMP_Text[] rowPlace = new TMP_Text[0], rowName = new TMP_Text[0], rowTime = new TMP_Text[0], rowPrize = new TMP_Text[0];
        [SerializeField] UnityEngine.UI.Image[] rowFace = new UnityEngine.UI.Image[0], rowBack = new UnityEngine.UI.Image[0];
        [SerializeField] Sprite[] faces = new Sprite[0];              // the drivers' portraits, by name
        public const float Reveal3 = 1.5f, Reveal2 = 2.45f, WinAt = 3.6f;   // the drum roll's last stroke is WinAt
        /// A leaderboard line, in finishing order: who, the face to show (a driver's name), the time, what the place pays.
        public struct Row { public string name, face, time, prize; public bool me; }
        // The racers stand taller on the podium than in their karts, and the trophies are made to be seen from a phone.
        [SerializeField] float driverScale = 1.7f, trophySize = 2.1f;
        const float TrophyHeight = 0.62f;                 // plinth and cup at scale 1 (PodiumSetup.CupMesh)

        public bool Active { get; private set; }
        public const float PrizeSeconds = 4.5f;       // the pre-race shot on the stage (online it runs until 3-2-1)
        /// How long the pre-race shot wants: the b-roll and a beat behind the grid, or the stage's reveal.
        public float IntroSeconds => flying ? flySeconds + 0.7f : PrizeSeconds;
        public const float CeremonySeconds = 9f;      // the podium before the results card comes up: the reveal, then a few seconds of it

        Camera raceCam;
        // the drone's lap: the race camera taken off its rig, and the stage put away meanwhile
        bool flying;
        float flySeconds;
        Vector3[] path;
        PowerslideKartPhysics.KartCamera rig;
        Vector3 rigPos; Quaternion rigRot;
        float flightFrom; int flightFrame;      // the lap is also this phone's frame-rate test (RenderBudget)
        readonly List<GameObject> hiddenStage = new();
        readonly List<GameObject> hiddenHud = new();
        readonly List<GameObject> spawned = new();
        Vector3[] trophyHome;
        Transform[] trophyParent;
        float shotAt;
        bool winnersShot;
        Vector3 focus;

        public struct Winner { public GameObject driver; public string name; public string prize; }
        readonly bool[] present = new bool[3];
        System.Func<IList<Row>> boardRows;
        IList<Row> shownRows;
        float nextRows;
        int cue;
        float[] placeBase, houseBase, rowX;

        // the big line, when it is an amount ("0.54 SOL"): counted up from nothing
        float amount; string amountUnit, amountFormat, bigText;
        float cardsFrom = 0.9f;

        /// The stage for this race scene, made on first use.
        public static PodiumShow Ensure()
        {
            if (Instance != null) return Instance;
            var prefab = Resources.Load<GameObject>("PodiumStage");
            if (prefab == null) { Debug.LogWarning("[podium] no Resources/PodiumStage"); return null; }
            Vector3 at = Vector3.zero;
            foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)) if (cp.index == 0) at = cp.transform.position;
            // Well above the start line: sky all round, and far past any hill, tree or tower of the circuit below.
            var go = Instantiate(prefab, at + Vector3.up * 400f, Quaternion.identity);
            go.name = "PodiumStage";
            return go.GetComponent<PodiumShow>();
        }

        void Awake() { Instance = this; if (cam != null) cam.enabled = false; Init(); }
        Color gold = new(1f, 0.78f, 0.23f);

        /// Where each trophy rests on its block, remembered before any racer picks one up. (Lazy, so an edit-mode
        /// preview, where Awake never runs, gets it too.)
        void Init()
        {
            if (subtitle != null && subtitle.color.a > 0f && subtitle.color != Color.white) gold = subtitle.color;
            if (trophyHome != null) return;
            trophyHome = new Vector3[trophies.Length];
            trophyParent = new Transform[trophies.Length];
            for (int i = 0; i < trophies.Length; i++) if (trophies[i] != null) { trophyHome[i] = trophies[i].localPosition; trophyParent[i] = trophies[i].parent; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; End(); }

        // ---------- the two shots ----------

        /// Before the race: the trophies on their blocks and what each place wins. `places` are the plate lines,
        /// winner first (e.g. "1ST · 0.0195 SOL"); a place with no prize says so, or nothing.
        public void Prizes(string heading, string amount, IList<string> places)
        {
            Init();
            flying = FlightPath();
            Begin();
            winnersShot = false;
            boardRows = null; shownRows = null;
            if (board != null) board.gameObject.SetActive(false);
            ClearDrivers();
            for (int i = 0; i < trophies.Length; i++) ResetTrophy(i);
            if (title != null) title.text = heading;
            Big(amount);
            // a card per place: "1ST" and what it pays (or, with no money on the race, the metal)
            string[] metal = { "GOLD", "SILVER", "BRONZE" };
            for (int i = 0; i < plates.Length; i++)
            {
                var line = places != null && i < places.Count ? places[i] : "";
                int cut = line.IndexOf('\n');
                Card(i, cut >= 0 ? line[(cut + 1)..].Trim() : flying ? "" : metal[Mathf.Min(i, 2)], "");   // no trophies in shot from the drone: a card only where there is money on the place
            }
            cardsFrom = 1.5f;
            if (flying) return;
            focus = trophies.Length > 0 && trophies[0] != null ? trophies[0].position + Vector3.up * TrophyHeight * Size(0) * 0.55f : transform.position + Vector3.up * 2f;
            Pop();
        }

        /// After the race: the top three, winner first, on the podium with their trophies. Fewer than three racers
        /// leaves the empty places' trophies on their blocks.
        public void Winners(string heading, IList<Winner> top, System.Func<IList<Row>> board = null)
        {
            Init();
            Land();
            Begin();
            winnersShot = true;
            ClearDrivers();
            if (title != null) title.text = "RACE OVER";
            Big(heading);
            amountUnit = null;                               // the winner's name is said, never counted up
            cardsFrom = 0.8f;
            cue = 0;
            boardRows = board; shownRows = null; nextRows = 0f;
            LightsDown();
            if (roll != null) { roll.Stop(); roll.Play(); }
            AudioDirector.Instance?.Duck(WinAt + 0.6f);
            for (int i = 0; i < spots.Length && i < trophies.Length; i++)
            {
                ResetTrophy(i);
                bool there = top != null && i < top.Count && top[i].driver != null;
                present[i] = there;
                Card(i, "", "");                             // the leaderboard says who and what, not the cards
                if (!there) continue;
                var d = Instantiate(top[i].driver, spots[i].position, spots[i].rotation);
                d.name = "Podium_" + top[i].name;
                d.transform.localScale *= driverScale;
                spawned.Add(d);
                var rig = RigFor(top[i].driver.name);
                var torso = Stand(d, rig.hold);
                var holder = d.AddComponent<PodiumPose>();
                holder.Init(i, rig, Time.unscaledTime + RevealAt(i));   // still in the dark until their light comes on
                if (torso != null && trophies[i] != null)
                {
                    var t = trophies[i];
                    t.SetParent(torso, false);
                    t.localRotation = Quaternion.identity;
                    float s = trophyScale.Length > i ? trophyScale[i] : 1f;
                    t.localScale = Vector3.one * s * trophySize / Mathf.Max(0.0001f, torso.lossyScale.x);
                    // The winner holds the gold up over the head; second and third hold theirs up beside it, on the side
                    // away from the winner (second stands on the camera's left, which is the driver's own right).
                    t.localPosition = i == 0 ? rig.overHead : i == 1 ? rig.sideRight : rig.sideLeft;
                }
            }
            focus = spots.Length > 0 && spots[0] != null ? spots[0].position + Vector3.up * 1.4f : transform.position;
            if (rain != null) rain.Clear();
            RefreshBoard();
            PoseStage(0f);
        }

        /// When place `i` (0 the winner) is called.
        static float RevealAt(int i) => i == 0 ? WinAt : i == 1 ? Reveal2 : Reveal3;

        /// A tap during the drum roll jumps to the winner; after it, the caller moves on. True if it jumped.
        public bool SkipToWinner()
        {
            if (!Active || !winnersShot || Time.unscaledTime - shotAt >= WinAt - 0.05f) return false;
            shotAt = Time.unscaledTime - WinAt;
            cue = 2;                                         // straight to the crash: no stage-light hits stacked on it
            foreach (var p in FindObjectsByType<PodiumPose>(FindObjectsSortMode.None)) p.Wake();
            return true;
        }

        /// Any tap on the screen (skips a shot).
        public static bool Tapped() => UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame;

        /// The results card is up over the podium: the stage, the drivers and the confetti stay, the words go (they sat on
        /// top of the card, on their own canvas nearer the camera).
        public void HideWords() { if (overlay != null) overlay.gameObject.SetActive(false); }

        /// Back to the race camera and HUD.
        public void End()
        {
            if (!Active) return;
            Active = false;
            Land();
            if (cam != null) cam.enabled = false;
            if (overlay != null) overlay.gameObject.SetActive(false);     // (a camera canvas with its camera off draws as an overlay)
            if (raceCam != null) raceCam.enabled = true;
            foreach (var go in hiddenHud) if (go != null) go.SetActive(true);
            hiddenHud.Clear();
            if (rain != null) rain.Stop();
            if (roll != null) roll.Stop();
            LightsBack();
        }

        // ---------- camera ----------

        void LateUpdate()
        {
            if (!Active) return;
            float t = Time.unscaledTime - shotAt;
            Cues(t);
            if (boardRows != null && Time.unscaledTime >= nextRows) RefreshBoard();
            PoseCamera(t);
        }

        /// The ceremony's sounds and bursts, each once, on the shot's clock.
        void Cues(float t)
        {
            if (!winnersShot) return;
            if (cue < 1 && t >= Reveal3) { cue = 1; if (present[2]) Hit(); }
            if (cue < 2 && t >= Reveal2) { cue = 2; if (present[1]) Hit(); }
            if (cue < 3 && t >= WinAt)
            {
                cue = 3;
                if (roll != null) roll.Stop();
                if (crash != null) AudioDirector.Instance?.Play(crash, 1f);
                Pop();
                if (rain != null) { rain.Clear(); rain.Play(); }
            }
        }

        void Hit()
        {
            if (reveal != null) AudioDirector.Instance?.Play(reveal, 0.9f, Random.Range(0.97f, 1.03f));
            Haptics.Play(Haptics.Kind.Tick);
        }

        // ---------- the lights ----------

        /// A light slamming on: nothing before `s` = 0, then on, a stutter, and on for good.
        static float Slam(float s) => s < 0f ? 0f : s < 0.06f ? 1f : s < 0.11f ? 0.12f : 1f;

        /// Every stage light at `t` into the shot. The pre-race shot is lit all through.
        void PoseStage(float t)
        {
            placeBase ??= System.Array.ConvertAll(placeLights, l => l != null ? l.intensity : 0f);
            houseBase ??= System.Array.ConvertAll(houseLights, l => l != null ? l.intensity : 0f);
            for (int i = 0; i < placeLights.Length; i++)
            {
                float on = !winnersShot ? 1f : present[i] ? Slam(t - RevealAt(i)) : 0f;
                if (placeLights[i] != null) placeLights[i].intensity = placeBase[i] * on;
                if (i < placeShow.Length && placeShow[i] != null) placeShow[i].SetActive(on > 0.5f);
            }
            float house = !winnersShot ? 1f : Mathf.Clamp01((t - WinAt) / 0.35f);
            for (int i = 0; i < houseLights.Length; i++) if (houseLights[i] != null) houseLights[i].intensity = houseBase[i] * house;
            foreach (var go in winnerShow) if (go != null) go.SetActive(house > 0f);
            for (int k = 0; k < movers.Length; k++)          // the heads sweep the stage and the roof
                if (movers[k] != null) movers[k].localRotation = Quaternion.Euler(28f + Mathf.Sin(t * 1.3f + k * 2.1f) * 22f, (k == 0 ? 25f : -25f) + Mathf.Sin(t * 0.9f + k) * 40f, 0f);
            if (dimmed)
            {
                // the house in the dark: barely there, then the stage's own violet once the winner's light is on
                float lit = Mathf.Max(house, 0.18f * (Slam(t - Reveal3) + Slam(t - Reveal2)) * 0.5f);
                RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.03f, 0.03f, 0.06f), new Color(0.32f, 0.28f, 0.48f), lit);
                RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.02f, 0.02f, 0.04f), new Color(0.24f, 0.20f, 0.34f), lit);
                RenderSettings.ambientGroundColor = Color.Lerp(Color.black, new Color(0.10f, 0.08f, 0.12f), lit);
            }
        }

        // the race scene's own light, put away while the stage plays and given back after
        bool dimmed; UnityEngine.Rendering.AmbientMode ambMode; Color ambSky, ambEq, ambGround; float reflections; bool fogOn; Light sun; bool sunOn;
        void LightsDown()
        {
            if (dimmed) return;
            dimmed = true;
            ambMode = RenderSettings.ambientMode; ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGround = RenderSettings.ambientGroundColor;
            reflections = RenderSettings.reflectionIntensity; fogOn = RenderSettings.fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.reflectionIntensity = 0.15f; RenderSettings.fog = false;
            sun = RenderSettings.sun; sunOn = sun != null && sun.enabled;
            if (sun != null) sun.enabled = false;
        }
        void LightsBack()
        {
            if (!dimmed) return;
            dimmed = false;
            RenderSettings.ambientMode = ambMode; RenderSettings.ambientSkyColor = ambSky; RenderSettings.ambientEquatorColor = ambEq; RenderSettings.ambientGroundColor = ambGround;
            RenderSettings.reflectionIntensity = reflections; RenderSettings.fog = fogOn;
            if (sun != null) sun.enabled = sunOn;
        }

        // ---------- the leaderboard ----------

        /// The finishing order as it stands: the field below is still racing, so the times fill in as they cross the line.
        void RefreshBoard()
        {
            nextRows = Time.unscaledTime + 0.5f;
            shownRows = boardRows?.Invoke();
            if (board != null) board.gameObject.SetActive(winnersShot && shownRows != null && shownRows.Count > 0);
            if (shownRows == null) return;
            string[] ord = { "1ST", "2ND", "3RD" };
            for (int i = 0; i < rows.Length; i++)
            {
                bool on = i < shownRows.Count;
                if (rows[i] != null) rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var r = shownRows[i];
                if (i < rowPlace.Length && rowPlace[i] != null) rowPlace[i].text = i < 3 ? ord[i] : (i + 1) + "TH";
                if (i < rowName.Length && rowName[i] != null) rowName[i].text = (r.name ?? "").ToUpperInvariant();
                if (i < rowTime.Length && rowTime[i] != null) rowTime[i].text = r.time ?? "";
                if (i < rowPrize.Length && rowPrize[i] != null) rowPrize[i].text = r.prize ?? "";
                if (i < rowFace.Length && rowFace[i] != null)
                {
                    Sprite face = null; foreach (var f in faces) if (f != null && f.name == r.face) face = f;
                    rowFace[i].sprite = face; rowFace[i].enabled = face != null;
                }
                if (i < rowBack.Length && rowBack[i] != null) rowBack[i].color = r.me ? new Color(0.26f, 0.16f, 0.52f, 0.96f) : new Color(0.06f, 0.05f, 0.14f, 0.86f);
            }
        }

        /// Each line slides in when its place is called: the back of the field during the roll, then third, second, first.
        void PoseBoard(float t)
        {
            if (board == null || shownRows == null) return;
            rowX ??= System.Array.ConvertAll(rows, r => r != null ? r.anchoredPosition.x : 0f);   // where each line rests
            int n = shownRows.Count;
            for (int i = 0; i < rows.Length && i < n; i++)
            {
                if (rows[i] == null) continue;
                float at = i < 3 ? RevealAt(i) : Mathf.Min(0.35f + (n - 1 - i) * 0.28f, Reveal3 - 0.2f);
                float k = Ease(Mathf.Clamp01((t - at) / 0.3f));
                var cg = rows[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = k;
                var p = rows[i].anchoredPosition; rows[i].anchoredPosition = new Vector2(rowX[i] - 80f * (1f - k), p.y);
                rows[i].localScale = Vector3.one * (i == 0 && t >= WinAt ? Mathf.Max(1f, Pop(t, WinAt, 0.4f)) : 1f);
            }
        }

        /// The camera `t` seconds into the current shot.
        public void PoseCamera(float t)
        {
            PoseOverlay(t);
            PoseStage(t);
            PoseBoard(t);
            if (flying) { Fly(t); return; }
            if (cam == null) return;
            var stage = transform;
            // the words own the left of the frame: the camera stands off to that side, so the podium sits right of centre
            var aside = stage.right * -2.3f;
            if (!winnersShot)
            {
                // Start close on the gold trophy, pull back to the whole podium and the title: the reveal.
                float k = Ease(Mathf.Clamp01((t - 0.3f) / 3.2f));
                var near = focus + stage.forward * -3.3f + Vector3.up * 0.3f;
                var far = stage.position + stage.forward * -11.5f + Vector3.up * 3.2f;
                var lookNear = focus;
                var lookFar = stage.position + Vector3.up * 2.4f;
                cam.transform.position = Vector3.Lerp(near, far, k) + stage.right * Mathf.Sin(t * 0.4f) * 0.6f + aside * k;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(lookNear, lookFar, k) + aside * k - cam.transform.position, Vector3.up);
            }
            else
            {
                // In the dark: wide and low, creeping in on the podium while the roll builds. On the crash a push in on the
                // winner, then a slow drift from side to side. The leaderboard owns the left: the podium sits right of it.
                aside = stage.right * -3.6f;
                float creep = Mathf.Clamp01(t / WinAt), push = Ease(Mathf.Clamp01((t - WinAt) / 0.7f));
                float orbit = Mathf.Sin(Mathf.Max(0f, t - WinAt) * 0.35f) * 9f;
                var dir = Quaternion.AngleAxis(orbit, Vector3.up) * -stage.forward;
                float dist = Mathf.Lerp(Mathf.Lerp(14f, 12.5f, creep), 10.2f, push);
                var pos = stage.position + dir * dist + Vector3.up * Mathf.Lerp(2.2f, 3.1f, push) + aside;
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(stage.position + Vector3.up * 2.8f, focus + Vector3.up * 0.8f, push * 0.5f) + aside - pos, Vector3.up);
            }
        }

        static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        // ---------- the b-roll ----------
        // 13 Oct 2026, the user: "instead of showing the whole track shot, just show some broll shots of the tracks, like
        // underpass, the chickens, and for each track we should get different shots". So no lap: a few low cuts of what
        // this circuit has (its critters, the tunnel, its landmark, a ramp or a turbo pad, the shortcut's mouth), picked
        // afresh each race, then a crane down onto the grid that hands over to the chase camera.

        struct Shot { public string name; public float seconds; public System.Func<float, (Vector3 pos, Vector3 look)> pose; }
        readonly List<Shot> shots = new();
        const float ShotSeconds = 1.4f, GridSeconds = 1.6f;
        const int MaxBroll = 4;
        int cutLogged = -1;

        /// The shots for this circuit; false when the scene has no waypoints (the stage shot then plays instead).
        bool FlightPath()
        {
            var root = GameObject.Find("Waypoints");
            if (root == null || root.transform.childCount < 8 || Camera.main == null || Camera.main == cam) return false;
            path = new Vector3[root.transform.childCount];
            for (int i = 0; i < path.Length; i++) path[i] = root.transform.GetChild(i).position;
            var rng = new System.Random(System.Environment.TickCount);
            shots.Clear(); cutLogged = -1;
            var picks = new List<Shot>();
            void Add(Shot? s) { if (s is Shot v) picks.Add(v); }
            Add(CritterShot(rng));
            Add(TunnelShot());
            foreach (var s in LandmarkShots(rng)) Add(s);
            if (RampOrPad("Roadworks", rng, ramp: true) is Shot works) picks.Add(new Shot { name = "roadworks", seconds = works.seconds, pose = works.pose });   // the series' slalom, from its run-up
            var hazards = new List<Shot>();
            if (RampOrPad("Ramps", rng, ramp: true) is Shot r) hazards.Add(r);
            if (RampOrPad("TurboPads", rng, ramp: false) is Shot t) hazards.Add(t);
            if (hazards.Count > 0) picks.Add(hazards[rng.Next(hazards.Count)]);
            Add(ShortcutShot());
            for (int i = 0; i < picks.Count && shots.Count < MaxBroll; i++) shots.Add(picks[i]);
            if (shots.Count > 0) { var first = shots[0]; first.seconds += 0.6f; shots[0] = first; }   // the scene is still settling under the first cut
            shots.Add(GridShot());
            flySeconds = 0f; foreach (var s in shots) flySeconds += s.seconds;
            Debug.Log($"[intro] b-roll on {Cup.TrackId}: {string.Join(", ", shots.ConvertAll(s => s.name))} ({flySeconds:0.0} s)");
            return true;
        }

        /// The race camera `t` seconds into the intro: the shot running at t, eased along its move.
        void Fly(float t)
        {
            if (raceCam == null || shots.Count == 0) return;
            int i = 0; float from = 0f;
            while (i < shots.Count - 1 && t >= from + shots[i].seconds) { from += shots[i].seconds; i++; }
            if (i != cutLogged) { cutLogged = i; Debug.Log($"[intro] cut {i}: {shots[i].name} at {t:0.0} s (scene {Time.timeSinceLevelLoad:0.0} s)"); }
            float u = Mathf.Clamp01((t - from) / shots[i].seconds);
            u = u * u * (3f - 2f * u);
            var (pos, look) = shots[i].pose(u);
            raceCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        }

        int Nearest(Vector3 p)
        {
            int best = 0; float d = float.MaxValue;
            for (int i = 0; i < path.Length; i++) { float e = (path[i] - p).sqrMagnitude; if (e < d) { d = e; best = i; } }
            return best;
        }

        /// The way the race runs at waypoint i, flat.
        Vector3 Road(int i) { var d = path[(i + 1) % path.Length] - path[i]; d.y = 0f; return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward; }

        /// A shot is only kept when its camera is in the open and nothing stands close in front of it (a hill, a rock,
        /// a wall); what is out by the subject (its own collider, the ground it stands on, the cone's slope) is the shot.
        static string Blocked(Vector3 eye, Vector3 at, Transform subject)
        {
            var inside = Physics.OverlapSphere(eye, 0.4f, ~0, QueryTriggerInteraction.Ignore);
            if (inside.Length > 0) return "inside " + inside[0].name;
            if (!Physics.Linecast(eye, at, out var hit, ~0, QueryTriggerInteraction.Ignore)) return null;
            if (subject != null && hit.transform.IsChildOf(subject) || hit.distance >= 0.6f * Vector3.Distance(eye, at)) return null;
            return $"{hit.collider.name} at {hit.distance:0} m";
        }

        /// An item box's bubble near the camera fills the frame: such a spot is no good.
        static bool Bubble(Vector3 from, Vector3 to, Vector3 at)
        {
            foreach (var c in Physics.OverlapCapsule(from, to, 3.5f, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<PowerslideKartPhysics.ItemGiver>() != null) return true;
            foreach (var c in Physics.OverlapCapsule(to, to + (at - to).normalized * 12f, 2.5f, ~0, QueryTriggerInteraction.Collide))
                if (c.GetComponentInParent<PowerslideKartPhysics.ItemGiver>() != null) return true;
            return false;
        }

        static Shot? Make(string name, Vector3 from, Vector3 to, System.Func<Vector3> look, Transform subject, bool quiet = false, float seconds = ShotSeconds)
        {
            var a = look();
            var why = Blocked(from, a, subject) ?? Blocked(to, a, subject) ?? (Bubble(from, to, a) ? "an item bubble" : null);
            if (why != null) { if (!quiet) Debug.Log($"[intro] {name}: blocked ({why}), skipped"); return null; }
            return new Shot { name = name, seconds = seconds, pose = u => (Vector3.Lerp(from, to, u), look()) };
        }

        /// One of the critters, from down at its height on the road, the camera panning with it as it crosses.
        Shot? CritterShot(System.Random rng)
        {
            var live = Critter.All.FindAll(c => c != null && c.isActiveAndEnabled);
            if (live.Count == 0) return null;
            // ponytail: the camera rides with the animal at its height, pushing in, facing it (penguins and ducks are dark
            // from behind); which way it faces is taken when the cut starts, so a turn at the kerb does not swing the camera.
            // Checked from where it stands now (it walks ~2 m in the shot); from the road's line when the front is blocked.
            for (int k = 0; k < live.Count; k++)
            {
                var c = live[(k + rng.Next(live.Count)) % live.Count];
                var t = c.transform; var road = Road(Nearest(t.position));
                foreach (bool front in new[] { true, false })
                {
                    Vector3 Axis() { var f = front ? Face(t) : -road; f.y = 0f; return f.sqrMagnitude > 0.01f ? f.normalized : -road; }
                    Vector3 Eye(float u, Vector3 f) => t.position + f * Mathf.Lerp(5.4f, 4.0f, u) + Vector3.Cross(Vector3.up, f) * 1.3f + Vector3.up * 0.7f;
                    var now = Axis();
                    if (Make("critter", Eye(0f, now), Eye(1f, now), () => t.position + Vector3.up * 0.4f, t, quiet: true) is not Shot) continue;
                    Vector3 axis = Vector3.zero;
                    return new Shot { name = "critter", seconds = ShotSeconds, pose = u =>
                    {
                        if (axis == Vector3.zero || u <= 0f) axis = Axis();
                        return (Eye(u, axis), t.position + Vector3.up * 0.4f);
                    } };
                }
            }
            Debug.Log("[intro] critter: no clear view, skipped");
            return null;
        }

        /// Which way an animal's face looks: toward its eyes (ChickenSetup's boxes), whichever way its root is turned.
        static Vector3 Face(Transform t)
        {
            var eyes = Vector3.zero; int n = 0;
            foreach (var c in t.GetComponentsInChildren<Transform>()) if (c.name.StartsWith("Eye")) { eyes += c.position; n++; }
            if (n == 0) return t.forward;
            var f = eyes / n - t.position; f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : t.forward;
        }

        /// Palm Bay's tunnel: down the tube at kart height, under its strip lights.
        Shot? TunnelShot()
        {
            var a = GameObject.Find("TunnelLight0"); var b = GameObject.Find("TunnelLight7");
            if (a == null || b == null) return null;
            Vector3 p0 = a.transform.position, p1 = b.transform.position;   // 6 m and 90 m into the tube, at its crown
            if (Vector3.Dot(p1 - p0, Road(Nearest((p0 + p1) * 0.5f))) < 0f) (p0, p1) = (p1, p0);   // the way the race runs
            var down = Vector3.up * -6.8f;                                // the crown is ~9 m over the road: kart height plus a bit
            foreach (float k in new[] { 0.02f, 0.25f, 0.45f })            // the first stretch of the tube clear of item bubbles
                if (Make("tunnel", Vector3.Lerp(p0, p1, k) + down, Vector3.Lerp(p0, p1, k + 0.28f) + down, () => p1 + down + Vector3.up * 0.6f, null, quiet: true) is Shot shot) return shot;
            Debug.Log("[intro] tunnel: no clear stretch, skipped");
            return null;
        }

        /// Each circuit's own sight, filmed from the road side.
        IEnumerable<Shot?> LandmarkShots(System.Random rng)
        {
            switch (Cup.TrackId)
            {
                case "beach":
                {
                    var lh = GameObject.Find("Dressing/Lighthouse");
                    if (lh == null) break;
                    var p = lh.transform.position; Shot? shot = null;
                    foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f })   // round from the circuit's side until the rocks are out of the way
                        foreach (float far in new[] { 64f, 90f })
                        {
                            if (shot != null) break;
                            var back = Quaternion.AngleAxis(turn, Vector3.up) * -new Vector3(p.x, 0f, p.z).normalized;
                            var side = Vector3.Cross(Vector3.up, back);
                            shot = Make("lighthouse", p + back * far + side * 14f + Vector3.up * 6f, p + back * (far - 8f) - side * 10f + Vector3.up * 8f, () => p + Vector3.up * 17f, lh.transform, quiet: true);
                        }
                    if (shot == null) Debug.Log("[intro] lighthouse: no clear view, skipped");
                    yield return shot;
                    break;
                }
                case "alpine": yield return Beside(All("train-locomotive", rng), 11f, 2.0f, 1.6f); break;
                case "forest": yield return Beside(All("campfire_logs", rng), 7.5f, 1.2f, 0.6f); break;
                case "speedway":
                {
                    var cars = new List<Transform>();                       // the paddock's race cars (the stands show their backs to the road)
                    foreach (var c in new[] { "raceCarRed", "raceCarGreen", "raceCarOrange", "raceCarWhite" }) cars.AddRange(All(c, rng));
                    yield return Beside(cars, 7f, 1.1f, 0.6f);
                    foreach (var arch in All("overheadRound", rng)) if (Toward(arch, "arch") is Shot through) { yield return through; break; }
                    break;
                }
                case "volcano":
                {
                    yield return Beside(All("statue_head", rng), 15f, 1.0f, 5f);
                    // the cone itself: from the road, looking up at the crater's rim
                    int w = rng.Next(path.Length); var at = new Vector3(0f, 58f, 0f);
                    var eye = path[w] + Vector3.up * 6f; var towards = (at - eye); towards.y = 0f;
                    yield return Make("crater", eye, eye + towards.normalized * 16f + Vector3.up * 3f, () => at + Vector3.up * -6f, null);
                    break;
                }
            }
        }

        /// The dressing's props by name (they repeat: the kit's names), in a random order.
        static List<Transform> All(string name, System.Random rng)
        {
            var found = new List<Transform>();
            var dressing = GameObject.Find("Dressing");
            if (dressing != null) foreach (var t in dressing.GetComponentsInChildren<Transform>()) if (t.name == name) found.Add(t);
            for (int i = found.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (found[i], found[j]) = (found[j], found[i]); }
            return found;
        }


        /// A prop off the road, the camera sliding past it: the first of them, and the first spot (road side, nearer,
        /// or the far side) with nothing in the way.
        Shot? Beside(List<Transform> subjects, float away, float height, float lookUp)
        {
            foreach (var subject in subjects)
            {
                var p = subject.position; int w = Nearest(p); var dir = Road(w);
                var toRoad = path[w] - p; toRoad.y = 0f; toRoad = toRoad.normalized;
                foreach (float out_ in new[] { away, away * 0.6f, -away * 0.8f })
                {
                    var eye = p + toRoad * out_; eye.y = Mathf.Max(p.y, path[w].y - 0.5f) + height;
                    if (Make(subject.name, eye - dir * 4f, eye + dir * 4f, () => p + Vector3.up * lookUp, subject, quiet: true) is Shot shot) return shot;
                }
            }
            if (subjects.Count > 0) Debug.Log($"[intro] {subjects[0].name}: no clear view, skipped");
            return null;
        }

        /// A tall prop beside the road (a grandstand) from the far side of the tarmac, rolling past it, aimed at its middle.
        Shot? FromRoad(List<Transform> subjects, float height)
        {
            foreach (var subject in subjects)
            {
                var b = new Bounds(subject.position, Vector3.zero);
                foreach (var r in subject.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                int w = Nearest(b.center); var dir = Road(w);
                var across = path[w] - b.center; across.y = 0f; across = across.normalized;
                var eye = path[w] + across * 6f + Vector3.up * (height - 0.5f);
                var at = b.center;
                if (Make(subject.name, eye - dir * 8f, eye + dir * 4f, () => at, subject, quiet: true) is Shot shot) return shot;
            }
            if (subjects.Count > 0) Debug.Log($"[intro] {subjects[0].name}: no clear view, skipped");
            return null;
        }

        /// Up the road through something it passes under (the Speedway's arches).
        Shot? Toward(Transform subject, string name)
        {
            if (subject == null) return null;
            var p = subject.position; int w = Nearest(p); var dir = Road(w);
            var road = path[w].y - 0.5f;
            var at = new Vector3(p.x, road + 4f, p.z);
            return Make(name, at - dir * 46f + Vector3.up * -3f, at - dir * 30f + Vector3.up * -2.8f, () => at + dir * 30f, subject, quiet: true);
        }

        /// A ramp from the run-up, or a turbo pad skimmed at kart height.
        Shot? RampOrPad(string rootName, System.Random rng, bool ramp)
        {
            var root = GameObject.Find(rootName);
            if (root == null || root.transform.childCount == 0) return null;
            var t = root.transform.GetChild(rng.Next(root.transform.childCount));
            var p = t.position; var dir = Road(Nearest(p));
            return ramp
                ? Make("ramp", p - dir * 17f + Vector3.up * 0.9f, p - dir * 8f + Vector3.up * 1.3f, () => p + dir * 3f + Vector3.up * 0.8f, t)
                : Make("turbo", p - dir * 10f + Vector3.up * 0.7f, p + dir * 2f + Vector3.up * 0.7f, () => p + dir * 14f + Vector3.up * 0.5f, t);
        }

        /// Beach Buggy hints at its shortcuts in the intro: the camera at the cut's mouth, looking in.
        Shot? ShortcutShot()
        {
            var cut = ShortcutPath.Instance;
            if (cut == null || cut.path == null || cut.path.Length < 3) return null;
            var a = cut.path[0].transform.position; var b = cut.path[2].transform.position;
            var dir = b - a; dir.y = 0f; dir = dir.normalized;
            return Make("shortcut", a - dir * 14f + Vector3.up * 1.8f, a - dir * 5f + Vector3.up * 1.6f, () => b + Vector3.up * 1f, cut.transform);
        }

        /// The finale: a crane down behind the grid, ending where the chase camera takes over.
        Shot GridShot()
        {
            var line = path[0]; var dir = Road(0);
            var from = line - dir * 62f + Vector3.up * 11f; var to = line - dir * 38f + Vector3.up * 5f;
            return new Shot { name = "grid", seconds = GridSeconds, pose = u => (Vector3.Lerp(from, to, u), line + Vector3.up * 1.2f) };
        }

        /// Takes the race camera off its rig for the lap, puts the stage away and hangs the words on that camera.
        void TakeOff()
        {
            rig = raceCam.GetComponentInParent<PowerslideKartPhysics.KartCamera>();
            if (rig != null) rig.enabled = false;
            rigPos = raceCam.transform.localPosition; rigRot = raceCam.transform.localRotation;
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf && (cam == null || child != cam.transform) && (overlay == null || child != overlay.transform))
                { hiddenStage.Add(child.gameObject); child.gameObject.SetActive(false); }
            // The words go straight onto the screen for the lap. On the stage's camera they are drawn into the scene
            // before the grade, which is fine against the stage's black; over a bright circuit the scrim behind them
            // all but vanished (dark over HDR daylight is still daylight once it is tone-mapped).
            if (overlay != null && overlay.TryGetComponent<Canvas>(out var canvas)) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            flightFrom = Time.realtimeSinceStartup; flightFrame = Time.frameCount;
            Letterbox.Show(true);                                        // cinema bars over the lap (Beach Buggy's fly-through)
        }

        /// Gives the camera back to its rig and brings the stage back.
        void Land()
        {
            if (!flying) return;
            flying = false;
            float flown = Time.realtimeSinceStartup - flightFrom;
            if (flown > 2.5f) RenderBudget.Report((Time.frameCount - flightFrame) / flown);   // (a tap that skips it at once proves nothing)
            // no cut: from the drone's last shot the camera eases down onto the chase position while 3-2-1 runs
            Vector3 fromPos = raceCam != null ? raceCam.transform.position : Vector3.zero; Quaternion fromRot = raceCam != null ? raceCam.transform.rotation : Quaternion.identity;
            if (raceCam != null) raceCam.transform.SetLocalPositionAndRotation(rigPos, rigRot);
            if (rig != null) { rig.enabled = true; CameraBlend.From(raceCam, fromPos, fromRot, 0.9f); }
            Letterbox.Show(false);
            foreach (var go in hiddenStage) if (go != null) go.SetActive(true);
            hiddenStage.Clear();
            if (overlay != null && overlay.TryGetComponent<Canvas>(out var canvas)) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 0.6f; }
        }

        // ---------- the overlay ----------

        /// The big line: an amount is remembered as a number to count up to; anything else is just said.
        void Big(string text)
        {
            bigText = text ?? ""; amountUnit = null;
            var m = System.Text.RegularExpressions.Regex.Match(bigText, @"^\s*([0-9]+(?:\.([0-9]+))?)\s+(\S+)");
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out amount))
            { amountUnit = m.Groups[3].Value; amountFormat = "F" + m.Groups[2].Value.Length; }
        }

        void Card(int i, string main, string note)
        {
            if (i < plates.Length && plates[i] != null)
            {
                plates[i].text = main ?? "";
                // alone on the card it sits on the centre line; with a second line under it, above
                var at = plates[i].rectTransform.anchoredPosition;
                plates[i].rectTransform.anchoredPosition = new Vector2(at.x, string.IsNullOrEmpty(note) ? 0f : 24f);
            }
            if (i < cardNotes.Length && cardNotes[i] != null) cardNotes[i].text = note ?? "";
            if (i < cards.Length && cards[i] != null) cards[i].gameObject.SetActive(!string.IsNullOrEmpty(main));
        }

        /// A value that overshoots and settles: 0 before `from`, 1 after `from + dur`.
        static float Pop(float t, float from, float dur)
        {
            float x = Mathf.Clamp01((t - from) / dur);
            return x <= 0f ? 0f : x < 0.7f ? Mathf.SmoothStep(0f, 1.12f, x / 0.7f) : Mathf.Lerp(1.12f, 1f, (x - 0.7f) / 0.3f);
        }

        void PoseOverlay(float t)
        {
            if (overlay == null) return;
            overlay.alpha = Mathf.Clamp01(t / 0.25f);
            if (title != null) title.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(40f, 120f, Ease(Mathf.Clamp01(t / 0.4f))), title.rectTransform.anchoredPosition.y);
            if (subtitle != null && winnersShot)
            {
                // "AND THE WINNER IS..." in white, a dot a beat, under the roll; then the name in gold, popping in on the crash
                bool called = t >= WinAt;
                subtitle.text = called ? bigText : "AND THE WINNER IS" + new string('.', Mathf.Clamp(Mathf.FloorToInt(t / 0.8f), 0, 3));
                subtitle.color = called ? gold : Color.white;
                subtitle.rectTransform.localScale = Vector3.one * Mathf.Max(0.001f, called ? Pop(t, WinAt, 0.45f) : Pop(t, 0.15f, 0.4f) * 0.62f);
            }
            else if (subtitle != null)
            {
                subtitle.rectTransform.localScale = Vector3.one * Mathf.Max(0.001f, Pop(t, 0.15f, 0.5f));
                // the pot counts up as the camera pulls back
                subtitle.text = amountUnit != null ? (amount * Ease(Mathf.Clamp01((t - 0.35f) / 1.3f))).ToString(amountFormat, System.Globalization.CultureInfo.InvariantCulture) + " " + amountUnit : bigText;
            }
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null) cards[i].localScale = Vector3.one * Mathf.Max(0.001f, Pop(t, cardsFrom + 0.2f * i, 0.32f));
            if (hint != null) hint.alpha = Mathf.Clamp01((t - 2.2f) / 0.5f) * (0.55f + 0.45f * Mathf.Sin(t * 3f));
        }

        // ---------- helpers ----------

        void Begin()
        {
            shotAt = Time.unscaledTime;
            if (Active) return;
            Active = true;
            // Take over from whichever camera is drawing the race; its AudioListener stays where it is.
            raceCam = Camera.main != null && Camera.main != cam ? Camera.main : null;
            if (flying) TakeOff();
            else
            {
                if (raceCam != null) raceCam.enabled = false;
                if (cam != null) cam.enabled = true;
            }
            if (overlay != null) overlay.gameObject.SetActive(true);
            // The race HUD goes; the results card and the pause overlay stay available above the shot.
            var hud = FindAnyObjectByType<RaceHud>();
            var safe = hud != null ? hud.transform.Find("SafeArea") : null;
            if (safe != null)
                foreach (Transform child in safe)
                    if (child.gameObject.activeSelf && child.name != "Results" && child.name != "PauseOverlay") { hiddenHud.Add(child.gameObject); child.gameObject.SetActive(false); }
        }

        void Pop()
        {
            foreach (var p in poppers) if (p != null) { p.Clear(); p.Play(); }
            if (pop != null) AudioDirector.Instance?.Play(pop, 0.9f, Random.Range(0.95f, 1.05f));
            Haptics.Play(Haptics.Kind.Win);
        }

        void ResetTrophy(int i)
        {
            Init();
            if (i >= trophies.Length || trophies[i] == null) return;
            trophies[i].SetParent(trophyParent[i], false);
            trophies[i].localPosition = trophyHome[i];
            trophies[i].localRotation = Quaternion.identity;
            trophies[i].localScale = Vector3.one * (trophyScale.Length > i ? trophyScale[i] : 1f) * trophySize;
        }

        void ClearDrivers()
        {
            for (int i = 0; i < trophies.Length; i++) ResetTrophy(i);
            foreach (var d in spawned) if (d != null) Destroy(d);
            spawned.Clear();
        }

        /// A driver prefab made to stand: no steering wheel, no seated pose, holding the trophy pose. Returns its torso.
        Transform Stand(GameObject d, AnimationClip hold)
        {
            foreach (var pose in d.GetComponentsInChildren<BlockyDriverPose>(true)) { if (pose.Wheel != null) pose.Wheel.gameObject.SetActive(false); pose.enabled = false; }
            foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            // The hold clip is a still pose: sample it once and stop the Animation, so nothing rewrites (or, culled
            // off camera, stops rewriting) the arms that PodiumPose sets.
            var anim = d.GetComponent<Animation>();
            if (anim != null) { anim.Stop(); anim.enabled = false; }
            if (hold != null) hold.SampleAnimation(d, 0f);
            return Find(d.transform, "torso");
        }

        float Size(int i) => (trophyScale.Length > i ? trophyScale[i] : 1f) * trophySize;

        public Rig RigFor(string prefabName)
        {
            foreach (var r in rigs) if (r.driver == prefabName) return r;
            return rigs.Length > 0 ? rigs[0] : default;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
