using System.Collections.Generic;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// What the rear wheels throw up. Every Beach Buggy frame has dust behind the wheels; ours left only tyre marks.
    /// Off the road (sand, snow, earth, the map's own) a thick plume that grows with speed; on the tarmac, smoke on every
    /// turn (10 Oct 2026, the user: "if I tap right, smoke comes off the tyres and goes right"), more in a drift, blown
    /// the way the kart is turning. After dark the colours are dimmed a little: the puffs are unlit and would glow.
    public sealed class WheelDust : MonoBehaviour
    {
        Kart kart;
        readonly List<(KartWheel wheel, ParticleSystem ps)> rear = new();
        Vector3 lastFwd; float yawRate, nextRumble; bool player, playerKnown;
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
                "volcano" => new Color(0.36f, 0.33f, 0.32f),        // ash
                _ => new Color(0.88f, 0.76f, 0.56f),            // Palm Bay's sand
            };
            smoke = new Color(0.86f, 0.86f, 0.9f);
            spray = new Color(0.85f, 0.94f, 1f);
            if (NightScene.Instance != null) { ground *= 0.5f; smoke *= 0.8f; spray *= 0.6f; ground.a = smoke.a = spray.a = 1f; }   // the smoke was 0.45: invisible on the night road
        }

        void LateUpdate()
        {
            // the grass under the wheels, felt: a light tick five times a second while the player is off the road at speed
            if (player && Time.time >= nextRumble && kart != null && kart.velMag > 8f && rumbling) { nextRumble = Time.time + 0.2f; Haptics.Play(Haptics.Kind.Tick); }
        }
        bool rumbling;

        void Update()
        {
            if (kart == null) return;
            float speed = kart.velMag;
            // how hard the kart is turning, from its heading (the rigidbody does not turn: Powerslide yaws the rotator)
            var heading = kart.rotator != null ? kart.rotator : kart.transform;
            var fwd = Vector3.ProjectOnPlane(heading.forward, Vector3.up);
            if (lastFwd != Vector3.zero && Time.deltaTime > 0f) yawRate = Mathf.Lerp(yawRate, Vector3.SignedAngle(lastFwd, fwd, Vector3.up) / Time.deltaTime, 1f - Mathf.Exp(-12f * Time.deltaTime));
            lastFwd = fwd;
            float turn = Mathf.InverseLerp(8f, 55f, Mathf.Abs(yawRate)) * Mathf.InverseLerp(5f, 16f, speed);   // any real steer, not only a hard corner
            if (!playerKnown) { var prog = GetComponent<KartProgress>(); player = prog != null && prog.isPlayer; playerKnown = prog != null; }
            bool anyOffroad = false;
            var cam = Camera.main;
            bool far = cam != null && (cam.transform.position - transform.position).sqrMagnitude > 45f * 45f;
            foreach (var (wheel, ps) in rear)
            {
                if (wheel == null || ps == null) continue;
                bool offroad = wheel.surfaceProps != null && wheel.surfaceProps.speed < 0.95f;
                if (offroad && wheel.grounded) anyOffroad = true;
                // in the shallows: white spray, bigger and faster (Beach Buggy's curtains of water)
                var sheet = WaterBody.At(wheel.contactPoint);
                bool wet = sheet != null && !sheet.lava && wheel.contactPoint.y < sheet.transform.position.y + 0.1f;
                float rate = 0f;
                if ((wheel.grounded || wet) && speed > 3f && !far)
                    rate = wet ? Mathf.Lerp(30f, 70f, Mathf.InverseLerp(3f, 25f, speed))
                         : offroad ? Mathf.Lerp(8f, 46f, Mathf.InverseLerp(3f, 28f, speed))
                         : kart.drifting ? 50f + 20f * turn : turn > 0.05f ? 18f + 32f * turn : 0f;
                var e = ps.emission; e.rateOverTime = rate;
                if (rate > 0f)
                {
                    ps.transform.position = wet ? new Vector3(wheel.contactPoint.x, sheet.transform.position.y + 0.1f, wheel.contactPoint.z) : wheel.contactPoint + Vector3.up * 0.15f;
                    var m = ps.main; m.startColor = wet ? spray : offroad ? ground : smoke;
                    bool tarmacSmoke = !wet && !offroad;
                    m.startSpeedMultiplier = wet ? 3.2f : 1f; m.startSizeMultiplier = wet ? 1.5f : tarmacSmoke ? 2.4f : 1f;   // toy-scale wheels: a 1 m puff vanished behind them
                    m.startLifetimeMultiplier = tarmacSmoke ? 1.5f : 1f;
                    // on the tarmac the smoke is blown the way the kart is turning (and lifts): tap right, it goes right
                    var v = ps.velocityOverLifetime;
                    v.enabled = !wet && !offroad && (kart.drifting || turn > 0.05f);
                    if (v.enabled)
                    {
                        var side = heading.right * Mathf.Sign(yawRate) * (1.4f + 2.2f * turn);
                        v.space = ParticleSystemSimulationSpace.World;
                        v.x = new ParticleSystem.MinMaxCurve(side.x); v.y = new ParticleSystem.MinMaxCurve(0.7f); v.z = new ParticleSystem.MinMaxCurve(side.z);
                    }
                }
            }
            rumbling = anyOffroad;
        }
    }
}
