using System.Collections;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Each driver's special ability (Beach Buggy's: one per driver, on its own button, charged by racing). It charges
    /// over Charge seconds of racing and fires once; then it charges again. Solo races only (RaceSpawner): an online
    /// room's other karts are replays and could not answer it, and the Weekly is a lap against the clock. Built from
    /// what the game already has: boosts, the items' own projectiles, the oil, the spin-out, the respawn's shield.
    public sealed class Ability : MonoBehaviour
    {
        public enum Kind { Burst, Missiles, LongBoost, Rockets, ChainBoost, Bulldozer, SunBomb, Shield, Oil, Review }
        public sealed class Def { public Kind kind; public string name, line; }

        /// By driver (CharacterDef.displayName).
        public static Def For(string driver) => driver switch
        {
            "Toly" => new() { kind = Kind.Burst, name = "SHIP IT", line = "A huge burst of speed" },
            "Mert" => new() { kind = Kind.Missiles, name = "INDEXED", line = "Three homing missiles" },
            "Akshay" => new() { kind = Kind.LongBoost, name = "TOP SPEED", line = "A long, steady boost" },
            "Emmett" => new() { kind = Kind.Rockets, name = "BLINDSIDE", line = "Two rockets dead ahead" },
            "Bitsy" => new() { kind = Kind.ChainBoost, name = "ALL GAS", line = "Three boosts in a row" },
            "Pip" => new() { kind = Kind.Bulldozer, name = "BULLDOZER", line = "Unstoppable: whoever she touches spins" },
            "Chase" => new() { kind = Kind.SunBomb, name = "NETWORK EFFECT", line = "A sun bomb thrown ahead" },
            "Beeman" => new() { kind = Kind.Shield, name = "HOTFIX", line = "A shield nothing gets through" },
            "Maroo" => new() { kind = Kind.Oil, name = "BIG CURLS", line = "Three oil slicks behind" },
            "Andrew" => new() { kind = Kind.Review, name = "CODE REVIEW", line = "Everyone close ahead spins out" },
            _ => new() { kind = Kind.Burst, name = "BURST", line = "A burst of speed" },
        };

        public const float Charge = 18f;
        public Def def;
        public float charge01;
        public bool Ready => charge01 >= 1f;
        public bool isPlayer;
        /// How fast it charges against the usual: a Tour boss's 1.5 (RaceMode.BossCharge). A faster charge, never a free one.
        public float rate = 1f;
        public static event System.Action<Ability> Used;    // the HUD's callout, the playtest's frames

        Kart kart;
        float shieldUntil, ramUntil;
        GameObject shield;

        public static Ability On(Kart k, string driver, bool player)
        {
            var a = k.gameObject.AddComponent<Ability>();
            a.kart = k; a.def = For(driver); a.isPlayer = player;
            return a;
        }

        void Update()
        {
            var race = RaceManager.Instance;
            if (race == null || kart == null) return;
            if (race.state == RaceManager.State.Racing && !Ready) charge01 = Mathf.Min(1f, charge01 + Time.deltaTime * rate / Charge);
            if (shield != null && Time.time > Mathf.Max(shieldUntil, ramUntil)) { Destroy(shield); shield = null; }
        }

        /// The button, or the AI.
        public bool Fire()
        {
            var race = RaceManager.Instance;
            if (!Ready || kart == null || !kart.active || kart.spinningOut || race == null || race.state != RaceManager.State.Racing) return false;
            charge01 = 0f;
            switch (def.kind)
            {
                case Kind.Burst: kart.AddBoost(3.2f, 12f); break;
                case Kind.LongBoost: kart.AddBoost(6.5f, 4f); break;
                case Kind.ChainBoost: StartCoroutine(Chain()); break;
                case Kind.Missiles: StartCoroutine(Volley("Homing Item", 3, 0.28f)); break;
                case Kind.Rockets: StartCoroutine(Volley("Rocket", 2, 0.22f)); break;
                case Kind.SunBomb: Cast("Sun Bomb"); break;
                case Kind.Shield: Shield(5f); break;
                case Kind.Bulldozer: ramUntil = Time.time + 5f; Shield(5f); kart.AddBoost(2f, 4f); break;
                case Kind.Oil: StartCoroutine(Slicks()); break;
                case Kind.Review: Review(race); break;
            }
            Used?.Invoke(this);
            if (isPlayer) { Haptics.Play(Haptics.Kind.Boost); CameraFeel.Instance?.Kick(Vector3.up, 0.3f); }
            Debug.Log($"[race] {name} used {def.name}");
            return true;
        }

        IEnumerator Chain() { for (int i = 0; i < 3; i++) { kart.AddBoost(2f, 7f); yield return new WaitForSeconds(0.9f); } }

        IEnumerator Volley(string item, int count, float gap)
        {
            for (int i = 0; i < count; i++) { Cast(item); yield return new WaitForSeconds(gap); }
        }

        /// An item fired without touching the one the kart is holding; KartHooks spares the kart from its own shot.
        void Cast(string itemName)
        {
            var items = FindAnyObjectByType<ItemManager>();
            var item = items != null ? items.GetItem(itemName) : null;
            if (item == null || kart == null) return;
            var props = new ItemCastProperties
            {
                castKart = kart, castKartVelocity = kart.rb.linearVelocity, castGravity = kart.currentGravityDir,
                castPoint = kart.transform.position, castRotation = kart.rotator != null ? kart.rotator.rotation : kart.transform.rotation,
                castCollider = kart.rotator != null ? kart.rotator.GetComponent<Collider>() : null, castDirection = kart.forwardDir,
            };
            item.Activate(props);
            GetComponent<KartHooks>()?.AfterCast();
        }

        void Shield(float seconds)
        {
            shieldUntil = Time.time + seconds;
            kart.invulnerableUntil = Mathf.Max(kart.invulnerableUntil, shieldUntil);
            if (shield != null) return;
            var mat = Resources.Load<Material>("FX/Shield");
            shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(shield.GetComponent<Collider>());
            shield.name = "Shield";
            shield.transform.SetParent(kart.transform, false);
            shield.transform.localPosition = Vector3.up * 1.1f;
            shield.transform.localScale = new Vector3(3.6f, 2.8f, 4.6f);
            var r = shield.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        IEnumerator Slicks()
        {
            var prefab = Resources.Load<GameObject>("OilSlick");
            if (prefab == null) yield break;
            for (int i = 0; i < 3; i++)
            {
                var back = kart.transform.position - kart.transform.forward * 3.5f + Vector3.up * 3f;
                if (Physics.Raycast(back, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.IsKart())
                {
                    var fwd = Vector3.ProjectOnPlane(kart.transform.forward, hit.normal).normalized;
                    var slick = Instantiate(prefab, hit.point + hit.normal * 0.03f, Quaternion.LookRotation(fwd, hit.normal));
                    slick.GetComponent<OilSlick>()?.Spare(kart, 2.5f);
                    Destroy(slick, 25f);
                }
                yield return new WaitForSeconds(0.45f);
            }
        }

        void Review(RaceManager race)
        {
            var fwd = kart.transform.forward;
            foreach (var r in race.racers)
            {
                if (r == null || r.gameObject == gameObject) continue;
                var d = r.transform.position - transform.position;
                if (d.magnitude > 60f || Vector3.Dot(d.normalized, fwd) < 0.3f) continue;
                r.GetComponent<Kart>()?.SpinOut(Kart.SpinAxis.Yaw, 1);
            }
        }

        // Pip's bulldozer: any kart she touches while it lasts spins out
        void OnCollisionEnter(Collision c)
        {
            if (Time.time > ramUntil || !c.collider.IsKart()) return;
            var other = c.collider.transform.GetTopmostParentComponent<Kart>();
            if (other != null && other != kart) other.SpinOut(Kart.SpinAxis.Roll, 1);
        }

        /// The AI's judgement: speed on a straight, missiles at someone ahead, oil at someone behind, a shield at once.
        public void ThinkFor(KartAi ai, float straightness)
        {
            if (!Ready) return;
            var race = RaceManager.Instance; if (race == null) return;
            bool ahead = false, behind = false;
            foreach (var r in race.racers)
            {
                if (r == null || r.gameObject == gameObject) continue;
                var d = r.transform.position - transform.position; float m = d.magnitude, dot = Vector3.Dot(d.normalized, transform.forward);
                if (m < 55f && dot > 0.5f) ahead = true;
                if (m < 25f && dot < -0.4f) behind = true;
            }
            bool go = def.kind switch
            {
                Kind.Burst or Kind.LongBoost or Kind.ChainBoost => straightness > 0.85f,
                Kind.Missiles or Kind.Rockets or Kind.SunBomb or Kind.Review => ahead,
                Kind.Oil => behind,
                _ => true,
            };
            if (go) Fire();
        }
    }
}
