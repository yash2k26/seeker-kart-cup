using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// What the rear wheels throw up. Every Beach Buggy frame has dust behind the wheels; ours left only tyre marks.
    /// Off the road (sand, snow, earth, the map's own) a thick plume that grows with speed; on the tarmac only while
    /// drifting, a pale smoke. After dark the colours are dimmed: the puffs are unlit and would glow otherwise.
    public sealed class WheelDust : MonoBehaviour
    {
        Kart kart;
        readonly List<(KartWheel wheel, ParticleSystem ps)> rear = new();
        Color ground, smoke, spray;

        public static void On(Kart kart)
        {
            var prefab = Resources.Load<GameObject>("FX/WheelDust");
            if (kart == null || prefab == null || kart.GetComponent<WheelDust>() != null) return;
            var d = kart.gameObject.AddComponent<WheelDust>();
            d.kart = kart;
            foreach (var w in kart.GetComponentsInChildren<KartWheel>())
            {
                if (kart.transform.InverseTransformPoint(w.transform.position).z > 0f) continue;   // the rear pair
                var ps = Instantiate(prefab, kart.transform).GetComponent<ParticleSystem>();
                d.rear.Add((w, ps));
            }
        }

        void Start()
        {
            ground = Cup.TrackId switch
            {
                "alpine" => new Color(0.93f, 0.95f, 1f),
                "forest" => new Color(0.52f, 0.42f, 0.31f),
                "speedway" => new Color(0.62f, 0.56f, 0.45f),
                _ => new Color(0.88f, 0.76f, 0.56f),            // Palm Bay's sand
            };
            smoke = new Color(0.86f, 0.86f, 0.9f);
            spray = new Color(0.85f, 0.94f, 1f);
            if (NightScene.Instance != null) { ground *= 0.5f; smoke *= 0.45f; spray *= 0.6f; ground.a = smoke.a = spray.a = 1f; }
        }

        void Update()
        {
            if (kart == null) return;
            float speed = kart.velMag;
            var cam = Camera.main;
            bool far = cam != null && (cam.transform.position - transform.position).sqrMagnitude > 45f * 45f;
            foreach (var (wheel, ps) in rear)
            {
                if (wheel == null || ps == null) continue;
                bool offroad = wheel.surfaceProps != null && wheel.surfaceProps.speed < 0.95f;
                // in the shallows: white spray, bigger and faster (Beach Buggy's curtains of water)
                bool wet = WaterBody.SurfaceAt(wheel.contactPoint, out float surface) && wheel.contactPoint.y < surface + 0.1f;
                float rate = 0f;
                if ((wheel.grounded || wet) && speed > 3f && !far)
                    rate = wet ? Mathf.Lerp(30f, 70f, Mathf.InverseLerp(3f, 25f, speed))
                         : offroad ? Mathf.Lerp(8f, 46f, Mathf.InverseLerp(3f, 28f, speed))
                         : kart.drifting ? 22f : 0f;
                var e = ps.emission; e.rateOverTime = rate;
                if (rate > 0f)
                {
                    ps.transform.position = wet ? new Vector3(wheel.contactPoint.x, surface + 0.1f, wheel.contactPoint.z) : wheel.contactPoint + Vector3.up * 0.15f;
                    var m = ps.main; m.startColor = wet ? spray : offroad ? ground : smoke;
                    m.startSpeedMultiplier = wet ? 3.2f : 1f; m.startSizeMultiplier = wet ? 1.5f : 1f;
                }
            }
        }
    }
}
