using UnityEngine;

/// tiny helpers the gate scene needs
namespace KartCup
{
    public sealed class Spin : MonoBehaviour { void Update() => transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World); }

    public sealed class TextRelay : MonoBehaviour
    {
        [SerializeField] UnityEngine.UI.Text label;
        public void Set(string s) { if (label) label.text = s; }
    }

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
