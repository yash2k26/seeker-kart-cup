using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The two shots round a race. Before it: a drone's lap of the circuit (the race camera, flown along the
    /// waypoints, landing behind the grid) with the words over it: what the race is for, the prize pool counting up and
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
        /// How long the pre-race shot wants: the drone's lap and a beat behind the grid, or the stage's reveal.
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

        // ---------- the drone ----------

        /// The lap to fly: the circuit's waypoints (TrackSetup lays ~48 round it, the first on the start line).
        bool FlightPath()
        {
            var root = GameObject.Find("Waypoints");
            if (root == null || root.transform.childCount < 8 || Camera.main == null || Camera.main == cam) return false;
            // Beach Buggy hints at its shortcuts in the intro: the drone takes the circuit's (ShortcutPath) instead of the corner
            var cut = ShortcutPath.Instance;
            var points = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var w = root.transform.GetChild(i);
                points.Add(w.position);
                if (cut == null || cut.from == null || w != cut.from.transform) continue;
                foreach (var p in cut.path) points.Add(p.transform.position + Vector3.up * 1.5f);
                while (i + 1 < root.transform.childCount && root.transform.GetChild(i + 1) != cut.to.transform) i++;
            }
            path = points.ToArray();
            int n = path.Length;
            float lap = 0f;
            for (int i = 0; i < n; i++) lap += Vector3.Distance(path[i], path[(i + 1) % n]);
            flySeconds = Mathf.Clamp(lap / 190f, 5f, 7.5f);        // quick: about 190 m/s on average
            return true;
        }

        /// A point `s` waypoints round the lap (Catmull-Rom through them, wrapping).
        Vector3 Along(float s)
        {
            int n = path.Length, i = Mathf.FloorToInt(s); float u = s - i;
            Vector3 P(int k) => path[((k % n) + n) % n];
            Vector3 a = P(i - 1), b = P(i), c = P(i + 1), d = P(i + 2);
            return 0.5f * (2f * b + (c - a) * u + (2f * a - 5f * b + 4f * c - d) * u * u + (3f * b - a - 3f * c + d) * u * u * u);
        }

        /// The race camera `t` seconds into the lap: seven metres up (under the tunnel's crown and the start gantry),
        /// looking down the road, leaning into the turns. Away fast, slowing as it comes round to land behind the grid.
        void Fly(float t)
        {
            if (raceCam == null) return;
            float x = Mathf.Clamp01(t / flySeconds), k = x * (1.5f - 0.5f * x);
            float s = Mathf.Lerp(1.5f, path.Length - 2.5f, k);
            var pos = Along(s) + Vector3.up * 7f;
            var rot = Quaternion.LookRotation(Along(s + 2.4f) + Vector3.up * 2f - pos, Vector3.up);
            float turn = Vector3.SignedAngle(Along(s + 0.5f) - Along(s), Along(s + 3f) - Along(s + 2.5f), Vector3.up);
            raceCam.transform.SetPositionAndRotation(pos, rot * Quaternion.Euler(0f, 0f, Mathf.Clamp(-turn * 0.3f, -15f, 15f)));
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
