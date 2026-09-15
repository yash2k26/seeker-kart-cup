using System.Linq;
using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// Puts the grid together at runtime: the character you picked in the player slot, the rest of the roster as AI,
    /// each on its own kart with its own stats. Keeping this at runtime (not baked into the scene) is what lets the
    /// select screen — and later a room's slot list — decide who is on the grid.
    public sealed class RaceSpawner : MonoBehaviour
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] GameObject[] kartPrefabs;       // Kart_oobi … Kart_oozi
        [SerializeField] GameObject[] driverPrefabs;     // Driver_NodeRunner …
        [SerializeField] Transform[] gridSlots;
        [SerializeField] BasicWaypoint firstWaypoint;
        [SerializeField] KartCamera raceCamera;
        [SerializeField] int racers = 6;

        void Start()
        {
            if (roster == null || roster.characters.Length == 0) { Debug.LogError("[race] no roster"); return; }
            int playerIndex = CharacterDatabase.SelectedIndex;
            int count = Mathf.Min(racers, gridSlots.Length);

            for (int slot = 0; slot < count; slot++)
            {
                var def = roster.At(playerIndex + slot);          // you start on pole, the rest of the roster lines up behind
                bool isPlayer = slot == 0;
                var kart = Spawn(def, gridSlots[slot], isPlayer);
                if (isPlayer && raceCamera != null) raceCamera.Initialize(kart);
            }
            RaceManager.Instance?.Rescan();
            RaceManager.Instance?.BeginCountdown();
        }

        Kart Spawn(CharacterDef def, Transform slot, bool isPlayer)
        {
            var prefab = kartPrefabs.FirstOrDefault(k => k.name.EndsWith(def.kartId)) ?? kartPrefabs[0];
            var go = Instantiate(prefab, slot.position, slot.rotation);
            go.name = (isPlayer ? "Player_" : "AI_") + def.displayName;
            var kart = go.GetComponent<Kart>();
            def.ApplyTo(kart);

            var progress = go.AddComponent<KartProgress>();
            progress.racerName = def.displayName;
            progress.isPlayer = isPlayer;

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

            go.AddComponent<KartHooks>().Bind(kart, pose, isPlayer);

            if (isPlayer) go.AddComponent<KartInputMobile>();
            else
            {
                var ai = go.AddComponent<KartAi>();
                ai.SetTarget(firstWaypoint);
                ai.skill = Random.Range(0.88f, 0.99f);
            }
            return kart;
        }
    }
}
