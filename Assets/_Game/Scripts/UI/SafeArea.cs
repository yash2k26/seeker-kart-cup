using UnityEngine;

namespace KartCup
{
    /// Anchors a RectTransform to Screen.safeArea (punch-hole camera on the Seeker's short edge in landscape).
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;

        void Update()
        {
            if (Screen.safeArea == last) return;
            last = Screen.safeArea;
            var rt = (RectTransform)transform;
            var min = last.position; var max = last.position + last.size;
            min.x /= Screen.width; min.y /= Screen.height; max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
