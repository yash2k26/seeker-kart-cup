using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// RACE ONLINE: make a room (which tracks, the smallest stake it takes) or join one by its five-letter code. The
    /// lobby takes over the moment the room answers.
    public sealed class RoomsPanel : MonoBehaviour
    {
        public static RoomsPanel Instance { get; private set; }

        [SerializeField] TMP_InputField codeField;
        [SerializeField] TMP_Text trackLabel, stakeLabel, visibilityLabel, status;
        [SerializeField] Button createButton, joinButton;
        [SerializeField] TMP_Text[] roomRows = System.Array.Empty<TMP_Text>();   // the open-rooms list, top to bottom
        [SerializeField] Button[] roomButtons = System.Array.Empty<Button>();
        [SerializeField] CharacterDatabase roster;
        [SerializeField] KartDatabase garage;

        // A room's smallest stake, which the host picks: each player then stakes that or more. Base units; the Worker
        // refuses anything off its list (catalog.ts STAKE_TIERS).
        static readonly (string label, string minStake, string currency)[] Stakes =
        {
            ("FREE RACE", "0", "SOL"),
            ("STAKES FROM 0.01 SOL", "10000000", "SOL"), ("STAKES FROM 0.05 SOL", "50000000", "SOL"), ("STAKES FROM 0.1 SOL", "100000000", "SOL"), ("STAKES FROM 0.15 SOL", "150000000", "SOL"),
            ("STAKES FROM 5 SKR", "5000000", "SKR"), ("STAKES FROM 25 SKR", "25000000", "SKR"), ("STAKES FROM 100 SKR", "100000000", "SKR"),
        };

        int trackChoice = -1;   // -1 = Grand Prix (every track)
        int stakeChoice;
        bool isPublic = true;   // Among Us default: a room you make is open to anyone unless you say otherwise
        bool busy, attested;
        List<KartApi.PublicRoom> open = new();
        int pollGeneration;

        void Awake() { Instance = this; }
        void OnEnable()
        {
            busy = false;
            if (status != null) status.text = "";
            RoomClient.Error += OnError;
            Refresh();
            if (Application.isPlaying) _ = Poll(++pollGeneration);
        }
        void OnDisable() { RoomClient.Error -= OnError; pollGeneration++; }

        /// The open-rooms list refreshes every few seconds while this panel is on screen. One request, no sockets.
        async Awaitable Poll(int generation)
        {
            // Every 10 s, and less often while it fails: phones on one Wi-Fi share the Worker's per-address budget.
            float every = 10f;
            while (generation == pollGeneration && isActiveAndEnabled)
            {
                try { open = (await KartApi.OpenRooms())?.rooms ?? new List<KartApi.PublicRoom>(); ShowRooms(null); every = 10f; }
                catch (System.Exception e) { open = new List<KartApi.PublicRoom>(); ShowRooms(e is KartApi.ApiException ? "Can't reach the room list right now" : "Offline"); every = Mathf.Min(every * 2f, 60f); }
                await Awaitable.WaitForSecondsAsync(every);
            }
        }

        void ShowRooms(string problem)
        {
            for (int i = 0; i < roomRows.Length; i++)
            {
                if (roomRows[i] == null) continue;
                bool has = i < open.Count;
                var r = has ? open[i] : null;
                roomRows[i].text = has
                    ? $"{r.code}<pos=24%>{Fit(r.host)}<pos=58%>{r.players}/{r.maxPlayers}<pos=72%>{(r.tracks.Count > 1 ? "GRAND PRIX" : Tracks.At(Tracks.IndexOf(r.tracks[0])).name.ToUpperInvariant())}<pos=90%>{(r.minStake == "0" ? "FREE" : WalletFlow.Amount(r.minStake, r.currency) + "+")}"
                    : i == 0 ? $"<color=#B4A7DE>{problem ?? "No open rooms right now — make yours public"}</color>" : "";
                if (roomButtons.Length > i && roomButtons[i] != null) roomButtons[i].interactable = has;
            }
        }

        static string Fit(string name) => string.IsNullOrEmpty(name) || name.Length <= 14 ? name : name[..13] + "…";

        /// Tap on a listed room: join it.
        public void JoinRow(int i) { if (!busy && i >= 0 && i < open.Count) _ = Join(open[i].code); }

        public void ToggleVisibility() { isPublic = !isPublic; Tick(); Refresh(); }
        void OnError(string msg) { if (status != null && isActiveAndEnabled) status.text = msg; busy = false; }

        public void NextTrack() { trackChoice = trackChoice + 1 >= Tracks.Count ? -1 : trackChoice + 1; Tick(); Refresh(); }
        public void NextStake()
        {
            stakeChoice = (stakeChoice + 1) % Stakes.Length;
            if (Stakes[stakeChoice].minStake != "0" && KartApi.Cfg != null && !KartApi.Cfg.paidRooms) stakeChoice = 0;   // paid rooms off here: only FREE is offered
            Tick(); Refresh();
        }

        static void Tick() { Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click(); }

        void Refresh()
        {
            if (trackLabel != null) trackLabel.text = trackChoice < 0 ? $"GRAND PRIX  ·  {Tracks.Count} RACES" : Tracks.At(trackChoice).name.ToUpperInvariant();
            if (stakeLabel != null) stakeLabel.text = Stakes[stakeChoice].label;
            if (visibilityLabel != null) visibilityLabel.text = isPublic ? "PUBLIC  ·  ANYONE CAN JOIN" : "PRIVATE  ·  INVITE ONLY";
        }

        public async void Create()
        {
            if (busy) return;
            var f = Stakes[stakeChoice];
            if (f.minStake != "0")
            {
                if (WalletFlow.Instance == null || !WalletFlow.Instance.SignedIn)
                {
                    Say("Sign in with your wallet to host a paid race");
                    if (WalletFlow.Instance != null && !await WalletFlow.Instance.LoginAsync()) return;
                }
                // One honest line, once: paid play is for adults where skill contests are legal (research/20).
                if (!attested && PlayerPrefs.GetInt("kc.attest", 0) == 0)
                {
                    attested = true;
                    Say("Paid races are for players 18+ where skill contests are legal. Tap CREATE again to confirm.");
                    return;
                }
                PlayerPrefs.SetInt("kc.attest", 1);
            }
            busy = true;
            Say("Creating room…");
            var tracks = trackChoice < 0 ? Tracks.All.Select(t => t.id).ToList() : new List<string> { Tracks.At(trackChoice).id };
            try
            {
                var created = await KartApi.CreateRoom(tracks, f.minStake, f.currency, isPublic);
                KartApi.Event("room_create", new { minStake = f.minStake, currency = f.currency, tracks = tracks.Count, isPublic });
                await Join(created.code);
            }
            catch (KartApi.ApiException e)
            {
                Say(e.Status == 451 ? "Paid races aren't available in your region" : e.Status == 403 ? "Paid rooms are switched off right now"
                    : e.Status == 401 ? "Sign in first" : e.Status == 426 ? "Update the app to host paid races" : "Couldn't create the room. Check your connection.");
            }
            catch (System.Exception e) { Debug.LogWarning("[rooms] " + e.Message); Say("Couldn't create the room. Check your connection."); }
            finally { busy = false; }
        }

        public void JoinTyped() { if (!busy) _ = Join(codeField != null ? codeField.text : ""); }

        /// From an invite link: the code arrives already typed, and the player decides. A link must not drop anyone
        /// into a room on its own: joining shows your name (and a signed-in wallet) to everyone in it.
        public void JoinFromLink(string code)
        {
            if (codeField != null) codeField.text = code;
            Say($"Invited to room {code}. Tap JOIN to race.");
        }

        async Awaitable Join(string raw)
        {
            var code = new string((raw ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (code.Length != 5) { Say("Room codes are 5 letters"); return; }
            busy = true;
            Say($"Joining {code}…");
            var def = roster != null ? roster.Selected : null;
            var kart = garage != null ? garage.EffectiveKartFor(def, allowTrial: false) : (def != null ? def.kartId : "oodi");
            var client = RoomClient.Get();
            bool ok = await client.Join(code, CharacterDatabase.SelectedIndex, kart);
            busy = false;
            if (ok) { Tick(); ScreenFlow.Instance?.ShowLobby(); }
            else { if (status != null && (string.IsNullOrEmpty(status.text) || status.text.StartsWith("Joining"))) Say("No room with that code"); client.Leave(); }
        }


        void Say(string s) { if (status != null) status.text = s; }
    }
}
