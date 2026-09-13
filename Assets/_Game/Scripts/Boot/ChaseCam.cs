using UnityEngine;

namespace KartCup
{
    /// Gate-scene helper: damped follow camera. UKR's KartCameraSwitcher replaces this in the real race scene.
    public sealed class ChaseCam : MonoBehaviour
    {
        [SerializeField] Transform target; [SerializeField] float distance = 5.2f, height = 2.1f, damp = 6f, lookHeight = 0.9f;
        void LateUpdate()
        {
            if (!target) return;
            var want = target.position - target.forward * distance + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-damp * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation((target.position + Vector3.up * lookHeight) - transform.position, Vector3.up);
        }
    }
}
