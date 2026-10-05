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

        void Start()
        {
            if (roster == null || roster.characters.Length == 0) { Debug.LogError("[race] no roster"); return; }
            if (RoomClient.InRaceRoom) { SpawnOnline(); return; }

            // A race entered outside a cup (editor playtest, a direct scene load) still needs a grid.
            if (!Cup.Active)
            {
                // The scene's own track, so a directly loaded Race3 records its best as forest and RACE AGAIN reloads Race3.
                int.TryParse(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Replace("Race", ""), out int n);
                Cup.Begin(CharacterDatabase.SelectedIndex, roster.characters.Length, new[] { Mathf.Clamp(n - 1, 0, Tracks.Count - 1) });
            }
            int count = Mathf.Min(racers, gridSlots.Length);

            for (int slot = 0; slot < count && slot < Cup.Grid.Count; slot++)
            {
                // The grid comes from the cup: pole on race one, then the standings reversed, so leading costs you
                // track position next time out.
                int character = Cup.Grid[slot];
                var def = roster.At(character);
                bool isPlayer = character == Cup.PlayerCharacter;
                var kartId = isPlayer ? PlayerKart(def, allowTrial: true) : def.kartId;
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
            Loadout(race);
            if (show == null) { race.BeginCountdown(); yield break; }
            race.Hold();
            TurboPad.Spawn(System.Environment.TickCount);
            OilSlick.Spawn(System.Environment.TickCount + 1, Tracks.At(Cup.TrackIndex).difficulty);   // solo only: rooms never get them
            show.Prizes(Tracks.At(Cup.TrackIndex).kicker, Tracks.At(Cup.TrackIndex).name.ToUpperInvariant(), new[] { "1ST", "2ND", "3RD" });
            float until = Time.time + show.IntroSeconds, skippable = Time.time + 0.6f;
            while (Time.time < until && !(Time.time > skippable && PodiumShow.Tapped())) yield return null;
            show.End();
            race.BeginCountdown();
        }

        /// The consumable the shop armed for this race: one out of its pack, handed to the player's kart on the grid.
        static void Loadout(RaceManager race)
        {
            var player = race.Player; var pack = Inventory.Find(Inventory.Loadout);
            var items = FindAnyObjectByType<ItemManager>();
            if (player == null || pack == null || items == null) return;
            var caster = player.GetComponent<ItemCaster>();
            var item = items.GetItem(pack.item);
            if (caster == null || item == null || !Inventory.Take(pack.id)) return;
            caster.GiveItem(item, 1, true);
            KartApi.Event("loadout", new { item = pack.id });
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
            bool paidRoom = online && RoomClient.Instance != null && RoomClient.Instance.Room.IsPaid;
            if (!paidRoom) garage?.Find(kartId)?.ApplyTo(kart);
            if (isPlayer) CharacterDef.Paint(go, Paints.ColorFor(def)); else def.ApplyLivery(go);
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

            if (isPlayer) { go.AddComponent<KartHooks>().Bind(kart, pose, true); go.AddComponent<KartInputMobile>(); }
            else if (!online)
            {
                go.AddComponent<KartHooks>().Bind(kart, pose, false);
                // Personality is seeded from the character, so the same rival drives the same way every time.
                go.AddComponent<KartAi>().Configure(def, firstWaypoint, character);
            }
            // a remote kart gets no hooks and no input: RemoteKart poses the driver and spins the wheels itself
            return kart;
        }
    }
}
