using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Puts the grid together at runtime: solo, the character you picked plus the rest of the roster as AI; online,
    /// the room's roster with your kart driven by touch and everyone else's replayed from the network. Keeping this
    /// at runtime (not baked into the scene) is what lets the select screen and the lobby decide who is on the grid.
    public sealed class RaceSpawner : MonoBehaviour
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] KartDatabase garage;
        [SerializeField] GameObject[] kartPrefabs;       // Kart_oobi … Kart_race-future
        [SerializeField] GameObject[] driverPrefabs;     // Driver_Toly …
        [SerializeField] Transform[] gridSlots;
        [SerializeField] BasicWaypoint firstWaypoint;
        [SerializeField] KartCamera raceCamera;
        [SerializeField] int racers = 6;
        BasicWaypoint first;                             // where the AI sets off: the start line, whichever way round

        void Start()
        {
            if (roster == null || roster.characters.Length == 0) { Debug.LogError("[race] no roster"); return; }
            Chests.ClearLast(); Tour.ClearLast(); Daily.LastPaid = false;
            gameObject.AddComponent<PerfLog>();   // frame times to the log, every race
            RaceCamera.Apply(raceCamera != null ? raceCamera.GetComponent<Camera>() : Camera.main);   // trackside clutter not drawn far off
            if (RoomClient.InRaceRoom) { RaceMode.Clear(); SpawnOnline(); return; }   // a room is always the ordinary race

            // A race entered outside a cup (editor playtest, a direct scene load) still needs a grid.
            if (!Cup.Active)
            {
                // The scene's own track, so a directly loaded Race3 records its best as forest and RACE AGAIN reloads Race3.
                int.TryParse(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Replace("Race", ""), out int n);
                Cup.Begin(CharacterDatabase.SelectedIndex, roster.characters.Length, new[] { Mathf.Clamp(n - 1, 0, Tracks.Count - 1) });
                if (RaceMode.InEvent) Cup.StartAtBack();   // (the playtest's -event)
            }
            if (Weekly.Racing) RaceMode.Clear();   // the time trial is the ordinary lap, whatever ran before it
            // The event's levers (RaceMode): its laps, the circuit turned round, the mist. Turned before the grid is filled,
            // so the karts take their places on the reversed grid.
            var first = firstWaypoint;
            var race = RaceManager.Instance;
            if (race != null) race.totalLaps = Mathf.Max(1, RaceMode.Laps);
            if (RaceMode.Reverse) first = TrackDirection.Reverse(gridSlots, race) ?? first;
            if (RaceMode.Mist) TrackDirection.Mist();
            this.first = first;
            // Kartel Weekly's time trial and a Smash-Up: the player alone on the grid; a duel: the rival on pole, you beside
            bool duel = RaceMode.Current == RaceMode.Kind.Duel && RaceMode.Rival >= 0;
            int count = Weekly.Racing || RaceMode.Alone ? 1 : duel ? 2 : Mathf.Min(racers, gridSlots.Length);

            for (int slot = 0; slot < count && slot < Cup.Grid.Count; slot++)
            {
                // The grid comes from the cup: pole on race one, then the standings reversed, so leading costs you
                // track position next time out.
                int character = Weekly.Racing || RaceMode.Alone ? Cup.PlayerCharacter : duel ? (slot == 0 ? RaceMode.Rival : Cup.PlayerCharacter) : Cup.Grid[slot];
                var def = roster.At(character);
                bool isPlayer = duel ? slot == 1 : character == Cup.PlayerCharacter;   // (a mirror duel has the same driver twice)
                var kartId = isPlayer && !Weekly.Racing ? PlayerKart(def, allowTrial: true) : def.kartId;
                var kart = Spawn(def, gridSlots[slot], isPlayer, character, kartId, online: false);
                if (isPlayer && raceCamera != null) raceCamera.Initialize(kart);
            }
            RaceManager.Instance?.Rescan();
            StartCoroutine(Intro());
        }

        /// Before the flag: this race's turbo pads are laid, then a drone's lap of the circuit under its name (PodiumShow),
        /// then the grid and the 3-2-1. A tap skips the lap.
        IEnumerator Intro()
        {
            var race = RaceManager.Instance;
            if (race == null) yield break;
            var show = PodiumShow.Ensure();
            if (RaceMode.Current == RaceMode.Kind.Elimination && !Weekly.Racing) gameObject.AddComponent<Elimination>();
            // the time trial, a Smash-Up and a Clean Race: no items
            bool noItems = Weekly.Racing || RaceMode.Alone || RaceMode.Rule == RaceMode.Items.Clean;
            if (noItems) { var boxes = FindAnyObjectByType<ItemBag>(); if (boxes != null) boxes.gameObject.SetActive(false); }
            else
            {
                // bought items (Carry): more than one held, the player picks what to take while the grid waits; one, it goes in
                if (!Carry.SkipPicker && Carry.NeedsPicker) { race.Hold(); yield return CarryPicker.Ask(); }
                else if (!Carry.SkipPicker) Carry.Chosen = Carry.Auto();
                CarriedItems.Begin(race.Player != null ? race.Player.GetComponent<Kart>() : null, Carry.Clamp(Carry.Chosen));
            }
            if (show == null) { race.BeginCountdown(); yield break; }
            race.Hold();
            if (Weekly.Racing) show.Prizes("KARTEL WEEKLY · TIME TRIAL", Tracks.At(Cup.TrackIndex).name.ToUpperInvariant(), System.Array.Empty<string>());
            else
            {
                TurboPad.Spawn(System.Environment.TickCount);
                OilSlick.Spawn(System.Environment.TickCount + 1, Tracks.At(Cup.TrackIndex).difficulty + RaceMode.Hazard);   // solo only: rooms never get them
                Ramp.Spawn(System.Environment.TickCount + 3);                                             // Beach Buggy's jumps, on the long straights
                Critter.Spawn(System.Environment.TickCount + 2, Cup.TrackId);                             // Beach Buggy's critters, crossing the straights
                Roadworks.Spawn(System.Environment.TickCount + 4, RaceMode.Works);                        // the Tour's later series: slaloms of crates
                if (RaceMode.Alone) gameObject.AddComponent<SmashUp>();                                   // after the critters: they count
                show.Prizes(Tracks.At(Cup.TrackIndex).kicker, Tracks.At(Cup.TrackIndex).name.ToUpperInvariant(), new[] { "1ST", "2ND", "3RD" });
            }
            float until = Time.time + show.IntroSeconds, skippable = Time.time + 0.6f;
            while (Time.time < until && !(Time.time > skippable && PodiumShow.Tapped())) yield return null;
            show.End();
            race.BeginCountdown();
        }

        /// The driver prefab a character races as (the podium stands them up).
        public static GameObject DriverPrefab(string driverId)
        {
            var spawner = FindAnyObjectByType<RaceSpawner>();
            if (spawner == null) return null;
            foreach (var p in spawner.driverPrefabs) if (p != null && p.name == "Driver_" + driverId) return p;
            return null;
        }

        public static CharacterDatabase Roster => FindAnyObjectByType<RaceSpawner>()?.roster;

        static string AutopilotFile => System.IO.Path.Combine(Application.persistentDataPath, "autopilot");
        static bool DeviceAutopilot => !Application.isEditor && !RoomClient.InRaceRoom && System.IO.File.Exists(AutopilotFile);

        /// The autopilot's driver, from the file adb puts there: empty for the character's own AI; "skill=1.1" and
        /// "steady" (no unforced mistakes, takes the shortcuts) stand in for a stronger player, to play the Tour through.
        static void Tune(KartAi ai)
        {
            string cfg = "";
            try { cfg = System.IO.File.ReadAllText(AutopilotFile); } catch { }
            foreach (var word in cfg.Split(new[] { ' ', '\n', '\r', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.StartsWith("skill=") && float.TryParse(word.Substring(6), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) ai.skill = s;
                if (word == "steady") ai.steady = true;
            }
            Debug.Log($"[race] autopilot drives the player at skill {ai.skill:0.000}{(ai.steady ? ", steady" : "")}");
        }

        /// The room's roster in join order. Our own kart is the real thing; everyone else is a RemoteKart proxy.
        void SpawnOnline()
        {
            var client = RoomClient.Instance;
            var players = client.Room.players.Where(p => p.connected || p.id == client.MyId).ToList();
            int count = Mathf.Min(players.Count, gridSlots.Length);
            Kart mine = null; KartProgress me = null;
            var remotes = new List<RemoteKart>();
            for (int slot = 0; slot < count; slot++)
            {
                var p = players[slot];
                var def = roster.At(p.character);
                bool isMe = p.id == client.MyId;
                // Never a kart the player has not paid for, on any phone: the sender's claim is checked against ownership
                // only on the sender's own phone, so a remote kart is simply drawn as whatever prefab it names.
                var kartId = isMe ? PlayerKart(def, allowTrial: false) : (HasKart(p.kart) ? p.kart : def.kartId);
                var kart = Spawn(def, gridSlots[slot], isMe, p.character, kartId, online: true);
                var progress = kart.GetComponent<KartProgress>();
                progress.racerName = p.name;
                if (isMe) { mine = kart; me = progress; if (raceCamera != null) raceCamera.Initialize(kart); }
                else
                {
                    var remote = kart.gameObject.AddComponent<RemoteKart>();
                    remote.Bind(kart, kart.GetComponentInChildren<BlockyDriverPose>(), p.id, p.name);
                    remotes.Add(remote);
                }
            }
            RaceManager.Instance?.Rescan();
            gameObject.AddComponent<NetRace>().Bind(client, RaceManager.Instance, mine, me, remotes);
        }

        bool HasKart(string id) => !string.IsNullOrEmpty(id) && kartPrefabs.Any(k => k != null && k.name == "Kart_" + id);

        /// The kart the player actually drives: their garage pick if owned (or a trial they armed, solo only), else the
        /// character's own. Spending the trial here means one real race, not one look at the grid.
        string PlayerKart(CharacterDef def, bool allowTrial)
        {
            if (garage == null) return def.kartId;
            var id = garage.EffectiveKartFor(def, allowTrial);
            if (allowTrial && id == KartDatabase.ArmedTrial && !KartDatabase.Owned(garage.Find(id))) KartDatabase.SpendTrial(id);
            return HasKart(id) ? id : def.kartId;
        }

        Kart Spawn(CharacterDef def, Transform slot, bool isPlayer, int character, string kartId, bool online)
        {
            var prefab = kartPrefabs.FirstOrDefault(k => k.name == "Kart_" + kartId) ?? kartPrefabs.FirstOrDefault(k => k.name.EndsWith(def.kartId)) ?? kartPrefabs[0];
            var go = Instantiate(prefab, slot.position, slot.rotation);
            go.name = (isPlayer ? "Player_" : online ? "Remote_" : "AI_") + def.displayName;
            var kart = go.GetComponent<Kart>();
            def.ApplyTo(kart);
            // A room with money on it races equal karts: the garage pick is paint there. Its powers count in solo
            // races and free rooms, where nobody bought a place on the podium.
            // The Weekly's time trial is the same: a prize is never bought.
            bool paidRoom = online && RoomClient.Instance != null && RoomClient.Instance.Room.IsPaid;
            if (!paidRoom && !Weekly.Racing) garage?.Find(kartId)?.ApplyTo(kart);
            if (isPlayer) { CharacterDef.Paint(go, Paints.ColorFor(def, kartId), Paints.GoldOn(kartId)); Paints.Driving = kartId; } else def.ApplyLivery(go);
            if (NightScene.Instance != null) go.AddComponent<Headlights>().Init(kart, isPlayer);   // lamps lit after dark

            var progress = go.AddComponent<KartProgress>();
            progress.racerName = def.displayName;
            progress.isPlayer = isPlayer;
            progress.characterIndex = character;

            // driver in the seat
            BlockyDriverPose pose = null;
            var seat = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
            var driverPrefab = driverPrefabs.FirstOrDefault(d => d.name.EndsWith(def.driverId)) ?? driverPrefabs.FirstOrDefault();
            if (seat != null && driverPrefab != null)
            {
                var driver = Instantiate(driverPrefab, seat);
                driver.transform.localPosition = Vector3.zero;
                driver.transform.localRotation = Quaternion.identity;
                pose = driver.GetComponent<BlockyDriverPose>();
                pose?.Bind(kart.rb);
            }

            if (isPlayer)
            {
                go.AddComponent<KartHooks>().Bind(kart, pose, true);
                // A test hook for the phone: with a file named "autopilot" in the app's own files folder (only adb puts
                // one there), the field's AI drives the player's kart, so a whole race can be watched on the device.
                if (DeviceAutopilot) { var ai = go.AddComponent<KartAi>(); ai.Configure(def, first, character); Tune(ai); }
                else go.AddComponent<KartInputMobile>();
            }
            else if (!online)
            {
                go.AddComponent<KartHooks>().Bind(kart, pose, false);
                // Personality is seeded from the character, so the same rival drives the same way every time.
                var ai = go.AddComponent<KartAi>();
                ai.Configure(def, first, character);
                // the event's pace (Tour cups, bosses), to 1.2 at most: faster than that the AI cannot hold Pine Ridge
                ai.skill = Mathf.Clamp(ai.skill * RaceMode.Difficulty, 0.8f, 1.2f);
                ai.cornerBonus = Mathf.Clamp(RaceMode.Difficulty - 1f, 0f, 0.16f) * 0.5f;   // a quicker field is quicker through the corners too
                if (RaceMode.Current == RaceMode.Kind.Duel)   // a boss makes no unforced mistakes, never lets you go, and drives a maxed kart
                {
                    ai.steady = true; ai.maxCatchUp = 0.24f;
                    Tour.BossKart.ApplyTo(kart);
                }
            }
            // a remote kart gets no hooks and no input: RemoteKart poses the driver and spins the wheels itself
            // each driver's special (solo races; never in a Clean Race). A boss's charges faster: the card says so.
            if (!online && !Weekly.Racing && RaceMode.Rule != RaceMode.Items.Clean)
                Ability.On(kart, def.displayName, isPlayer).rate = !isPlayer && RaceMode.Current == RaceMode.Kind.Duel ? RaceMode.BossCharge : 1f;
            return kart;
        }
    }
}
