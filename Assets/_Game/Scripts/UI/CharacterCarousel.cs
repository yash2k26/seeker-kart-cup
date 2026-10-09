using System.Collections.Generic;
using System.Linq;
using PowerslideKartPhysics;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The character select: the driver, standing on the podium and facing you, swapped by the roster strip, a flick or
    /// the arrows (Beach Buggy's driver screen: the character alone, big, alive; their kart is the garage's). They wave
    /// hello when they arrive and idle on their own rig's takes. No render textures, no portraits to maintain.
    public sealed class CharacterCarousel : Carousel
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Transform podium;
        [SerializeField] GameObject[] kartPrefabs;
        [SerializeField] GameObject[] driverPrefabs;
        [SerializeField] TMP_Text nameLabel, blurbLabel;
        // the big button says CHOOSE TRACK, or UNLOCK · price for a locked judge; the half-row under it takes SOL and ORE
        [SerializeField] TMP_Text nextLabel, secondaryLabel, tertiaryLabel;
        [SerializeField] Button secondaryButton, tertiaryButton;
        string message;   // a purchase's status, shown in the blurb's second line while it is fresh
        MenuBackdrop backdrop; Transform bay;   // the street select: the driver's bay on the meet, when MenuMeet built one
        [SerializeField] StatBar speedBar, accelBar, handlingBar, weightBar, turboBar, attackBar;
        // The podium is a turntable: it turns steadily (degrees per second) with the kart standing on it. Swaying the
        // kart alone over bare sand read as floating. Time-based, so the garage and this screen never jump between them.
        [SerializeField] float baseYaw = 65f, spin = 14f;
        // Standing (the select screen): each driver's idle and greeting takes (MenuSetup builds them from the driver's own
        // model), how far the rig's origin sits above its feet in that pose, and the world scale they have in a seat.
        [SerializeField] bool standing = true;
        [SerializeField] AnimationClip[] idleClips = System.Array.Empty<AnimationClip>(), waveClips = System.Array.Empty<AnimationClip>();
        [SerializeField] float[] standLifts = System.Array.Empty<float>();
        [SerializeField] float standScale = 1f, standYaw = 8f;
        Animation standingAnim; string idleName, waveName;

        GameObject current;
        int index;

        /// The driver on the turntable changed (arrows, a flick, or a face in the roster strip).
        public static event System.Action<int> Changed;
        public int Index => index;
        /// The driver on show is a judge this phone has not bought or beaten yet: browsable, not raceable.
        public bool Locked => roster != null && !CharacterDatabase.Owned(roster.At(index));

        void Start()
        {
            index = CharacterDatabase.SelectedIndex;
            Show();
        }

        /// Editor-side preview so the menu scene is not empty when it is opened or screenshotted.
        public void ShowInEditor(int i) { index = i; Show(); if (podium != null) podium.localRotation = Quaternion.Euler(0, baseYaw, 0); }

        /// A face tapped in the roster strip: straight to that driver. Tapping the one already on the turntable is a poke.
        public void Pick(int i)
        {
            if (roster == null || i < 0 || i >= roster.characters.Length) return;
            if (i == index) { Poke(); return; }
            index = i; message = null;
            if (!Locked) CharacterDatabase.SelectedIndex = index;   // a locked judge is looked at, not picked
            Show();
            Greet(0.22f);
        }

        // ---- voices: each driver says hello on arrival and answers a tap in their own voice (Voices, DriverVoices) ----
        float lastPoke; int pokes; Tween hop;
        string DriverId => roster != null ? roster.At(index)?.driverId : null;

        /// The driver on the turntable says hello (just arrived, or the screen opened on them); a negative delay
        /// takes back one that has not been said yet.
        public void Greet(float delay)
        {
            if (!Application.isPlaying) return;
            CancelInvoke(nameof(SayHello));
            if (delay >= 0f) Invoke(nameof(SayHello), delay);
        }

        void SayHello() => Voices.Say(DriverId, "hello");

        /// A tap on the kart: a line, and the kart hops. Keep poking and they get annoyed, then start over.
        public override void Poke()
        {
            CancelInvoke(nameof(SayHello));
            pokes = Time.unscaledTime - lastPoke < 2.5f ? pokes + 1 : 1;
            lastPoke = Time.unscaledTime;
            Voices.Say(DriverId, pokes >= 4 ? "annoyed" : "poke");
            if (pokes >= 4) pokes = 0;
            Haptics.Play(Haptics.Kind.Tick);
            if (current == null) return;
            if (hop.isAlive) hop.Complete();
            hop = Tween.PunchLocalPosition(current.transform, new Vector3(0f, 0.5f, 0f), 0.42f, frequency: 2, useUnscaledTime: true);
            Wave();
        }

        /// The greeting take, then back to idling.
        void Wave()
        {
            if (standingAnim == null || string.IsNullOrEmpty(waveName) || standingAnim.GetClip(waveName) == null) return;
            standingAnim.CrossFade(waveName, 0.12f);
            if (!string.IsNullOrEmpty(idleName)) standingAnim.CrossFadeQueued(idleName, 0.25f, QueueMode.CompleteOthers);
        }

        void Update()
        {
            if (!Dragging) DragYaw = Mathf.Lerp(DragYaw, 0f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            // standing, they face you and hold still (a drag still turns them); a kart alone keeps turning on the disc
            if (podium != null) podium.localRotation = Quaternion.Euler(0, (standing ? standYaw : baseYaw + Time.time * spin) + DragYaw, 0);
        }

        public override void Next() { Step(+1); }
        public override void Previous() { Step(-1); }

        void Step(int delta)
        {
            if (roster == null || roster.characters.Length == 0) return;
            index = ((index + delta) % roster.characters.Length + roster.characters.Length) % roster.characters.Length;
            message = null;
            if (!Locked) CharacterDatabase.SelectedIndex = index;
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
            Show();
            Greet(0.22f);
        }

        static void SetButton(TMP_Text label, Button button, string text)
        {
            if (label != null) label.text = text;
            if (button != null) button.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// CHOOSE TRACK on a locked judge: recruit them with gems (Beach Buggy's recruit), or go and get gems. Beating them
        /// in the Tour is the free way.
        public void Recruit()
        {
            var def = roster != null ? roster.At(index) : null;
            if (def == null || !Locked) return;
            int price = Gems.PriceOf(def);
            if (!Gems.Spend(price, def.sku)) { ScreenFlow.Instance?.ShowGems(); return; }
            Unlocks.Earn(def.sku);
            CharacterDatabase.SelectedIndex = index;
            message = $"<color=#FFB126>{def.displayName.ToUpperInvariant()} JOINS YOUR ROSTER</color>";
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm(); Greet(0.2f);
            KartApi.Event("recruit", new { driver = def.displayName, price });
            Show();
        }
        public void Secondary() => ScreenFlow.Instance?.ShowGems();
        public void Tertiary() => ScreenFlow.Instance?.ShowGems();

        /// The garage borrows the podium: hide our kart (and stop turning it) while it is open, bring it back after.
        public void SetVisible(bool on)
        {
            enabled = on;
            if (current != null) current.SetActive(on);
            if (backdrop != null) backdrop.Spot = on && bay != null && gameObject.activeInHierarchy ? bay : null;
        }
        /// The select screen opened again: the camera back to the driver on show.
        public void Refocus() { if (backdrop != null && bay != null) backdrop.Spot = bay; Show(); }   // and the card: the gem balance may have changed

        void Show()
        {
            var def = roster != null ? roster.At(index) : null;
            if (def == null) return;

            if (current != null) Destroy(current);
            var kartPrefab = kartPrefabs.FirstOrDefault(k => k.name.EndsWith(def.kartId)) ?? kartPrefabs.FirstOrDefault();
            var driverPrefab = driverPrefabs.FirstOrDefault(d => d.name.EndsWith(def.driverId)) ?? driverPrefabs.FirstOrDefault();
            backdrop ??= FindFirstObjectByType<MenuBackdrop>();
            bay = backdrop != null ? backdrop.BayOf(def.displayName) : null;
            if (bay != null)
            {   // the street: the camera goes to the driver where they stand with their kart; the turntable stays empty
                backdrop.Spot = bay;
                if (Application.isPlaying) bay.GetComponentInChildren<MeetActor>()?.Fidget();
            }
            else if (standing && driverPrefab != null && podium != null) current = StandUp(driverPrefab, Application.isPlaying);
            else if (kartPrefab != null && podium != null) current = Mount(kartPrefab, driverPrefab, podium, def.tint, Application.isPlaying);

            if (nameLabel != null)
            {
                nameLabel.text = def.displayName.ToUpperInvariant();
                if (Application.isPlaying) UiMotion.Punch(nameLabel.transform, 1.08f, 0.25f);   // a new driver, said out loud
            }
            // the driver's special, as Beach Buggy's driver screen names it under the character
            var special = Ability.For(def.displayName);
            bool locked = Locked;
            int price = Gems.PriceOf(def);
            // two lines fit above the stat rows: the blurb and the special, or (locked) the one line that says how to get them
            string second = message ?? $"<color=#FFB126>{special.name}</color>  <size=90%>{special.line}</size>";
            if (blurbLabel != null) blurbLabel.text = locked && message == null ? $"<color=#FFB126>LOCKED</color>  Beat {def.displayName} in Kartel Tour, or recruit them. You have {Gems.Balance} gems."
                                                    : $"{def.blurb}\n{second}";
            if (nextLabel != null) nextLabel.text = locked ? $"RECRUIT  ·  {price} GEMS" : "CHOOSE TRACK";
            SetButton(secondaryLabel, secondaryButton, locked ? (Gems.Balance >= price ? $"GET MORE GEMS  ·  {Gems.Balance}" : $"GET GEMS  ·  YOU HAVE {Gems.Balance}") : "");
            SetButton(tertiaryLabel, tertiaryButton, "");
            UiMotion.Beside(secondaryButton, tertiaryButton);
            speedBar?.Set(def.speed);
            accelBar?.Set(def.accel);
            handlingBar?.Set(def.handling);
            weightBar?.Set(def.weight);
            turboBar?.Set(def.turbo);
            attackBar?.Set(def.attack);
            Changed?.Invoke(index);
        }

        /// The driver on their feet in the middle of the disc, facing +Z of the podium (standYaw turns that to the camera):
        /// no seat pose, no steering wheel, their own idle take playing, the feet on the disc's top face.
        GameObject StandUp(GameObject prefab, bool animate)
        {
            int i = System.Array.IndexOf(driverPrefabs, prefab);
            var idle = i >= 0 && i < idleClips.Length ? idleClips[i] : null;
            var wave = i >= 0 && i < waveClips.Length ? waveClips[i] : null;
            float lift = i >= 0 && i < standLifts.Length ? standLifts[i] : 0f;
            var d = Instantiate(prefab, podium);
            float scale = standScale / Mathf.Max(0.0001f, podium.lossyScale.x);
            d.transform.localRotation = Quaternion.identity;
            d.transform.localScale = Vector3.one * scale;
            d.transform.localPosition = Vector3.up * (lift * scale);
            foreach (var pose in d.GetComponentsInChildren<BlockyDriverPose>(true)) { if (pose.Wheel != null) pose.Wheel.gameObject.SetActive(false); pose.enabled = false; }
            foreach (var t in d.GetComponentsInChildren<Transform>(true)) if (t != d.transform && t.name.ToLowerInvariant().Contains("steering")) t.gameObject.SetActive(false);
            foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            standingAnim = d.GetComponent<Animation>();
            if (standingAnim == null) standingAnim = d.AddComponent<Animation>();
            standingAnim.cullingType = AnimationCullingType.AlwaysAnimate;
            idleName = idle != null ? idle.name : null; waveName = wave != null ? wave.name : null;
            if (idle != null)
            {
                if (standingAnim.GetClip(idle.name) == null) standingAnim.AddClip(idle, idle.name);
                standingAnim.clip = idle;
                idle.SampleAnimation(d, 0f);                              // the pose an edit-mode preview shows too
                if (Application.isPlaying) standingAnim.Play(idle.name);
            }
            if (wave != null && standingAnim.GetClip(wave.name) == null) standingAnim.AddClip(wave, wave.name);
            if (animate)
            {
                Tween.Scale(d.transform, d.transform.localScale * 0.7f, d.transform.localScale, 0.28f, Ease.OutBack);
                Invoke(nameof(Wave), 0.2f);
            }
            return d;
        }

        public GameObject DriverPrefabFor(CharacterDef def) => def != null ? driverPrefabs.FirstOrDefault(d => d.name.EndsWith(def.driverId)) ?? driverPrefabs.FirstOrDefault() : null;

        /// The driver on their feet at `parent`'s origin, their own idle take playing: the title's home stage borrows the
        /// select screen's clips and lifts. Nothing of the turntable's own state is touched.
        public GameObject StandFor(CharacterDef def, Transform parent)
        {
            var prefab = def != null ? driverPrefabs.FirstOrDefault(d => d.name.EndsWith(def.driverId)) ?? driverPrefabs.FirstOrDefault() : null;
            if (prefab == null || parent == null) return null;
            int i = System.Array.IndexOf(driverPrefabs, prefab);
            var idle = i >= 0 && i < idleClips.Length ? idleClips[i] : null;
            float lift = i >= 0 && i < standLifts.Length ? standLifts[i] : 0f;
            var d = Instantiate(prefab, parent);
            float scale = standScale / Mathf.Max(0.0001f, parent.lossyScale.x);
            d.transform.localRotation = Quaternion.identity; d.transform.localScale = Vector3.one * scale; d.transform.localPosition = Vector3.up * (lift * scale);
            foreach (var pose in d.GetComponentsInChildren<BlockyDriverPose>(true)) { if (pose.Wheel != null) pose.Wheel.gameObject.SetActive(false); pose.enabled = false; }
            foreach (var t in d.GetComponentsInChildren<Transform>(true)) if (t != d.transform && t.name.ToLowerInvariant().Contains("steering")) t.gameObject.SetActive(false);
            foreach (var smr in d.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            var anim = d.GetComponent<Animation>() ?? d.AddComponent<Animation>();
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            if (idle != null)
            {
                if (anim.GetClip(idle.name) == null) anim.AddClip(idle, idle.name);
                anim.clip = idle; idle.SampleAnimation(d, 0f);
                if (Application.isPlaying) anim.Play(idle.name);
            }
            return d;
        }

        /// A display-only kart with its driver on the podium: no physics, no input, no race components, tyres grounded,
        /// body painted. Shared by the character select and the garage so the two screens show the same thing.
        public static GameObject Mount(GameObject kartPrefab, GameObject driverPrefab, Transform podium, Color tint, bool animate)
        {
            var current = Instantiate(kartPrefab, podium);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
            if (animate) Tween.Scale(current.transform, 0.7f, 1f, 0.28f, Ease.OutBack);   // the swap is felt, not just seen
            foreach (var body in current.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            foreach (var kart in current.GetComponentsInChildren<Kart>()) kart.enabled = false;
            foreach (var col in current.GetComponentsInChildren<Collider>()) col.enabled = false;
            // Wheel Update still extends suspension when Kart is disabled. Freeze the display at its authored axle.
            var wheels = current.GetComponentsInChildren<KartWheel>();
            foreach (var wheel in wheels)
            {
                wheel.enabled = false;
                if (wheel.visualWheel != null) wheel.visualWheel.localPosition = Vector3.zero;
            }
            foreach (var fx in current.GetComponentsInChildren<ParticleSystem>()) fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var audio in current.GetComponentsInChildren<AudioSource>()) audio.enabled = false;
            foreach (var effects in current.GetComponentsInChildren<KartEffects>()) effects.enabled = false;
            // Ground the displayed tyres; the physics prefab root is above the contact patch.
            float lowest = float.PositiveInfinity;
            foreach (var wheel in wheels)
                foreach (var renderer in wheel.visualWheel.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.enabled) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            if (!float.IsInfinity(lowest)) current.transform.position += Vector3.up * (podium.position.y - lowest + 0.015f);

            if (tint != Color.white) CharacterDef.Paint(current, tint);   // painted like the one you will race

            var seat = current.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
            if (seat != null && driverPrefab != null)
            {
                var driver = Instantiate(driverPrefab, seat);
                driver.transform.localPosition = Vector3.zero;
                driver.transform.localRotation = Quaternion.identity;
            }
            return current;
        }
    }
}
