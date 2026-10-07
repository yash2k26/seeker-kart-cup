using UnityEngine;

namespace KartCup
{
    /// An item box drawn the Beach Buggy way: a see-through bubble with a question mark, always facing the driver,
    /// bobbing over the road. Powerslide's ItemGiver hides the box's renderer while it cools down after a pickup;
    /// the moment it goes out, the bubble pops (stars and a ring), and it comes back the same way it left.
    [RequireComponent(typeof(Renderer))]
    public sealed class ItemBubble : MonoBehaviour
    {
        Renderer rend;
        Vector3 home, baseScale;
        float phase;
        bool wasOn = true;
        float back = -1f;

        void Awake()
        {
            rend = GetComponent<Renderer>();
            home = transform.position; baseScale = transform.localScale;
            phase = (home.x * 0.37f + home.z * 0.19f) % (Mathf.PI * 2f);   // neighbours bob out of step
        }

        void LateUpdate()
        {
            bool on = rend.enabled;
            if (wasOn && !on) Pop();
            if (!wasOn && on) back = Time.time;                 // returning: grows back in
            wasOn = on;

            float t = Time.time * 2.1f + phase;
            transform.position = home + Vector3.up * (Mathf.Sin(t) * 0.16f);
            float grow = back < 0f ? 1f : Mathf.Clamp01((Time.time - back) / 0.25f);
            transform.localScale = baseScale * (1f + Mathf.Sin(t * 1.7f) * 0.035f) * Mathf.SmoothStep(0.2f, 1f, grow);
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        void Pop()
        {
            var fx = Resources.Load<GameObject>("FX/BubblePop");
            if (fx != null) Instantiate(fx, transform.position, Quaternion.identity);
        }
    }
}
