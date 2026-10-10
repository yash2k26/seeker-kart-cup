using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Where the right thumb's buttons sit. With AUTO GAS on (the default, Beach Buggy's way) the kart drives itself
    /// forward and there is no GAS button: DRIFT takes its corner, the one a thumb rests on, and BRAKE moves into
    /// DRIFT's old place. With it off, the layout the scene was built with comes back and GAS must be held. The
    /// STEERING setting decides whether the left thumb's arrows are buttons or only lights (SLIDE and TILT).
    public sealed class ControlLayout : MonoBehaviour
    {
        [SerializeField] InputManager input;
        [SerializeField] RectTransform gas, drift, brake, item, meter, itemLabel;
        [SerializeField] CanvasGroup steerLeft, steerRight;   // buttons only under ARROWS; SteerButtons lights them otherwise
        [SerializeField] GameObject slideArea;                 // the left half, live only under SLIDE

        static readonly List<ControlLayout> live = new();
        struct Spot { public Vector2 pos, size; }
        readonly Dictionary<RectTransform, Spot> built = new();

        void Awake()
        {
            if (input == null) input = FindFirstObjectByType<InputManager>();
            foreach (var rt in new[] { gas, drift, brake, item, meter, itemLabel })
                if (rt != null) built[rt] = new Spot { pos = rt.anchoredPosition, size = rt.sizeDelta };
        }

        void OnEnable() { live.Add(this); Apply(); }
        void OnDisable() => live.Remove(this);

        /// The setting changed (settings screen or pause menu): every HUD in the scene follows it at once.
        public static void ApplyAll() { foreach (var l in live) l.Apply(); }

        void Apply()
        {
            bool arrows = Settings.Steering == Settings.Steer.Arrows;
            foreach (var a in new[] { steerLeft, steerRight })
                if (a != null) { a.blocksRaycasts = arrows; a.alpha = arrows ? 1f : 0.3f; }
            if (slideArea != null) slideArea.SetActive(Settings.Steering == Settings.Steer.Slide);

            bool auto = Settings.AutoGas;
            if (input != null) input.mobileAutoAccel = auto ? 1f : 0f;   // brake still overrides it (accel = auto - brake)
            foreach (var kv in built) { kv.Key.anchoredPosition = kv.Value.pos; kv.Key.sizeDelta = kv.Value.size; }
            if (gas != null) gas.gameObject.SetActive(!auto);
            if (!auto || gas == null) return;
            var g = built[gas];
            if (drift != null) { var d = built[drift]; drift.anchoredPosition = g.pos; drift.sizeDelta = g.size;
                                 if (brake != null) { brake.anchoredPosition = new Vector2(d.pos.x, d.pos.y); brake.sizeDelta = new Vector2(d.size.x, built[brake].size.y); } }
            // the drift meter rides on top of DRIFT, between it and the item slot above
            if (meter != null) { meter.anchoredPosition = new Vector2(g.pos.x, g.pos.y + g.size.y + 6f); meter.sizeDelta = new Vector2(g.size.x, 18f); }
            if (item != null) item.anchoredPosition = built[item].pos + new Vector2(0f, 18f);
            if (itemLabel != null) itemLabel.anchoredPosition = built[itemLabel].pos + new Vector2(0f, 18f);
        }
    }
}
