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
        }
    }
}
