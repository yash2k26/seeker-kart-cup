using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// What a kart shows of the item it holds, the way the film does it. A rocket (or a homing one) brings the pod up
    /// off the flank beside the driver's head, and the shot leaves its tubes with a flash and a kick; a bomb or a sun
    /// bomb rides in the cradle on the other flank until it is thrown. Built by ItemSetup as Resources/ItemMount;
    /// RaceSpawner hangs one on every kart that can hold an item.
    public sealed class ItemMount : MonoBehaviour
    {
        public Transform pod, strut, muzzle, cradle;
        public GameObject sunBomb, bomb;
        public ParticleSystem flash;
        public Light flashLight;

        ItemCaster caster;
        Vector3 stow, up;
        float u, kick, hold, pop, flashFade;
        string held, fired;

        public static ItemMount On(Kart kart)
        {
            if (kart == null || kart.GetComponent<ItemCaster>() == null) return null;
            var prefab = Resources.Load<GameObject>("ItemMount");
            if (prefab == null) return null;
            var parent = kart.rotator != null ? kart.rotator : kart.transform;     // lean with the body
            var mount = Instantiate(prefab, parent).GetComponent<ItemMount>();
            mount.transform.localPosition = Vector3.zero; mount.transform.localRotation = Quaternion.identity;
            mount.Init(kart, parent);
            return mount;
        }

        void Init(Kart kart, Transform parent)
        {
            caster = kart.GetComponent<ItemCaster>();
            // the flanks and the deck, from the kart's own box: thirteen bodies, one rule
            float half = 1.1f, top = 0.4f;
            var box = kart.GetComponentInChildren<BoxCollider>();
            if (box != null)
            {
                var corner = parent.InverseTransformPoint(box.transform.TransformPoint(box.center + box.size * 0.5f));
                half = Mathf.Abs(corner.x); top = corner.y;
            }
            stow = new Vector3(-(half + 0.12f), top - 0.05f, -0.25f);       // lying on its side, low on the left flank
            up = new Vector3(-(half - 0.02f), top + 0.95f, -0.05f);         // upright beside the driver's head
            cradle.localPosition = new Vector3(half + 0.30f, top + 0.42f, 0.05f);
            caster.castEvent.AddListener(OnCast);
            Apply();
        }

        void OnCast()
        {
            fired = held;
            if (fired == "Rocket" || fired == "Homing Item")
            {
                kick = 1f; hold = 0.5f; u = 1f;
                if (flash != null) flash.Play();
                flashFade = 1f;
            }
        }

        /// Where the shot the kart just fired should start from: the pod's tubes for a rocket, the cradle for a sun
        /// bomb. False for anything that leaves the kart some other way (a dropped bomb, a boost).
        public bool LaunchPoint(out Vector3 point)
        {
            point = default;
            if (fired == "Rocket" || fired == "Homing Item") { point = muzzle.position; return true; }
            if (fired == "Sun Bomb") { point = cradle.position + Vector3.up * 0.25f; return true; }
            return false;
        }

        void Update()
        {
            if (caster == null) return;
            held = caster.item != null && caster.ammo > 0 ? caster.item.itemName : null;
            bool rocket = held == "Rocket" || held == "Homing Item";
            hold -= Time.deltaTime;
            u = Mathf.MoveTowards(u, rocket || hold > 0f ? 1f : 0f, Time.deltaTime / 0.16f);      // up in a sixth of a second
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime / 0.22f);
            bool carried = held == "Sun Bomb" || held == "Bomb";
            pop = Mathf.MoveTowards(pop, carried ? 1f : 0f, Time.deltaTime / 0.12f);
            if (sunBomb != null) sunBomb.SetActive(held == "Sun Bomb");
            if (bomb != null) bomb.SetActive(held == "Bomb");
            if (flashLight != null) { flashFade = Mathf.MoveTowards(flashFade, 0f, Time.deltaTime / 0.12f); flashLight.intensity = 60f * flashFade; flashLight.enabled = flashFade > 0f; }
            Apply();
        }

        void Apply()
        {
            // rises off the flank and rolls upright, a little past and back (the clack)
            float e = u < 0.75f ? Mathf.SmoothStep(0f, 1.1f, u / 0.75f) : Mathf.Lerp(1.1f, 1f, (u - 0.75f) / 0.25f);
            float roll = -90f * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.3f) / 0.7f));
            pod.gameObject.SetActive(u > 0.001f);
            pod.localPosition = Vector3.LerpUnclamped(stow, up, e) - Vector3.forward * (0.12f * kick);
            pod.localRotation = Quaternion.Euler(-9f * kick, 0f, roll);
            if (strut != null) strut.localScale = new Vector3(1f, Mathf.Max(0.05f, Mathf.Clamp01(u)), 1f);
            cradle.gameObject.SetActive(pop > 0.001f);
            cradle.localScale = Vector3.one * (pop < 0.8f ? pop / 0.8f * 1.12f : Mathf.Lerp(1.12f, 1f, (pop - 0.8f) / 0.2f));
        }
    }
}
