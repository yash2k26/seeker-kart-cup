using UnityEngine;

namespace KartCup
{
    /// The whole Beach-Buggy driver vocabulary on a Kenney Blocky Character (six rigid nodes, no bones):
    /// steer → arms, lateral G → torso roll, velocity → head yaw, hit → slump, finish → bounce.
    /// Base pose: sample the `drive` clip once (Animation component, clip set to play once and stop) or leave the rest pose.
    public sealed class BlockyDriverPose : MonoBehaviour
    {
        [SerializeField] Transform torso, armLeft, armRight, head;
        [SerializeField] Rigidbody kartBody;
        [SerializeField] float armSteerDeg = 35f, torsoRollDeg = 12f, headYawDeg = 25f, smooth = 12f;

        [Range(-1, 1)] public float Steer;            // set by KartHooks from UKR's input
        float steerS, rollS, yawS, hitT, winT;
        Quaternion tRest, alRest, arRest, hRest;
        Vector3 lastVel;

        void Awake()
        {
            tRest = torso.localRotation; alRest = armLeft.localRotation; arRest = armRight.localRotation; hRest = head.localRotation;
        }

        /// Spawned karts bind at runtime; the editor-built prefabs keep their serialized reference.
        public void Bind(Rigidbody body) => kartBody = body;

        public void OnHit()    => hitT = 1.2f;
        public void OnFinish() => winT = 3f;

        void LateUpdate()
        {
            float dt = Time.deltaTime, a = 1f - Mathf.Exp(-smooth * dt);
            Vector3 vel = kartBody ? kartBody.linearVelocity : Vector3.zero;
            Vector3 local = kartBody ? kartBody.transform.InverseTransformDirection(vel) : Vector3.zero;
            float lateralG = kartBody ? kartBody.transform.InverseTransformDirection((vel - lastVel) / Mathf.Max(dt, 1e-4f)).x / 9.81f : 0f;
            lastVel = vel;

            steerS = Mathf.Lerp(steerS, Steer, a);
            rollS  = Mathf.Lerp(rollS, Mathf.Clamp(-lateralG, -1f, 1f), a);
            yawS   = Mathf.Lerp(yawS, Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(local.z, 0.1f)) * Mathf.Rad2Deg / 45f, -1f, 1f), a);

            // hit: slump forward and drop the arms; finish: bounce with arms up
            float slump = 0f, bounce = 0f;
            if (hitT > 0) { hitT -= dt; slump = Mathf.Sin(Mathf.Clamp01(hitT / 1.2f) * Mathf.PI); }
            if (winT > 0) { winT -= dt; bounce = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * Mathf.Clamp01(winT); }

            torso.localRotation    = tRest * Quaternion.Euler(slump * 35f, 0f, rollS * torsoRollDeg);
            armLeft.localRotation  = alRest * Quaternion.Euler(-steerS * armSteerDeg - bounce * 120f + slump * 40f, 0f, 0f);
            armRight.localRotation = arRest * Quaternion.Euler( steerS * armSteerDeg - bounce * 120f + slump * 40f, 0f, 0f);
            head.localRotation     = hRest * Quaternion.Euler(slump * 30f, yawS * headYawDeg, 0f);
            if (bounce > 0f) torso.localPosition = new Vector3(torso.localPosition.x, 0.7f + bounce * 0.15f, torso.localPosition.z);
        }
    }
}
