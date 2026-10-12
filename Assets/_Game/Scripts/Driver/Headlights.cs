using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// A kart's lamps at night: two headlight lenses and two tail lights on the bodywork, and on the player's kart two
    /// real spot lights so the road ahead lights up. Rivals get the lenses only: a phone cannot afford twelve lights.
    public sealed class Headlights : MonoBehaviour
    {
        public void Init(Kart kart, bool player)
        {
            var night = NightScene.Instance;
            if (night == null || kart == null) return;
            var body = GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Body");
            if (body == null) return;

            // The bodywork's box in the kart's own frame (the mesh's axes are whatever the kit exported).
            var lb = body.localBounds;
            var toKart = kart.transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int k = 0; k < 8; k++)
            {
                var c = toKart.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? lb.min.x : lb.max.x, (k & 2) == 0 ? lb.min.y : lb.max.y, (k & 4) == 0 ? lb.min.z : lb.max.z));
                min = Vector3.Min(min, c); max = Vector3.Max(max, c);
            }
            float x = (max.x - min.x) * 0.33f, y = Mathf.Lerp(min.y, max.y, 0.6f);
            Dim(night.smoke);
            foreach (float side in new[] { -1f, 1f })
            {
                // a quad's face is its -Z, so the headlight turns round to shine forward
                Lens("Headlight", body.transform, kart.transform, night.lens, new Vector3(x * side, y, max.z + 0.03f), Quaternion.Euler(0, 180f, 0), new Vector2(0.18f, 0.12f));
                Lens("Taillight", body.transform, kart.transform, night.tail, new Vector3(x * side, y, min.z - 0.03f), Quaternion.identity, new Vector2(0.12f, 0.05f));
                if (!player) continue;
                var beam = new GameObject("HeadlightBeam");
                beam.transform.SetParent(body.transform, true);
                beam.transform.SetPositionAndRotation(kart.transform.TransformPoint(new Vector3(x * side, y + 0.05f, max.z)),
                                                      kart.transform.rotation * Quaternion.Euler(12f, side * 4f, 0));
                var l = beam.AddComponent<Light>();
                l.type = LightType.Spot; l.color = night.headlight; l.intensity = night.intensity; l.range = night.range;
                l.spotAngle = night.angle; l.innerSpotAngle = night.angle * 0.5f; l.shadows = LightShadows.None;
            }
            if (!player) return;
            // The film keeps every face lit: a soft cool fill over the seat, from the camera's side. Gentle and a metre
            // off: close in, it blew a bald or pale head out to a white ball (a black cap hid that in the editor).
            var seat = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat") ?? body.transform;
            var fill = new GameObject("DriverFill").AddComponent<Light>();
            fill.transform.SetParent(seat, false);
            fill.transform.position = seat.position + kart.transform.up * 1.8f - kart.transform.forward * 1.2f;
            fill.type = LightType.Point; fill.range = 4f; fill.intensity = 0.22f; fill.color = new Color(0.9f, 0.92f, 1f); fill.shadows = LightShadows.None;
        }

        /// Smoke, dust and sparks were authored for daylight as unlit white; at night they read as glowing marshmallows.
        void Dim(float k)
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!(ps.name.Contains("Exhaust") || ps.name.Contains("Collision"))) continue;   // drift sparks stay bright: they are the tier read
                var main = ps.main;
                var start = main.startColor;
                if (start.mode == ParticleSystemGradientMode.Color) { var c = start.color; main.startColor = new Color(c.r * k, c.g * k, c.b * k, c.a); }
                else if (start.mode == ParticleSystemGradientMode.TwoColors) { var a = start.colorMin; var b = start.colorMax; main.startColor = new ParticleSystem.MinMaxGradient(new Color(a.r * k, a.g * k, a.b * k, a.a), new Color(b.r * k, b.g * k, b.b * k, b.a)); }
            }
        }

        /// A lit quad on the bodywork, placed in the kart's frame but parented to the body so it rides its bounce.
        static void Lens(string name, Transform body, Transform kart, Material mat, Vector3 kartLocal, Quaternion kartRot, Vector2 size)
        {
            if (mat == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(go.GetComponent<Collider>());   // now, not at the frame's end: parented under the kart's moving body first,
                                                             // its concave mesh collider logged a physics error per lamp per kart (MenuMonkey, 12 Oct 2026)
            go.name = name;
            go.transform.SetParent(body, true);
            go.transform.SetPositionAndRotation(kart.TransformPoint(kartLocal), kart.rotation * kartRot);
            var s = body.lossyScale;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, s.x), size.y / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
