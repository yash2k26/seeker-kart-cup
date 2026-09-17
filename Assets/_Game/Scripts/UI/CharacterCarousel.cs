using System.Linq;
using PowerslideKartPhysics;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The character select: a live kart and driver on the podium, swapped by the arrows. No render textures and no
    /// portraits to maintain — the thing you race is the thing you are looking at.
    public sealed class CharacterCarousel : MonoBehaviour
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Transform podium;
        [SerializeField] GameObject[] kartPrefabs;
        [SerializeField] GameObject[] driverPrefabs;
        [SerializeField] TMP_Text nameLabel, blurbLabel;
        [SerializeField] StatBar speedBar, accelBar, handlingBar, weightBar;
        [SerializeField] float spinSpeed = 18f;

        GameObject current;
        int index;

        void Start()
        {
            index = CharacterDatabase.SelectedIndex;
            Show();
        }

        /// Editor-side preview so the menu scene is not empty when it is opened or screenshotted.
        public void ShowInEditor(int i) { index = i; Show(); }

        void Update()
        {
            if (podium != null) podium.Rotate(0, spinSpeed * Time.deltaTime, 0, Space.World);
        }

        public void Next() { Step(+1); }
        public void Previous() { Step(-1); }

        void Step(int delta)
        {
            if (roster == null || roster.characters.Length == 0) return;
            index = ((index + delta) % roster.characters.Length + roster.characters.Length) % roster.characters.Length;
            CharacterDatabase.SelectedIndex = index;
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
            Show();
        }

        void Show()
        {
            var def = roster != null ? roster.At(index) : null;
            if (def == null) return;

            if (current != null) Destroy(current);
            var kartPrefab = kartPrefabs.FirstOrDefault(k => k.name.EndsWith(def.kartId)) ?? kartPrefabs.FirstOrDefault();
            if (kartPrefab != null && podium != null)
            {
                current = Instantiate(kartPrefab, podium);
                current.transform.localPosition = Vector3.zero;
                current.transform.localRotation = Quaternion.identity;
                // display only: no physics, no input, no race components on the podium
                foreach (var body in current.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
                foreach (var kart in current.GetComponentsInChildren<Kart>()) kart.enabled = false;
                foreach (var col in current.GetComponentsInChildren<Collider>()) col.enabled = false;

                var seat = current.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
                var driverPrefab = driverPrefabs.FirstOrDefault(d => d.name.EndsWith(def.driverId)) ?? driverPrefabs.FirstOrDefault();
                if (seat != null && driverPrefab != null)
                {
                    var driver = Instantiate(driverPrefab, seat);
                    driver.transform.localPosition = Vector3.zero;
                    driver.transform.localRotation = Quaternion.identity;
                }
            }

            if (nameLabel != null) nameLabel.text = def.displayName.ToUpperInvariant();
            if (blurbLabel != null) blurbLabel.text = def.blurb;
            speedBar?.Set(def.speed);
            accelBar?.Set(def.accel);
            handlingBar?.Set(def.handling);
            weightBar?.Set(def.weight);
        }
    }
}
