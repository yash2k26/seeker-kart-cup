using System.Linq;
using PowerslideKartPhysics;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The character select: a live kart and driver on the podium, swapped by the arrows. No render textures and no
    /// portraits to maintain — the thing you race is the thing you are looking at.
    public sealed class CharacterCarousel : Carousel
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Transform podium;
        [SerializeField] GameObject[] kartPrefabs;
        [SerializeField] GameObject[] driverPrefabs;
        [SerializeField] TMP_Text nameLabel, blurbLabel;
        [SerializeField] StatBar speedBar, accelBar, handlingBar, weightBar, turboBar, attackBar;
        // The podium is a turntable: it turns steadily (degrees per second) with the kart standing on it. Swaying the
        // kart alone over bare sand read as floating. Time-based, so the garage and this screen never jump between them.
        [SerializeField] float baseYaw = 65f, spin = 14f;

        GameObject current;
        int index;

        /// The driver on the turntable changed (arrows, a flick, or a face in the roster strip).
        public static event System.Action<int> Changed;
        public int Index => index;

        void Start()
        {
            index = CharacterDatabase.SelectedIndex;
            Show();
        }

        /// Editor-side preview so the menu scene is not empty when it is opened or screenshotted.
        public void ShowInEditor(int i) { index = i; Show(); if (podium != null) podium.localRotation = Quaternion.Euler(0, baseYaw, 0); }

        /// A face tapped in the roster strip: straight to that driver.
        public void Pick(int i)
        {
            if (roster == null || i < 0 || i >= roster.characters.Length || i == index) return;
            index = i;
            CharacterDatabase.SelectedIndex = index;
            Show();
        }

        void Update()
        {
            if (!Dragging) DragYaw = Mathf.Lerp(DragYaw, 0f, 1f - Mathf.Exp(-4f * Time.deltaTime));
            if (podium != null) podium.localRotation = Quaternion.Euler(0, baseYaw + Time.time * spin + DragYaw, 0);
        }

        public override void Next() { Step(+1); }
        public override void Previous() { Step(-1); }

        void Step(int delta)
        {
            if (roster == null || roster.characters.Length == 0) return;
            index = ((index + delta) % roster.characters.Length + roster.characters.Length) % roster.characters.Length;
            CharacterDatabase.SelectedIndex = index;
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
            Show();
        }

        /// The garage borrows the podium: hide our kart (and stop turning it) while it is open, bring it back after.
        public void SetVisible(bool on)
        {
            enabled = on;
            if (current != null) current.SetActive(on);
        }

        void Show()
        {
            var def = roster != null ? roster.At(index) : null;
            if (def == null) return;

            if (current != null) Destroy(current);
            var kartPrefab = kartPrefabs.FirstOrDefault(k => k.name.EndsWith(def.kartId)) ?? kartPrefabs.FirstOrDefault();
            var driverPrefab = driverPrefabs.FirstOrDefault(d => d.name.EndsWith(def.driverId)) ?? driverPrefabs.FirstOrDefault();
            if (kartPrefab != null && podium != null) current = Mount(kartPrefab, driverPrefab, podium, def.tint, Application.isPlaying);

            if (nameLabel != null)
            {
                nameLabel.text = def.displayName.ToUpperInvariant();
                if (Application.isPlaying) UiMotion.Punch(nameLabel.transform, 1.08f, 0.25f);   // a new driver, said out loud
            }
            if (blurbLabel != null) blurbLabel.text = def.blurb;
            speedBar?.Set(def.speed);
            accelBar?.Set(def.accel);
            handlingBar?.Set(def.handling);
            weightBar?.Set(def.weight);
            turboBar?.Set(def.turbo);
            attackBar?.Set(def.attack);
            Changed?.Invoke(index);
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
