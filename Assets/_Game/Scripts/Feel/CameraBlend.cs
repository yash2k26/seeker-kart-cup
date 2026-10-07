using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Eases the race camera from where a shot left it (the drone's lap) onto its rig's chase position, instead of a cut.
    /// The rig (KartCamera) keeps computing its pose in physics steps; this lays the blend over it each frame.
    public sealed class CameraBlend : MonoBehaviour
    {
        Vector3 fromPos; Quaternion fromRot;
        float start = -1f, seconds = 0.8f;
        KartCamera rig;

        public static void From(Camera cam, Vector3 pos, Quaternion rot, float seconds)
        {
            if (cam == null) return;
            var b = cam.GetComponent<CameraBlend>();
            if (b == null) b = cam.gameObject.AddComponent<CameraBlend>();
            b.fromPos = pos; b.fromRot = rot; b.seconds = Mathf.Max(0.05f, seconds);
            b.rig = cam.GetComponent<KartCamera>();
            b.start = Time.unscaledTime;
        }

        void LateUpdate()
        {
            if (start < 0f || rig == null) return;
            float k = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
            if (k >= 1f) { start = -1f; return; }
            float e = k * k * (3f - 2f * k);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, rig.targetPos, e), Quaternion.Slerp(fromRot, rig.targetRot, e));
        }
    }
}
