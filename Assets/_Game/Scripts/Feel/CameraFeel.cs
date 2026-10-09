using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// FOV kick on boost + a small directional shake on hits. Attach to UKR's race camera (KartCameraSwitcher lives there; no Cinemachine).
    /// Unity FOV is vertical: 62° vertical ≈ 107° horizontal on the Seeker's 20:9 panel.
    public sealed class CameraFeel : MonoBehaviour
    {
        public static CameraFeel Instance { get; private set; }

        [Header("FOV")]
        [SerializeField] float baseFov = 56f;
        [SerializeField] float boostAdd = 7f;
        [SerializeField] float speedAdd = 3f;
        [SerializeField] float lerpSpeed = 8f;

        [Header("Shake")]
        [SerializeField] float maxShake = 0.25f;
        [SerializeField] float shakeDecay = 6f;
        [Range(0, 1)] public float shakeGain = 1f;   // settings toggle: 0 = off
        // Beach Buggy's chase camera (10 Oct 2026, the user: "moving like a drone shot"): it trails to the outside of a
        // corner, so the kart is seen a little side-on, and banks into it, then settles behind on the straight.
        [SerializeField] float swingPerDeg = 0.17f, swingMax = 17f, leanPerDeg = 0.04f, leanMax = 4f, follow = 3.2f;
        float yawRate, swing, lean;
        Quaternion rigRot, leanedRot;
        KartCamera rig;
        /// The finish line's own swing (KartHooks.Swing) takes the orbit over.
        public bool HoldSwing { get; set; }
        /// The player's kart's turn rate, degrees a second (positive: to the right).
        public void Turn(float degreesPerSecond) => yawRate = degreesPerSecond;

        Camera cam;
        Vector3 shakeVel, shakeOffset, rigPos, shakenPos;

        public float BaseFov { get => baseFov; set => baseFov = value; }
        float boostUntil, speed01;

        void Awake() { Instance = this; cam = GetComponent<Camera>(); shakeGain = Settings.Shake ? 1f : 0f; }

        public void SetSpeed01(float s) => speed01 = Mathf.Clamp01(s);
        public void Boost(float seconds = 3f) => boostUntil = Time.time + seconds;
        public bool Boosting => Time.time < boostUntil;
        public void Kick(Vector3 worldDir, float strength = 1f) => shakeVel += transform.InverseTransformDirection(worldDir.normalized) * (maxShake * strength * shakeGain);

        void LateUpdate()
        {
            float target = baseFov + (Time.time < boostUntil ? boostAdd : 0f) + speed01 * speedAdd;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime));

            // damped spring on a local offset; UKR positions the camera, we add on top after it
            shakeVel += (-80f * shakeOffset - shakeDecay * shakeVel) * Time.unscaledDeltaTime;
            shakeOffset += shakeVel * Time.unscaledDeltaTime;
            // The rig (KartCamera) moves the camera in physics steps, not every frame. On a frame it did not move it, the
            // position is still last frame's shaken one: shaking that again doubled the offset, and stacked it.
            if (transform.position != shakenPos) rigPos = transform.position;
            transform.position = rigPos + transform.TransformDirection(shakeOffset);
            shakenPos = transform.position;

            float k = 1f - Mathf.Exp(-follow * Time.deltaTime);
            swing = Mathf.Lerp(swing, Mathf.Clamp(yawRate * swingPerDeg, -swingMax, swingMax), k);
            lean = Mathf.Lerp(lean, Mathf.Clamp(-yawRate * leanPerDeg, -leanMax, leanMax), k);
            if (!HoldSwing)
            {
                if (rig == null) rig = FindFirstObjectByType<KartCamera>();
                if (rig != null) { rig.orbitOverride = true; rig.orbitDegrees = swing; }   // 0 = behind, positive = out to the kart's left
            }
            // the bank, on top of whatever the rig set (the same once-per-rig-move care as the shake)
            if (transform.rotation != leanedRot) rigRot = transform.rotation;
            transform.rotation = rigRot * Quaternion.Euler(0f, 0f, HoldSwing ? 0f : lean);
            leanedRot = transform.rotation;
        }
    }
}
