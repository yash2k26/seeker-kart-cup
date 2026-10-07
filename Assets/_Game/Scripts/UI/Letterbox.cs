using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Cinema bars over the drone's lap before the race (Beach Buggy letterboxes its fly-through): two black bands that
    /// slide in at the top and bottom and slide away as the camera lands behind the grid. Built at runtime, its own
    /// canvas under every other overlay, so the lap's title still draws over it.
    public sealed class Letterbox : MonoBehaviour
    {
        const float Share = 0.11f;                            // of the screen's height, each bar
        static Letterbox instance;
        RectTransform top, bottom;
        float amount, target;

        public static void Show(bool on)
        {
            if (instance == null)
            {
                if (!on) return;
                var go = new GameObject("Letterbox", typeof(Canvas), typeof(CanvasScaler));
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -20;
                instance = go.AddComponent<Letterbox>();
                instance.top = Bar(go.transform, 1f); instance.bottom = Bar(go.transform, 0f);
            }
            instance.target = on ? 1f : 0f;
        }

        static RectTransform Bar(Transform parent, float y)
        {
            var rt = new GameObject(y > 0.5f ? "Top" : "Bottom", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y); rt.pivot = new Vector2(0.5f, y);
            var img = rt.GetComponent<Image>(); img.color = Color.black; img.raycastTarget = false;
            return rt;
        }

        void LateUpdate()
        {
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / 0.35f);
            float h = Screen.height * Share * amount * (1f / Mathf.Max(0.01f, GetComponent<Canvas>().scaleFactor));
            top.sizeDelta = new Vector2(0f, h); bottom.sizeDelta = new Vector2(0f, h);
            if (amount <= 0f && target <= 0f) { instance = null; Destroy(gameObject); }
        }
    }
}
