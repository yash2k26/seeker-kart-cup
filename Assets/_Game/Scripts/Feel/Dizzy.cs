using UnityEngine;

namespace KartCup
{
    /// Three stars circling a hit driver's head (Beach Buggy's cartoon read of "stunned"), turned to face the lens.
    /// Spawned by KartHooks on a spin-out and gone when the spin is over.
    public sealed class Dizzy : MonoBehaviour
    {
        const float Radius = 0.55f, Turn = 300f;
        Transform[] stars;
        float until, born;

        /// Over this kart's driver for `seconds`. The ring rides the kart's root, which does not spin with the body.
        public static void On(Transform kart, float seconds)
        {
            var prefab = Resources.Load<GameObject>("FX/Dizzy");
            if (prefab == null || kart == null) return;
            var old = kart.GetComponentInChildren<Dizzy>();
            if (old != null) { old.until = Mathf.Max(old.until, Time.time + seconds); return; }
            float top = 1.6f;                                  // above the tallest thing the kart carries: the driver's head
            foreach (var r in kart.GetComponentsInChildren<Renderer>())
                if (r is MeshRenderer or SkinnedMeshRenderer) top = Mathf.Max(top, r.bounds.max.y - kart.position.y);
            var d = Instantiate(prefab, kart).GetComponent<Dizzy>();
            d.transform.localPosition = Vector3.up * (Mathf.Min(top, 3f) + 0.25f);
            d.until = Time.time + seconds;
        }

        void Awake()
        {
            stars = new Transform[transform.childCount];
            for (int i = 0; i < stars.Length; i++) stars[i] = transform.GetChild(i);
            born = Time.time;
        }

        void LateUpdate()
        {
            if (Time.time > until) { Destroy(gameObject); return; }
            var cam = Camera.main;
            float spin = (Time.time - born) * Turn;
            float fade = Mathf.Clamp01((until - Time.time) / 0.3f) * Mathf.Clamp01((Time.time - born) / 0.15f);
            for (int i = 0; i < stars.Length; i++)
            {
                float a = (spin + i * 360f / stars.Length) * Mathf.Deg2Rad;
                stars[i].position = transform.position + new Vector3(Mathf.Cos(a) * Radius, Mathf.Sin(a * 2f) * 0.08f, Mathf.Sin(a) * Radius);
                if (cam != null) stars[i].rotation = Quaternion.LookRotation(stars[i].position - cam.transform.position);
                stars[i].localScale = Vector3.one * 0.42f * fade;
            }
        }
    }
}
