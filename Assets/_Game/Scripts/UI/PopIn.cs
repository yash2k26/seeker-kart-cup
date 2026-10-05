using PrimeTween;
using UnityEngine;

namespace KartCup
{
    /// A group's members arrive one after another when its screen opens: each pops from a little smaller with an
    /// overshoot, 45 ms apart, so a menu assembles itself instead of blinking on.
    public sealed class PopIn : MonoBehaviour
    {
        [SerializeField] float gap = 0.045f, delay = 0.08f, from = 0.82f;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            int k = 0;
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf) continue;
                Tween.StopAll(child);
                child.localScale = Vector3.one * from;
                Tween.Scale(child, 1f, 0.34f, Ease.OutBack, startDelay: delay + gap * k++, useUnscaledTime: true);
            }
        }
    }
}
