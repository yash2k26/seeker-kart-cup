using System.Collections;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// The title's hero (10 Oct 2026, Beach Buggy's hub): the chosen driver standing by the chosen kart, in its paint, on
    /// the plinth MenuMeet put in front of the street ("HomeStage"). Rebuilt every time the title shows, so a new pick,
    /// paint or purchase is on the doorstep when you come back.
    public sealed class HomeStage : MonoBehaviour
    {
        [SerializeField] CharacterCarousel carousel;
        [SerializeField] KartDatabase karts;
        [SerializeField] CharacterDatabase roster;
        [SerializeField] GameObject[] kartPrefabs = System.Array.Empty<GameObject>();
        GameObject kart, driver;
        public bool Driving { get; private set; }

        /// CHOOSE SERIES (10 Oct 2026, the user's shot): the driver hops into the kart, the kart turns to the camera and
        /// drives at it; when it fills the screen `done` runs (the flow cuts to the series). With no pair on the stage, at once.
        public void DriveOff(System.Action done)
        {
            if (kart == null || Driving || !Application.isPlaying) { done?.Invoke(); return; }
            StartCoroutine(Drive(done));
        }

        [SerializeField] AudioClip engineClip, screechClip, launchClip;   // the kit's, as fallbacks: a kart's own KartAudio usually carries them

        IEnumerator Drive(System.Action done)
        {
            Driving = true;
            var cam = Camera.main;
            var seat = kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
            // the kart's own sound and smoke, which the garage display had switched off (CharacterCarousel.Mount)
            var ka = kart.GetComponentInChildren<KartAudio>(true);
            var sources = kart.GetComponentsInChildren<AudioSource>(true);
            var engine = ka != null && ka.engineSnd != null ? ka.engineSnd : sources.FirstOrDefault(a => a.name == "EngineSnd");
            var tyres = sources.FirstOrDefault(a => a.name == "TireSnd");
            var oneShot = ka != null && ka.oneShotSource != null ? ka.oneShotSource : sources.FirstOrDefault(a => a.name == "OneShotSnd");
            var sparks = kart.GetComponentsInChildren<ParticleSystem>(true).Where(ps => ps.name.StartsWith("DriftParticles")).ToArray();   // the kit's drift sparks
            // Tyre smoke: the kit has none (its drift systems are sparks), so the dark exhaust puffs are cloned onto the two
            // rear wheels, bigger and longer-lived, and emitted by hand through the turn and the first stretch of the run.
            var dark = kart.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(ps => ps.name.StartsWith("ExhaustDark"));
            var rear = kart.GetComponentsInChildren<KartWheel>(true).OrderBy(w => kart.transform.InverseTransformPoint(w.transform.position).z).Take(2).ToArray();
            var smoke = new System.Collections.Generic.List<ParticleSystem>();
            if (dark != null)
                foreach (var w in rear)
                {
                    var puff = Instantiate(dark.gameObject, w.transform).GetComponent<ParticleSystem>();
                    puff.name = "TyreSmoke"; puff.transform.localPosition = new Vector3(0f, -0.2f, -0.3f); puff.transform.localRotation = Quaternion.identity;
                    var main = puff.main; main.startSizeMultiplier *= 4.5f; main.startLifetimeMultiplier *= 2.2f; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.startColor = new Color(0.86f, 0.86f, 0.92f, 0.85f);   // light grey: black exhaust vanishes against a night road
                    var vel = puff.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // and it rises (all three in one curve mode, or Unity logs an error every play)
                    puff.gameObject.SetActive(true); smoke.Add(puff);
                }
            var exhaust = kart.GetComponentsInChildren<ParticleSystem>(true).Where(ps => ps.name.StartsWith("ExhaustParticles") || ps.name.StartsWith("BoostParticles")).ToArray();
            void Start(AudioSource a, AudioClip clip, float volume, float pitch, bool loop)
            {
                if (a == null) return;
                a.enabled = true; a.gameObject.SetActive(true);
                if (a.clip == null) a.clip = clip;
                a.loop = loop; a.volume = volume; a.pitch = pitch;
                if (a.clip != null && !a.isPlaying) a.Play();
            }
            // 1. the hop: up and over, into the seat; the engine catches as the driver lands
            if (driver != null && seat != null)
            {
                Vector3 from = driver.transform.position; Quaternion fromRot = driver.transform.rotation;
                Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
                for (float t = 0f; t < 0.4f && driver != null; t += Time.deltaTime)
                {
                    float u = Mathf.Clamp01(t / 0.4f);
                    var p = Vector3.Lerp(from, seat.position, u); p.y += Mathf.Sin(u * Mathf.PI) * 1.7f;
                    driver.transform.position = p; driver.transform.rotation = Quaternion.Slerp(fromRot, seat.rotation, u);
                    yield return null;
                }
                if (driver != null) Destroy(driver); driver = null;
                var prefab = carousel != null ? carousel.DriverPrefabFor(roster != null ? roster.Selected : null) : null;
                if (prefab != null && kart != null) { var d = Instantiate(prefab, seat); d.transform.localPosition = Vector3.zero; d.transform.localRotation = Quaternion.identity; }
                Haptics.Play(Haptics.Kind.Land);
            }
            if (kart == null) { Driving = false; done?.Invoke(); yield break; }
            Start(engine, engineClip, 0.9f, 0.6f, true);
            yield return new WaitForSeconds(0.05f);
            // 2. round on the spot, tyres screeching and smoking (a slide while turning read as the kart skating), then
            // straight at the camera, faster and faster until the nose is in the lens. The wheels roll about the kart's
            // axle (KartWheel points a wheel's own forward along the axle, so a local X spin tilts it): world-space,
            // about transform.right, forward positive.
            var wheels = kart.GetComponentsInChildren<KartWheel>().Select(w => w.visualWheel).Where(w => w != null).ToArray();
            Vector3 target = cam != null ? cam.transform.position : kart.transform.position + kart.transform.forward * 20f;
            Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
            var dir = Flat(target - kart.transform.position).normalized;
            var startRot = kart.transform.rotation; var aim = Quaternion.LookRotation(dir, Vector3.up);
            Start(tyres, ka != null && ka.defaultTireSnd != null ? ka.defaultTireSnd : screechClip, 0.8f, 1.05f, true);
            // The kit's drift systems emit 15 a second, a haze over a long slide: a turn this short needs its smoke emitted
            // by hand, a cloud at the launch and a stream through the run.
            foreach (var ps in smoke) { ps.Play(true); ps.Emit(16); }
            foreach (var ps in sparks) { ps.gameObject.SetActive(true); ps.Play(true); }
            for (float turn = 0f; turn < 1f && kart != null; turn = Mathf.MoveTowards(turn, 1f, Time.deltaTime / 0.22f))
            {
                kart.transform.rotation = Quaternion.Slerp(startRot, aim, Mathf.SmoothStep(0f, 1f, turn));
                if (engine != null) engine.pitch = Mathf.Lerp(0.6f, 1.1f, turn);   // revving up for the launch
                foreach (var ps in smoke) ps.Emit(Mathf.CeilToInt(180f * Time.deltaTime));
                foreach (var ps in sparks) ps.Emit(Mathf.CeilToInt(120f * Time.deltaTime));
                yield return null;
            }
            foreach (var ps in smoke) ps.Emit(30);   // the wheels light up as it goes
            foreach (var ps in sparks) ps.Emit(30);
            if (kart != null) kart.transform.rotation = aim;
            float speed = 0f, ran = 0f;
            Haptics.Play(Haptics.Kind.Boost);
            if (oneShot != null) { oneShot.enabled = true; oneShot.gameObject.SetActive(true); var launch = ka != null && ka.boostStartSnd != null ? ka.boostStartSnd : launchClip; if (launch != null) oneShot.PlayOneShot(launch, 0.9f); }
            foreach (var ps in exhaust) { ps.gameObject.SetActive(true); ps.Play(true); }
            while (kart != null)
            {
                float dt = Time.deltaTime; ran += dt;
                speed = Mathf.Min(speed + 45f * dt, 34f);
                kart.transform.position += dir * speed * dt;
                float deg = speed * dt / 0.35f * Mathf.Rad2Deg;   // a 0.35 m wheel: distance over radius, in degrees
                foreach (var w in wheels) if (w != null) w.Rotate(kart.transform.right, deg, Space.World);
                if (engine != null) engine.pitch = Mathf.Lerp(1.1f, 1.75f, speed / 34f);
                if (tyres != null && ran > 0.35f && tyres.isPlaying) { tyres.volume = Mathf.MoveTowards(tyres.volume, 0f, dt * 3f); if (tyres.volume <= 0f) tyres.Stop(); }   // the wheels hook up: the screech dies
                if (ran <= 0.5f) { foreach (var ps in smoke) ps.Emit(Mathf.CeilToInt(160f * (1f - ran / 0.5f) * dt)); foreach (var ps in sparks) ps.Emit(Mathf.CeilToInt(80f * (1f - ran / 0.5f) * dt)); }   // thinning as the wheels hook up
                else { foreach (var ps in smoke) if (ps.isEmitting) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); foreach (var ps in sparks) if (ps.isEmitting) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
                foreach (var ps in exhaust) ps.Emit(Mathf.CeilToInt(90f * dt));
                var toCam = Flat(target - kart.transform.position);
                if (toCam.magnitude < 2.4f || Vector3.Dot(dir, toCam) < 0f) break;
                yield return null;
            }
            Driving = false;
            done?.Invoke();
        }

        void OnEnable() { Bays(false); Build(); }
        void OnDisable() { Clear(); Bays(true); }
        public void ShowInEditor() { Bays(false); Build(); }
        public void LeaveInEditor() { Clear(); Bays(true); }

        /// The other drivers' bays (Kart_*, Bay_*) are the select screen's scenery: the home is just you and your kart
        /// (10 Oct 2026, the user), so they go while the title is up and come back for the driver select.
        void Bays(bool on)
        {
            var backdrop = FindFirstObjectByType<MenuBackdrop>();
            var meet = backdrop != null ? backdrop.Meet : null;
            if (meet == null) return;
            for (int i = 0; i < meet.childCount; i++)
            {
                var c = meet.GetChild(i);
                if (c.name.StartsWith("Kart_") || c.name.StartsWith("Bay_") || c.name.StartsWith("BayKey_")) c.gameObject.SetActive(on);
            }
        }

        void Clear()
        {
            foreach (var go in new[] { kart, driver })
                if (go != null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            kart = driver = null;
        }

        void Build()
        {
            Clear();
            var backdrop = FindFirstObjectByType<MenuBackdrop>();
            var stage = backdrop != null && backdrop.Meet != null ? backdrop.Meet.Find("HomeStage") : null;
            if (stage == null || karts == null) return;
            var def = roster != null ? roster.Selected : null;
            string kartId = karts.EffectiveKartFor(def, allowTrial: false) ?? "";
            var prefab = kartPrefabs.FirstOrDefault(k => k != null && k.name.EndsWith(kartId)) ?? kartPrefabs.FirstOrDefault();
            if (prefab != null)
            {
                var paint = Paints.Find(Paints.SelectedId);
                var colour = paint != null ? paint.color : Paints.ColorFor(def, kartId);
                kart = CharacterCarousel.Mount(prefab, null, stage, colour, false);
                if (paint != null ? paint.gold : Paints.GoldOn(kartId)) CharacterDef.Paint(kart, colour, gold: true);
                // Beach Buggy's pair: the kart left of the plinth's centre, nose to the front-left; the driver at the right, facing you
                kart.transform.localPosition += new Vector3(-1.4f, 0f, 0.3f);
                kart.transform.localRotation = Quaternion.Euler(0f, 212f, 0f);
                foreach (var r in kart.GetComponentsInChildren<Renderer>()) if (r.name != "BlobShadow") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (def != null && carousel != null && (driver = carousel.StandFor(def, stage)) != null)
            {
                driver.transform.localPosition += new Vector3(1.5f, 0f, -0.3f);
                driver.transform.localRotation = Quaternion.Euler(0f, 190f, 0f);
                foreach (var r in driver.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var head = driver.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head");
                driver.AddComponent<MeetActor>().Bind(driver.GetComponent<Animation>(), head, System.Array.Empty<string>(), false);   // watches the camera, as the street does
            }
        }
    }
}
