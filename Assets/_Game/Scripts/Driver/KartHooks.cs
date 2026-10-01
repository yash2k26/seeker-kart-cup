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
        KartPowers powers;
        ItemMount mount;
        int knownAmmo, castFrame = -1;

        public void Bind(Kart k, BlockyDriverPose pose, bool player)
        {
            kart = k; driver = pose; isPlayer = player; rb = k.rb;
            body = GetComponent<BodyJuice>();
            k.landEvent.AddListener(OnLand);
            k.boostStartEvent.AddListener(OnBoostStart);
            k.spinOutEvent.AddListener(OnSpinOut);
            k.collisionEvent.AddListener((point, normal) => OnCollision());

            caster = GetComponent<ItemCaster>();
            powers = GetComponent<KartPowers>();
            if (caster != null && player) caster.castEvent.AddListener(() => AudioDirector.Instance?.UseItem(caster.item != null ? caster.item.itemName : null));
            if (caster != null) caster.castEvent.AddListener(() => castFrame = Time.frameCount);
            mount = ItemMount.On(k);   // the pod and the cradle (before the listener above runs they have noted what was fired)
        }

        /// The projectile this kart just fired. A projectile does not say who fired it, so the one that appeared beside
        /// the kart on the frame of the cast is taken to be ours. It never touches its own kart (Powerslide ignores one
        /// collider; a kart has two, and a rocket born inside the second spun its own shooter), it starts from where
        /// the kart shows it (the pod's tubes, the cradle), and the ATTACK power makes it fly harder.
        void LateUpdate()
        {
            if (castFrame != Time.frameCount) return;
            foreach (var shot in FindObjectsByType<SpawnedProjectileItem>(FindObjectsSortMode.None))
            {
                if (shot.GetComponent<PoweredShot>() != null || (shot.transform.position - transform.position).sqrMagnitude > 25f) continue;
                shot.gameObject.AddComponent<PoweredShot>();
                var srb = shot.GetComponent<Rigidbody>();
                bool moving = srb != null && rb != null && (srb.linearVelocity - rb.linearVelocity).sqrMagnitude > 4f;   // thrown or fired, not dropped
                if (moving)
                {
                    foreach (var mine in GetComponentsInChildren<Collider>())
                        foreach (var its in shot.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(mine, its, true);
                    if (mount != null && mount.LaunchPoint(out var from)) { shot.transform.position = from; srb.position = from; }
                }
                if (powers == null || Mathf.Abs(powers.attack - 1f) < 0.01f) continue;
                shot.targetSpeed *= powers.attack; shot.accel *= powers.attack;
                if (srb != null && rb != null) srb.linearVelocity = rb.linearVelocity + (srb.linearVelocity - rb.linearVelocity) * powers.attack;
            }
        }

        void Update()
        {
            if (kart == null) return;
            if (driver != null) driver.Steer = kart.GetVisualSteer();

            // the item box has no event of its own: notice the slot filling instead
            if (caster != null)
            {
                if (caster.ammo > knownAmmo)
                {
                    if (isPlayer) AudioDirector.Instance?.Pickup(caster.item != null ? caster.item.itemName : null);
                    if (powers != null && powers.bonusAmmo > 0) caster.ammo += powers.bonusAmmo;   // ATTACK: a shot on the house
                }
                knownAmmo = caster.ammo;
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
            AudioDirector.Instance?.Hit();
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
