using UnityEngine;

namespace KartCup
{
    /// Small reactions layered on top of the driver's seated animation: the hands turn the wheel with the steering,
    /// and a hit slumps them briefly.
    ///
    /// The clip only animates root, legs and arms — not torso or head. Writing to a bone the clip never touches
    /// and then reading it back the next frame compounds the offset, which spun the driver on the seat. So this
    /// only ever composes onto bones the clip rewrites every frame.
    public sealed class BlockyDriverPose : MonoBehaviour
    {
        [SerializeField] Transform torso, armLeft, armRight, head, wheel;
        [SerializeField] Rigidbody kartBody;
        [SerializeField] float armSteerDeg = 18f, smooth = 10f;
        // Measured off each rig by ContentSetup: the arm rotation sign that lifts the left fist (a right turn turns
        // the wheel clockwise), and the wheel's turn per degree of arm swing that keeps the rim under the hands.
        [SerializeField] float armTurn = 1f, wheelTurn = 1f;
        Quaternion wheelRest;

        [Range(-1, 1)] public float Steer;           // set by KartHooks from the kart's visual steer
        public Transform Wheel => wheel;              // the podium hides it: nobody holds a trophy and a steering wheel
        float steerSmoothed, hitT, winT;

        /// Spawned karts bind at runtime; the editor-built prefabs keep their serialized reference.
        public void Bind(Rigidbody body) => kartBody = body;

        public void OnHit() => hitT = 1.1f;
        public void OnFinish() => winT = 2.5f;

        void Awake() { if (wheel != null) wheelRest = wheel.localRotation; }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            steerSmoothed = Mathf.Lerp(steerSmoothed, Mathf.Clamp(Steer, -1f, 1f), 1f - Mathf.Exp(-smooth * dt));
            hitT = Mathf.Max(0f, hitT - dt);
            winT = Mathf.Max(0f, winT - dt);

            float lean = steerSmoothed * armSteerDeg * armTurn;
            float slump = hitT > 0f ? Mathf.Sin(hitT * 12f) * 22f : 0f;
            float cheer = winT > 0f ? Mathf.Abs(Mathf.Sin(winT * 8f)) * 40f : 0f;

            // composed onto the clip's value, which is rewritten every frame — never accumulates
            if (armLeft != null) armLeft.localRotation *= Quaternion.Euler(-cheer, 0f, lean + slump);
            if (armRight != null) armRight.localRotation *= Quaternion.Euler(-cheer, 0f, lean - slump);
            // the clip never touches the wheel, so it is set from its rest pose rather than composed
            if (wheel != null) wheel.localRotation = wheelRest * Quaternion.Euler(0f, 0f, -steerSmoothed * armSteerDeg * wheelTurn);
        }
    }
}
