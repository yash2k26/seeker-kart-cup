using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace KartCup
{
    /// Glue between Ultimate Kart Racing and our feel scripts. Wire UKR's UnityEvents (jumpEvent, landEvent, boostStartEvent,
    /// collisionEvent, spinOutEvent) to these methods in the Inspector; steer is read from UKR's InputManager by name once.
    public sealed class KartHooks : MonoBehaviour
    {
        [SerializeField] bool isPlayer;
        [SerializeField] BlockyDriverPose driver;
        [SerializeField] BodyJuice body;
        [SerializeField] Rigidbody rb;
        [SerializeField] float topSpeed = 30f;        // UKR Kart.maxSpeed
        [SerializeField] MonoBehaviour ukrInputManager;   // drag UKR's InputManager (only for the player kart)
        [SerializeField] UnityEvent onPlayerHit = new();

        FieldInfo steerField; object steerOwner;

        void Start()
        {
            // UKR's InputManager exposes `steerInput`; resolve it once by name so we do not hard-reference the paid package.
            if (ukrInputManager)
            {
                steerField = ukrInputManager.GetType().GetField("steerInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                          ?? ukrInputManager.GetType().GetField("steerInput", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                steerOwner = steerField != null && steerField.IsStatic ? null : ukrInputManager;
                if (steerField == null) Debug.LogWarning("[KartHooks] steerInput field not found on " + ukrInputManager.GetType().Name);
            }
        }

        void Update()
        {
            if (driver && steerField != null) driver.Steer = Convert.ToSingle(steerField.GetValue(steerOwner));
            if (isPlayer && CameraFeel.Instance && rb) CameraFeel.Instance.SetSpeed01(rb.linearVelocity.magnitude / topSpeed);
        }

        // ---- UKR UnityEvent targets ----
        public void OnBoostStart()
        {
            if (!isPlayer) return;
            CameraFeel.Instance?.Boost(3f);
            Haptics.Play(Haptics.Kind.Boost);
        }

        public void OnLand()
        {
            float impact = rb ? Mathf.Clamp01(-rb.linearVelocity.y / 8f) : 0.5f;
            body?.Kick(-impact * 0.4f);
            if (isPlayer && impact > 0.3f) { Haptics.Play(Haptics.Kind.Land); CameraFeel.Instance?.Kick(Vector3.down, impact); }
        }

        public void OnCollision()
        {
            body?.Kick(-0.15f);
            if (isPlayer) { Haptics.Play(Haptics.Kind.Hit); CameraFeel.Instance?.Kick(-transform.forward, 0.5f); }
        }

        public void OnSpinOut()
        {
            driver?.OnHit();
            body?.Kick(-0.3f);
            if (!isPlayer) return;
            HitStop.Do();
            Haptics.Play(Haptics.Kind.Hit);
            CameraFeel.Instance?.Kick(-transform.forward, 1f);
            onPlayerHit?.Invoke();
        }

        public void OnFinish()
        {
            driver?.OnFinish();
            if (isPlayer) Haptics.Play(Haptics.Kind.Win);
        }
    }
}
