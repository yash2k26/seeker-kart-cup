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
        int knownAmmo;

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
            mount = ItemMount.On(k);   // the pod and the cradle: their own listener notes what was fired, before ours below runs
            if (caster != null) caster.castEvent.AddListener(OnCast);
        }

        /// The projectile this kart just fired. A projectile does not say who fired it, so the one that appeared beside
        /// the kart on the frame of the cast is taken to be ours. It never touches its own kart (Powerslide ignores one
        /// collider; a kart has two, and a rocket born inside the second spun its own shooter), it starts from where
        /// the kart shows it (the pod's tubes, the cradle), and the ATTACK power makes it fly harder.
        System.Collections.IEnumerator Rearm(Collider[] mine, Collider[] its)
        {
            yield return new WaitForSeconds(1.5f);
            foreach (var a in mine)
                foreach (var b in its) if (a != null && b != null) Physics.IgnoreCollision(a, b, false);
        }

        /// Runs as the cast happens (the shot is already in the scene when Powerslide raises the event), not a frame
        /// later: a physics step in between is all a trap needs to hit the kart it was dropped under.
        void OnCast()
        {
            foreach (var shot in FindObjectsByType<SpawnedProjectileItem>(FindObjectsSortMode.None))
            {
                if (shot.GetComponent<PoweredShot>() != null || (shot.transform.position - transform.position).sqrMagnitude > 25f) continue;
                shot.gameObject.AddComponent<PoweredShot>();
                var srb = shot.GetComponent<Rigidbody>();
                bool moving = srb != null && rb != null && (srb.linearVelocity - rb.linearVelocity).sqrMagnitude > 4f;   // thrown or fired, not dropped
                // Nothing a kart lets go of may hit that kart on its way out. Powerslide spares one of the kart's
                // colliders for half a second; ours have several, so a trap dropped under the kart spun out the one
                // who dropped it. What is fired never turns on its own kart; what is dropped (a trap, a bomb) is
                // live for it again a second and a half later, by when the kart is long gone.
                var mineAll = GetComponentsInChildren<Collider>(); var itsAll = shot.GetComponentsInChildren<Collider>();
                foreach (var mine in mineAll)
                    foreach (var its in itsAll) Physics.IgnoreCollision(mine, its, true);
                if (moving) { if (mount != null && mount.LaunchPoint(out var from)) { shot.transform.position = from; srb.position = from; } }
                else StartCoroutine(Rearm(mineAll, itsAll));
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
