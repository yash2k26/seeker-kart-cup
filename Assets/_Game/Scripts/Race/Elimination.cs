using System.Linq;
using PowerslideKartPhysics;
using PrimeTween;
using UnityEngine;

namespace KartCup
{
    /// Elimination (Beach Buggy's): ten seconds after GO, and every five after that, whoever is last is knocked out of
    /// the race. Out early, you place behind everyone still driving; the last kart running wins. Finishing the laps first
    /// ends it the ordinary way. (At 20 s and every 15, a one-lap race of ~33 s lost one kart before the line and played
    /// as an ordinary race, 11 Oct 2026; now the fifth knockout falls at 30 s.)
    public sealed class Elimination : MonoBehaviour
    {
        public static Elimination Instance { get; private set; }
        public const float First = 10f, Every = 5f;

        RaceManager race;
        float next = First;
        int knocked;

        void Awake() { Instance = this; race = RaceManager.Instance; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// Seconds to the next knockout (the HUD's lap line counts it down).
        public float SecondsToNext => race == null ? 0f : Mathf.Max(0f, next - race.raceTime);

        void Update()
        {
            if (race == null || race.state != RaceManager.State.Racing || race.raceTime < next) return;
            next += Every;
            var alive = race.racers.Where(r => !r.finished).ToList();
            if (alive.Count <= 1) return;
            Knock(alive.OrderBy(r => r.score).First(), alive);
        }

        void Knock(KartProgress out_, System.Collections.Generic.List<KartProgress> alive)
        {
            knocked++;
            Debug.Log($"[race] {out_.racerName} knocked out at {race.raceTime:0.0} s ({alive.Count - 1} left)");
            var kart = out_.GetComponent<Kart>();
            if (kart != null) kart.active = false;
            RaceHud.Instance?.Callout(out_.isPlayer ? "KNOCKED OUT" : $"{out_.racerName.ToUpperInvariant()} OUT!", 1.4f);
            Haptics.Play(out_.isPlayer ? Haptics.Kind.Hit : Haptics.Kind.Tick);
            AudioDirector.Instance?.Warn();

            // ranked by when they went: the first out is last (a time no finisher can have)
            out_.finished = true; out_.finishTime = 1e6f - knocked;
            alive.Remove(out_);
            if (out_.isPlayer)
            {
                // the race is over for us: everyone still driving places ahead, in the order they are running
                int i = 0;
                foreach (var r in alive.OrderByDescending(r => r.score)) { r.finished = true; r.finishTime = 9e5f + i++; }
                race.onFinish.Invoke(out_);
                race.SetState(RaceManager.State.Finished);
            }
            else
            {
                Vanish(out_.transform);
                if (alive.Count == 1) race.Finish(alive[0]);                  // the last kart running wins
            }
        }

        /// A knocked-out kart shrinks away and is gone.
        static void Vanish(Transform t)
        {
            Tween.Scale(t, Vector3.zero, 0.35f, Ease.InBack).OnComplete(t, x => x.gameObject.SetActive(false));
        }
    }
}
