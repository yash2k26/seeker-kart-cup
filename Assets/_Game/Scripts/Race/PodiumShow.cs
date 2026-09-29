using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The ceremony stage, used for two shots. Before a race: the three trophies on the podium, what each place wins,
    /// and the prize pool (whatever it is), with party poppers going off. After it: the top three standing on their
    /// blocks holding their trophies (gold, biggest, to the winner, who is too excited to stand still), under confetti.
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
        [SerializeField] TMP_Text title, subtitle;
        [SerializeField] TMP_Text[] plates = new TMP_Text[3];       // on each block: the prize, or the racer's name
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
        // The racers stand taller on the podium than in their karts, and the trophies are made to be seen from a phone.
        [SerializeField] float driverScale = 1.7f, trophySize = 1.9f;
        [SerializeField] float namePlateHeight = 2.3f;   // above each block's top
        const float TrophyHeight = 0.545f;                // plinth and cup at scale 1 (PodiumSetup.CupMesh)

        public bool Active { get; private set; }
        public const float PrizeSeconds = 4.5f;       // the pre-race shot (online it runs until 3-2-1)
        public const float CeremonySeconds = 5f;      // the podium before the results card comes up

        Camera raceCam;
        readonly List<GameObject> hiddenHud = new();
        readonly List<GameObject> spawned = new();
        Vector3[] trophyHome;
        Transform[] trophyParent;
        float shotAt;
        bool winnersShot;
        Vector3 focus;

        public struct Winner { public GameObject driver; public string name; }

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

        /// Where each trophy rests on its block, remembered before any racer picks one up. (Lazy, so an edit-mode
        /// preview, where Awake never runs, gets it too.)
        void Init()
        {
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
            Begin();
            winnersShot = false;
            ClearDrivers();
            for (int i = 0; i < trophies.Length; i++) ResetTrophy(i);
            if (title != null) title.text = heading;
            if (subtitle != null) subtitle.text = amount;
            // each place's line floats just over its own trophy
            for (int i = 0; i < plates.Length; i++) if (plates[i] != null) { plates[i].text = places != null && i < places.Count ? places[i] : ""; Plate(i, TrophyHeight * Size(i) + 0.4f); }
            focus = trophies.Length > 0 && trophies[0] != null ? trophies[0].position + Vector3.up * TrophyHeight * Size(0) * 0.55f : transform.position + Vector3.up * 2f;
            Pop();
        }

        /// After the race: the top three, winner first, on the podium with their trophies. Fewer than three racers
        /// leaves the empty places' trophies on their blocks.
        public void Winners(string heading, IList<Winner> top)
        {
            Init();
            Begin();
            winnersShot = true;
            ClearDrivers();
            if (title != null) title.text = heading;
            if (subtitle != null) subtitle.text = "";
            for (int i = 0; i < spots.Length && i < trophies.Length; i++)
            {
                ResetTrophy(i);
                if (plates[i] != null) { plates[i].text = ""; Plate(i, namePlateHeight + (i == 0 ? 0.5f : 0f)); }
                if (top == null || i >= top.Count || top[i].driver == null) continue;
                var d = Instantiate(top[i].driver, spots[i].position, spots[i].rotation);
                d.name = "Podium_" + top[i].name;
                d.transform.localScale *= driverScale;
                spawned.Add(d);
                // the winner's name is the title; second and third wear theirs overhead
                if (plates[i] != null) plates[i].text = i == 0 ? "" : top[i].name.ToUpperInvariant();
                var rig = RigFor(top[i].driver.name);
                var torso = Stand(d, rig.hold);
                var holder = d.AddComponent<PodiumPose>();
                holder.Init(i, rig);
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
            Pop();
            if (rain != null) { rain.Clear(); rain.Play(); }
        }

        /// Any tap on the screen (skips a shot).
        public static bool Tapped() => UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame;

        /// Back to the race camera and HUD.
        public void End()
        {
            if (!Active) return;
            Active = false;
            if (cam != null) cam.enabled = false;
            if (raceCam != null) raceCam.enabled = true;
            foreach (var go in hiddenHud) if (go != null) go.SetActive(true);
            hiddenHud.Clear();
            if (rain != null) rain.Stop();
        }

        // ---------- camera ----------

        void LateUpdate() { if (Active) PoseCamera(Time.unscaledTime - shotAt); }

        /// The camera `t` seconds into the current shot.
        public void PoseCamera(float t)
        {
            if (cam == null) return;
            var stage = transform;
            if (!winnersShot)
            {
                // Start close on the gold trophy, pull back to the whole podium and the title: the reveal.
                float k = Ease(Mathf.Clamp01((t - 0.3f) / 3.2f));
                var near = focus + stage.forward * -3.3f + Vector3.up * 0.3f;
                var far = stage.position + stage.forward * -11.5f + Vector3.up * 3.2f;
                var lookNear = focus;
                var lookFar = stage.position + Vector3.up * 2.4f;
                cam.transform.position = Vector3.Lerp(near, far, k) + stage.right * Mathf.Sin(t * 0.4f) * 0.6f;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(lookNear, lookFar, k) - cam.transform.position, Vector3.up);
            }
            else
            {
                // Low and wide on the podium, easing in on the winner, drifting a little from side to side.
                float k = Ease(Mathf.Clamp01(t / 4.5f));
                float orbit = Mathf.Sin(t * 0.35f) * 9f;
                var dir = Quaternion.AngleAxis(orbit, Vector3.up) * -stage.forward;
                var pos = stage.position + dir * Mathf.Lerp(11.5f, 8.2f, k) + Vector3.up * Mathf.Lerp(1.8f, 2.6f, k);
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.Lerp(stage.position + Vector3.up * 2.9f, focus, k * 0.5f) - pos, Vector3.up);
            }
        }

        static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        // ---------- helpers ----------

        void Begin()
        {
            shotAt = Time.unscaledTime;
            if (Active) return;
            Active = true;
            // Take over from whichever camera is drawing the race; its AudioListener stays where it is.
            raceCam = Camera.main != null && Camera.main != cam ? Camera.main : null;
            if (raceCam != null) raceCam.enabled = false;
            if (cam != null) cam.enabled = true;
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

        /// A plate floats `height` above its block, over the trophy before the race and over the racer after it.
        void Plate(int i, float height)
        {
            if (i >= plates.Length || plates[i] == null || i >= spots.Length || spots[i] == null) return;
            var p = plates[i].transform.position;
            plates[i].transform.position = new Vector3(p.x, spots[i].position.y + height, p.z);
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
