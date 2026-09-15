using UnityEngine;

namespace KartCup
{
    /// Slow camera drift over the menu vista so the title screen is never a still image.
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float radius = 9f, height = 3.4f, speed = 4f, look = 1.1f;
        [SerializeField] float yawOffset = 13f;   // swings the subject into the right half, clear of the title text
        float angle;

        void LateUpdate()
        {
            if (target == null) return;
            angle += speed * Time.deltaTime;
            float a = angle * Mathf.Deg2Rad;
            transform.position = target.position + new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius);
            transform.LookAt(target.position + Vector3.up * look);
            transform.rotation *= Quaternion.Euler(0, -yawOffset, 0);
        }
    }
}
