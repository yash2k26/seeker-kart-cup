using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Streaks round the screen's edges while the player's kart is boosting (drift release, turbo pad, nitro): Beach
    /// Buggy blurs the edges on every boost. A full-screen image whose middle is clear, flickered a little in scale and
    /// angle so the lines rush instead of sitting still.
    [RequireComponent(typeof(Image))]
    public sealed class SpeedLines : MonoBehaviour
    {
        [SerializeField] float strength = 0.55f;
        Image img;
        Kart player;
        float alpha, nextJitter;

        void Awake() { img = GetComponent<Image>(); img.raycastTarget = false; SetAlpha(0f); }

        void Update()
        {
            if (player == null) { var p = RaceManager.Instance != null ? RaceManager.Instance.Player : null; if (p != null) player = p.GetComponent<Kart>(); }
            // any boost start (drift release, pad, nitro, turbo: CameraFeel's window) or boost still being spent
            bool boosting = (CameraFeel.Instance != null && CameraFeel.Instance.Boosting) || (player != null && player.boostReserve > 0f);
            bool on = player != null && boosting && player.velMag > 8f && Time.timeScale > 0f
                      && !(PodiumShow.Instance != null && PodiumShow.Instance.Active);   // never over the ceremony (the kart cruises on under it)
            alpha = Mathf.MoveTowards(alpha, on ? strength : 0f, Time.unscaledDeltaTime * (on ? 4f : 1.6f));
            SetAlpha(alpha);
            if (alpha <= 0.001f || Time.unscaledTime < nextJitter) return;
            nextJitter = Time.unscaledTime + 0.045f;
            transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-2.5f, 2.5f));
            transform.localScale = Vector3.one * Random.Range(1.0f, 1.07f);
        }

        void SetAlpha(float a) { var c = img.color; c.a = a; img.color = c; img.enabled = a > 0.001f; }
    }
}
