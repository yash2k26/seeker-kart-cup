using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// A prop a kart smashes through (Beach Buggy's crates, pots and cones): it flies off the way the kart was going,
    /// tumbling, in a puff of dust, and is back in its place a few seconds later for the next lap. A trigger, not a
    /// body: the kart treats anything solid it touches as a wall, which would cancel its drift and its boost. The
    /// flight is animated by hand, so it never knocks a kart about either. Hitting one costs a little speed.
    public sealed class Smashable : MonoBehaviour
    {
        const float Flight = 1.1f, Shrink = 0.35f, Back = 8f;
        Vector3 home, homeScale, vel, spin;
        Quaternion homeRot;
        Renderer[] shown;
        float hitAt = -1f, downUntil = -1f;
        public static event System.Action<Smashable, Kart> Smashed;   // (the playtest takes frames on it)

        void Awake()
        {
            home = transform.position; homeRot = transform.rotation; homeScale = transform.localScale;
            shown = GetComponentsInChildren<Renderer>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (hitAt >= 0f || downUntil >= 0f || !other.IsKart()) return;
            var kart = other.transform.GetTopmostParentComponent<Kart>();
            if (kart == null || kart.rb == null) return;
            var v = kart.rb.linearVelocity;
            var away = transform.position - kart.transform.position; away.y = 0f;
            vel = v * 0.85f + away.normalized * 3f + Vector3.up * (4.5f + v.magnitude * 0.12f);
            spin = Random.onUnitSphere * Random.Range(360f, 720f);
            hitAt = Time.time;
            Smashed?.Invoke(this, kart);
            if (!kart.rb.isKinematic) kart.rb.linearVelocity = v * 0.96f;         // a bump, not a wall (another phone's kart is kinematic: leave it)
            var puff = Resources.Load<GameObject>("FX/SmashPuff");
            if (puff != null) Instantiate(puff, transform.position + Vector3.up * 0.4f, Quaternion.identity);
            var p = kart.GetComponent<KartProgress>();
            if (p != null && p.isPlayer) { Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Hit(); CameraFeel.Instance?.Kick(-kart.transform.forward, 0.25f); Achievements.Add(Achievements.Stat.Smashes); }
        }

        void Update()
        {
            if (hitAt >= 0f)
            {
                float t = Time.time - hitAt;
                vel += Physics.gravity * Time.deltaTime;
                transform.position += vel * Time.deltaTime;
                transform.Rotate(spin * Time.deltaTime, Space.World);
                if (t > Flight) transform.localScale = homeScale * Mathf.Max(0f, 1f - (t - Flight) / Shrink);
                if (t > Flight + Shrink) { hitAt = -1f; downUntil = Time.time + Back; Show(false); }
                return;
            }
            if (downUntil >= 0f && Time.time > downUntil)
            {
                downUntil = -1f;
                transform.SetPositionAndRotation(home, homeRot);
                transform.localScale = homeScale;
                Show(true);
            }
        }

        void Show(bool on) { foreach (var r in shown) if (r != null) r.enabled = on; }
    }
}
