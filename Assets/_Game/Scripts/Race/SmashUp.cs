using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Smash-Up (Beach Buggy's Stampede): alone on the circuit for a lap, and the stars are for what the player smashes on
    /// the way, the crates, pots and presents by the road and the animals crossing it. The goal is a share of what stands
    /// there, so it holds on every circuit without a number per map: two in five for three stars, one in four for two,
    /// one in ten for one. The crates wait just over the kerb on the inside of the corners and along the straights
    /// (TrackDressing.Smashables), so the lap that scores cuts every apex. The apex cones fly too but do not count: there
    /// are hundreds, a few metres apart, and the autopilot's ordinary line clipped 13 of them on Pine Ridge.
    public sealed class SmashUp : MonoBehaviour
    {
        public static int Score { get; private set; }
        public static int Total { get; private set; }
        public static SmashUp Instance { get; private set; }

        static readonly float[] Share = { 0f, 0.1f, 0.25f, 0.4f };   // the ordinary racing line clips about one in ten (3 of 26 on Pine Ridge)
        public static int Need(int stars) => Mathf.Max(stars, Mathf.CeilToInt(Total * Share[Mathf.Clamp(stars, 0, 3)]));
        public static int Stars => Total <= 0 ? 0 : Score >= Need(3) ? 3 : Score >= Need(2) ? 2 : Score >= Need(1) ? 1 : 0;
        /// The place the stars stand for, so the podium's gems and chest pay a Smash-Up the way they pay a race.
        public static int Place => Stars switch { 3 => 1, 2 => 2, 1 => 3, _ => 6 };

        void Awake()
        {
            Instance = this;
            Score = 0;
            int props = 0;
            foreach (var s in FindObjectsByType<Smashable>(FindObjectsSortMode.None)) if (Counts(s)) props++;
            Total = props + Critter.All.Count;
            Smashable.Smashed += OnSmashed; Critter.Hit += OnHit;
            Debug.Log($"[race] smash-up: {Total} to smash, {Need(3)} for three stars");
        }

        void OnDestroy()
        {
            Smashable.Smashed -= OnSmashed; Critter.Hit -= OnHit;
            if (Instance == this) Instance = null;
        }

        bool told;
        void Update()
        {
            var race = RaceManager.Instance;
            if (told || race == null || race.state != RaceManager.State.Finished) return;
            told = true;
            Debug.Log($"[race] smash-up over: {Score} of {Total}, {Stars} star(s)");
        }

        static bool Counts(Smashable s) => !s.name.Contains("cone");
        void OnSmashed(Smashable s, Kart kart) { if (Counts(s)) Count(kart); }
        void OnHit(Critter _, Kart kart) => Count(kart);

        void Count(Kart kart)
        {
            var race = RaceManager.Instance;
            if (race == null || race.state != RaceManager.State.Racing || race.Player == null || kart.gameObject != race.Player.gameObject) return;
            int before = Stars;
            Score++;
            if (Stars > before) RaceHud.Instance?.Callout(Stars == 1 ? "1 STAR" : $"{Stars} STARS", 1f);
        }
    }
}
