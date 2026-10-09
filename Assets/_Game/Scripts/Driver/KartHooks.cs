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
        bool wasDrifting; int driftTier;
        Vector3 lastFwd;

        public void Bind(Kart k, BlockyDriverPose pose, bool player)
        {
            kart = k; driver = pose; isPlayer = player; rb = k.rb;
            body = GetComponentInChildren<BodyJuice>();
            if (body == null) body = BodyJuice.Wrap(k);   // (Unity's null: never ?? on a component lookup)
            WheelDust.On(k);
            k.landEvent.AddListener(OnLand);
            k.boostStartEvent.AddListener(OnBoostStart);
            k.spinOutEvent.AddListener(OnSpinOut);
            k.collisionEvent.AddListener((point, normal) => OnCollision());

            caster = GetComponent<ItemCaster>();
            powers = GetComponent<KartPowers>();
            if (caster != null && player) caster.castEvent.AddListener(() => { AudioDirector.Instance?.UseItem(caster.item != null ? caster.item.itemName : null); Achievements.Add(Achievements.Stat.ItemsUsed); });
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
        /// An ability fired an item straight through its Item (Ability.Cast): the same care as a cast.
        public void AfterCast() => OnCast();

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
            // a drift let go with at least one tier charged fires a boost (Achievements counts them)
            if (isPlayer && kart.drifting && !wasDrifting) Haptics.Play(Haptics.Kind.Tick);   // the drift catching, felt
            if (isPlayer) { if (wasDrifting && !kart.drifting && driftTier > 0) Achievements.Add(Achievements.Stat.DriftBoosts); driftTier = kart.drifting ? kart.boostCount : 0; wasDrifting = kart.drifting; }
            if (isPlayer && CameraFeel.Instance != null && rb != null)
                CameraFeel.Instance.SetSpeed01(rb.linearVelocity.magnitude / Mathf.Max(1f, kart.maxSpeed));
            if (isPlayer && CameraFeel.Instance != null)
            {
                var heading = kart.rotator != null ? kart.rotator : kart.transform;
                var fwd = Vector3.ProjectOnPlane(heading.forward, Vector3.up);
                if (lastFwd != Vector3.zero && Time.deltaTime > 0f) CameraFeel.Instance.Turn(Vector3.SignedAngle(lastFwd, fwd, Vector3.up) / Time.deltaTime);
                lastFwd = fwd;
            }
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

        static float lastHurt = -10f;
        // the driver riding this kart (Driver_<Name>, spawned as a clone): whose voice the barks are in
        string DriverId => driver != null ? driver.gameObject.name.Replace("(Clone)", "").Replace("Driver_", "").Trim() : Voices.Player;

        public void OnSpinOut()
        {
            driver?.OnHit();
            body?.Kick(-0.3f);
            // Beach Buggy's hit: a struck kart (a rocket flips it, a bouncer rolls it, a bomb's three turns) is thrown up and
            // lands hard, under a puff of smoke, stars round the driver's head. A slip (oil, a trap's two turns) stays down.
            bool struck = kart.lastSpinAxis != Kart.SpinAxis.Yaw || kart.lastSpinCount >= 3;
            if (struck) StartCoroutine(Throw());
            var puff = Resources.Load<GameObject>("FX/HitPuff");
            if (puff != null) Instantiate(puff, transform.position + Vector3.up * 0.9f, Quaternion.identity, transform);   // rides with the kart
            Dizzy.On(transform, struck ? 2.2f : 1.3f);
            if (!isPlayer) return;
            HitStop.Do();
            AudioDirector.Instance?.Hit();
            if (Time.unscaledTime - lastHurt > 4f) { lastHurt = Time.unscaledTime; Voices.Say(DriverId, "hurt"); }   // a bark, not a commentary
            Haptics.Play(Haptics.Kind.Hit);
            CameraFeel.Instance?.Kick(-transform.forward, 1f);
        }

        /// One physics step after the hit, so a Sun Bomb's own knock (applied right after its spin-out) is not doubled.
        System.Collections.IEnumerator Throw()
        {
            yield return new WaitForFixedUpdate();
            if (rb != null && rb.linearVelocity.y < 2f) rb.AddForce(Vector3.up * 5.5f, ForceMode.VelocityChange);
        }

        /// The finish line's camera: round from behind the kart to in front of it, to the driver's face.
        public void SwingCamera() => StartCoroutine(Swing());

        System.Collections.IEnumerator Swing()
        {
            var kc = FindFirstObjectByType<KartCamera>();
            if (kc == null) yield break;
            if (CameraFeel.Instance != null) CameraFeel.Instance.HoldSwing = true;   // the corner swing gives the orbit up
            kc.orbitOverride = true;
            for (float t = 0f; t < 1.8f; t += Time.deltaTime)
            {
                kc.orbitDegrees = Mathf.SmoothStep(0f, 155f, t / 1.8f);
                yield return null;
            }
            kc.orbitDegrees = 155f;
        }

        void SayWin() => Voices.Say(DriverId, "win");

        public void OnFinish()
        {
            driver?.OnFinish();
            if (isPlayer && RaceManager.Instance != null && RaceManager.Instance.PositionOf(GetComponent<KartProgress>()) == 1)
                Invoke(nameof(SayWin), 1.6f);                // after the announcer's YOU WIN
            if (isPlayer) Haptics.Play(Haptics.Kind.Win);
        }
    }
}
