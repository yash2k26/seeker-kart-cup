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

        IEnumerator Drive(System.Action done)
        {
            Driving = true;
            var cam = Camera.main;
            var seat = kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
            // 1. the hop: up and over, into the seat; then the seated driver takes over
            if (driver != null && seat != null)
            {
                Vector3 from = driver.transform.position; Quaternion fromRot = driver.transform.rotation;
                Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
                for (float t = 0f; t < 0.55f && driver != null; t += Time.deltaTime)
                {
                    float u = Mathf.Clamp01(t / 0.55f);
                    var p = Vector3.Lerp(from, seat.position, u); p.y += Mathf.Sin(u * Mathf.PI) * 1.7f;
                    driver.transform.position = p; driver.transform.rotation = Quaternion.Slerp(fromRot, seat.rotation, u);
                    yield return null;
                }
                if (driver != null) Destroy(driver); driver = null;
                var prefab = carousel != null ? carousel.DriverPrefabFor(roster != null ? roster.Selected : null) : null;
                if (prefab != null && kart != null) { var d = Instantiate(prefab, seat); d.transform.localPosition = Vector3.zero; d.transform.localRotation = Quaternion.identity; }
                Haptics.Play(Haptics.Kind.Land);
                yield return new WaitForSeconds(0.12f);
            }
            if (kart == null) { Driving = false; done?.Invoke(); yield break; }
            // 2. round to face the camera, then go: faster and faster until the nose is in the lens
            var wheels = kart.GetComponentsInChildren<KartWheel>().Select(w => w.visualWheel).Where(w => w != null).ToArray();
            Vector3 target = cam != null ? cam.transform.position : kart.transform.position + kart.transform.forward * 20f;
            Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
            var dir = Flat(target - kart.transform.position).normalized;
            var startRot = kart.transform.rotation; var aim = Quaternion.LookRotation(dir, Vector3.up);
            // round on the spot first (a slide while turning read as the kart skating), then straight at the camera.
            // The wheels roll about the kart's axle (KartWheel points a wheel's own forward along the axle, so a local X
            // spin tilts it instead): world-space, about transform.right, forward positive.
            for (float turn = 0f; turn < 1f && kart != null; turn = Mathf.MoveTowards(turn, 1f, Time.deltaTime / 0.35f))
            {
                kart.transform.rotation = Quaternion.Slerp(startRot, aim, Mathf.SmoothStep(0f, 1f, turn));
                yield return null;
            }
            if (kart != null) kart.transform.rotation = aim;
            float speed = 0f;
            Haptics.Play(Haptics.Kind.Boost); AudioDirector.Instance?.Confirm();
            while (kart != null)
            {
                float dt = Time.deltaTime;
                speed = Mathf.Min(speed + 30f * dt, 28f);
                kart.transform.position += dir * speed * dt;
                float deg = speed * dt / 0.35f * Mathf.Rad2Deg;   // a 0.35 m wheel: distance over radius, in degrees
                foreach (var w in wheels) if (w != null) w.Rotate(kart.transform.right, deg, Space.World);
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
