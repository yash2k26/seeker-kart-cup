using System.Collections.Generic;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;
using UnityEngine.Events;

namespace KartCup
{
    /// Owns the race: countdown, clock, running order, finish. Every field a networked race would need to
    /// mirror is set through SetState/raceTime, so the online layer can drive the same manager.
    public sealed class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }
        public enum State { Idle, Countdown, Racing, Finished }

        [Header("Rules")]
        public int totalLaps = 2;
        public float countdownSeconds = 3f;
        public Checkpoint[] checkpoints = System.Array.Empty<Checkpoint>();

        [Header("Events")]
        public UnityEvent<int> onCountdown = new();      // 3, 2, 1, then 0 = GO
        public UnityEvent onGo = new();
        public UnityEvent<KartProgress> onFinish = new();

        public State state { get; private set; } = State.Idle;
        public float raceTime { get; private set; }
        public float countdown { get; private set; }
        public KartProgress Player { get; private set; }
        public readonly List<KartProgress> racers = new();

        int lastTick = -1;
        double goAt = -1;                                // online: GO on the unscaled clock, shared by every phone
        readonly List<KartProgress> order = new();

        void Awake() { Instance = this; }

        void Start()
        {
            checkpoints = checkpoints.Where(c => c != null).OrderBy(c => c.index).ToArray();
            Rescan();
            // Online, NetRace holds the grid until the room's GO; solo, RaceSpawner starts it after the intro shot. Only a
            // scene with neither (an old test scene) starts straight away.
            if (!RoomClient.InRaceRoom && FindAnyObjectByType<RaceSpawner>() == null) BeginCountdown();
        }

        /// Call after spawning or despawning karts.
        public void Rescan()
        {
            racers.Clear();
            racers.AddRange(FindObjectsByType<KartProgress>(FindObjectsSortMode.None));
            Player = racers.FirstOrDefault(r => r.isPlayer);
            order.Clear(); order.AddRange(racers);
        }

        public void BeginCountdown()
        {
            state = State.Countdown;
            countdown = countdownSeconds;
            goAt = -1;
            lastTick = -1;
            foreach (var r in racers) r.GetComponent<Kart>().active = false;    // input is clamped to zero while frozen
        }

        /// Online: everyone waits on the grid until the room says when; nothing counts down on its own.
        public void Hold()
        {
            state = State.Idle;
            goAt = -1;
            foreach (var r in racers) r.GetComponent<Kart>().active = false;
        }

        /// Online: 3-2-1 toward a GO instant in Time.unscaledTimeAsDouble terms, the same server instant on every phone.
        public void BeginCountdownAt(double unscaledGo)
        {
            BeginCountdown();
            goAt = unscaledGo;
            countdown = (float)(goAt - Time.unscaledTimeAsDouble);
        }

        public void SetState(State next)                                        // the online race state object calls this
        {
            if (state == next) return;
            state = next;
            bool live = next == State.Racing;
            foreach (var r in racers) r.GetComponent<Kart>().active = live;
            if (live) { RaceSession.Instance?.BeginRace(); onGo.Invoke(); }
        }

        void Update()
        {
            switch (state)
            {
                case State.Countdown:
                    if (goAt > 0) countdown = (float)(goAt - Time.unscaledTimeAsDouble); else countdown -= Time.deltaTime;
                    int tick = Mathf.CeilToInt(Mathf.Max(0, countdown));
                    if (tick != lastTick)
                    {
                        lastTick = tick;
                        onCountdown.Invoke(tick);
                        AudioDirector.Instance?.Countdown(tick);
                    }
                    if (countdown <= 0) { raceTime = 0; SetState(State.Racing); }
                    break;

                case State.Racing:
                    // Online, the race runs on the room's clock (unscaled time since the shared GO), the same clock the
                    // room's referee times the drive by. Summing Time.deltaTime loses time on every frame hitch longer
                    // than maximumDeltaTime, and a finish that far off the room's clock is thrown out as a DNF.
                    raceTime = goAt > 0 ? (float)(Time.unscaledTimeAsDouble - goAt) : raceTime + Time.deltaTime;
                    order.Sort((a, b) => b.score.CompareTo(a.score));
                    break;
            }
        }

        public int PositionOf(KartProgress p) => order.IndexOf(p) + 1;

        /// The race clock now. Online that is this frame's room clock, not the value Update left last frame: a gate or
        /// the line is crossed in the physics steps that run before Update, and the finish-moment state the room's
        /// referee times the crossing by carries this frame's clock.
        public float Now => goAt > 0 && state != State.Idle && state != State.Countdown ? (float)(Time.unscaledTimeAsDouble - goAt) : raceTime;

        public void Finish(KartProgress p)
        {
            if (p.finished) return;
            p.finished = true;
            p.finishTime = Now;
            // Solo, a kart over the line keeps driving (the player's on autopilot, under a swinging camera); online every
            // kart stops, as before: what the other phones see of ours is the room's to show.
            if (RoomClient.InRaceRoom) p.GetComponent<Kart>().active = false;
            else if (p.isPlayer) FinishLap.Cruise(p);
            p.GetComponent<KartHooks>()?.OnFinish();                            // the driver's cheer, and the winner's bark
            onFinish.Invoke(p);
            if (p.isPlayer) state = State.Finished;
        }
    }
}
