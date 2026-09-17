using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Glue between one Powerslide kart and our feel layer: driver pose, body squash, camera kick, haptics, hit-stop.
    /// Subscribes to the kart's own UnityEvents in code — no Inspector wiring, no reflection.
    public sealed class KartHooks : MonoBehaviour
    {
        public bool isPlayer;
        [SerializeField] BlockyDriverPose driver;
        [SerializeField] BodyJuice body;

        Kart kart;
        Rigidbody rb;
        ItemCaster caster;
        Item heldItem;

        public void Bind(Kart k, BlockyDriverPose pose, bool player)
        {
            kart = k; driver = pose; isPlayer = player; rb = k.rb;
            body = GetComponent<BodyJuice>();
            k.landEvent.AddListener(OnLand);
            k.boostStartEvent.AddListener(OnBoostStart);
            k.spinOutEvent.AddListener(OnSpinOut);
            k.collisionEvent.AddListener((point, normal) => OnCollision());

            caster = GetComponent<ItemCaster>();
            if (caster != null && player) caster.castEvent.AddListener(() => AudioDirector.Instance?.UseItem());
        }

        void Update()
        {
            if (kart == null) return;
            if (driver != null) driver.Steer = kart.GetVisualSteer();

            // the item box has no event of its own: notice the slot filling instead
            if (isPlayer && caster != null && caster.item != heldItem)
            {
                if (caster.item != null && heldItem == null) AudioDirector.Instance?.Pickup();
                heldItem = caster.item;
            }
            if (isPlayer && CameraFeel.Instance != null && rb != null)
                CameraFeel.Instance.SetSpeed01(rb.linearVelocity.magnitude / Mathf.Max(1f, kart.maxSpeed));
        }

        public void OnBoostStart()
        {
            if (!isPlayer) return;
            CameraFeel.Instance?.Boost(2.5f);
            Haptics.Play(Haptics.Kind.Boost);
        }

        public void OnLand()
        {
            float impact = rb != null ? Mathf.Clamp01(-rb.linearVelocity.y / 8f) : 0.5f;
            body?.Kick(-impact * 0.4f);
            if (isPlayer && impact > 0.3f) { Haptics.Play(Haptics.Kind.Land); CameraFeel.Instance?.Kick(Vector3.down, impact); }
        }

        public void OnCollision()
        {
            body?.Kick(-0.15f);
            if (isPlayer) { Haptics.Play(Haptics.Kind.Hit); CameraFeel.Instance?.Kick(-transform.forward, 0.4f); }
        }

        public void OnSpinOut()
        {
            driver?.OnHit();
            body?.Kick(-0.3f);
            if (!isPlayer) return;
            HitStop.Do();
            Haptics.Play(Haptics.Kind.Hit);
            CameraFeel.Instance?.Kick(-transform.forward, 1f);
        }

        public void OnFinish()
        {
            driver?.OnFinish();
            if (isPlayer) Haptics.Play(Haptics.Kind.Win);
        }
    }
}
