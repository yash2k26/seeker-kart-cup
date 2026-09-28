using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace KartCup
{
    /// Panel switching and scene loading for the whole front end. One place so every button does the same thing,
    /// every panel enters the same way, and Android back always has an answer. Also the room's hand on the wheel:
    /// when the room says "load this track" or "back to the lobby", the scene changes here.
    public sealed class ScreenFlow : MonoBehaviour
    {
        public static ScreenFlow Instance { get; private set; }

        [SerializeField] GameObject[] panels = System.Array.Empty<GameObject>();   // 0 title, 1 select, 2 board, 3 settings, 4 track, 5 rooms, 6 lobby, 7 garage
        [SerializeField] int startPanel;
        [SerializeField] CharacterDatabase roster;
        [SerializeField] TMP_Text playLabel;      // says CONTINUE CUP when one is half-run

        const int Select = 1, Track = 4, Rooms = 5, Lobby = 6, Garage = 7;

        /// Set before loading the menu: open straight into the lobby (rematch) or join a code (invite link).
        public static bool PendingLobby;
        public static string PendingJoin;

        MenuBackdrop backdrop;
        int current = -1;
        bool loading;        // a double tap on PLAY must not load the race twice
        float lastShow;      // ...nor may the second tap land on whatever the first tap just revealed underneath it

        bool IsMenu => panels.Length > 0;

        void Awake()
        {
            Instance = this;
            backdrop = FindFirstObjectByType<MenuBackdrop>();
            RoomClient.LoadRequested += OnLoadRequested;
            RoomClient.RoomChanged += OnRoomChanged;
            Application.deepLinkActivated += OnDeepLink;
        }

        void OnDestroy()
        {
            RoomClient.LoadRequested -= OnLoadRequested;
            RoomClient.RoomChanged -= OnRoomChanged;
            Application.deepLinkActivated -= OnDeepLink;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Show(startPanel, silent: true);
            // A cup already under way turns the front door into a continue: the character is settled, so re-picking
            // one would just throw the standings away.
            if (playLabel != null) playLabel.text = Resuming ? "CONTINUE CUP" : "PLAY";
            if (!IsMenu) return;
            if (PendingLobby && RoomClient.InRoom) { PendingLobby = false; Show(Lobby, silent: true); return; }
            PendingLobby = false;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnDeepLink(Application.absoluteURL);   // cold start from an invite link
            if (!string.IsNullOrEmpty(PendingJoin)) { var code = PendingJoin; PendingJoin = null; Show(Rooms, silent: true); RoomsPanel.Instance?.JoinFromLink(code); }
        }

        static bool Resuming => Cup.Active && Cup.RaceIndex > 0;

        /// The title's primary button. New cup goes through character select; a cup in flight goes straight to the grid.
        public void Play()
        {
            if (Resuming) ContinueCup();
            else ShowSelect();
        }

        void Update()
        {
            // Android back arrives as Escape. Deeper panels go home; home leaves the app. Never a dead press.
            // The race scene carries a ScreenFlow too (for the results buttons) with no panels — it must ignore Back
            // and leave it to PauseMenu, so the panel check comes first.
            if (!IsMenu || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (current == Lobby) { AudioDirector.Instance?.Back(); LeaveRoom(); Show(Rooms, silent: true); }        // back out of a lobby = leave it
            else if (current > 0) { AudioDirector.Instance?.Back(); Show(current == Track ? Select : 0, silent: true); }   // track select was reached from select, so back retraces one step
            else Application.Quit();
        }

        public void Show(int panel) => Show(panel, silent: false);

        void Show(int panel, bool silent)
        {
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && i != panel) UiMotion.Hide(panels[i]);
            if (panel >= 0 && panel < panels.Length)
            {
                var t = panels[panel].transform;
                UiMotion.Show(panels[panel], t.Find("Card"));   // cards scale; the title plate is edge-anchored, so it only fades
            }
            current = panel;
            lastShow = Time.unscaledTime;
            if (backdrop != null) backdrop.Focused = panel == Select || panel == Garage;   // hold a composed angle while a kart is on show; the track panel covers the vista
            if (silent) return;
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
        }

        public void ShowTitle() => Show(0);
        public void ShowSelect() => Show(Select);
        public void ShowLeaderboard() => Show(2);
        public void ShowSettings() => Show(3);
        public void ShowTrack() { if (Time.unscaledTime - lastShow >= 0.3f) Show(Track); }   // NEXT overlaps PLAY: a double tap must not skip character select
        public void ShowRooms() => Show(Rooms);
        public void ShowLobby() => Show(Lobby);
        public void ShowGarage() => Show(Garage);

        /// Starts a NEW cup — every track, or the one chosen on the track panel — with the selected character.
        /// Continuing an existing cup goes through ContinueCup.
        public void StartRace()
        {
            if (loading || Time.unscaledTime - lastShow < 0.3f) return;   // RACE sits exactly under NEXT: a double tap must not skip track select
            AudioDirector.Instance?.Confirm();
            int player = CharacterDatabase.SelectedIndex, track = TrackCarousel.Selection;
            KartApi.Event("cup_start", new { character = player, track });
            if (track < 0) Cup.Begin(player, RosterSize);
            else Cup.Begin(player, RosterSize, new[] { track });
            Load(Cup.SceneName);
        }

        /// Title screen, when a cup is already in flight: drop straight back into the race it stopped at.
        public void ContinueCup()
        {
            if (loading || !Cup.Active) return;
            AudioDirector.Instance?.Confirm();
            Load(Cup.SceneName);
        }

        int RosterSize => roster != null && roster.characters.Length > 0 ? roster.characters.Length : 8;

        // ---------- rooms ----------
        public void LeaveRoom() { if (RoomClient.Instance != null) RoomClient.Instance.Leave(); }

        /// The room says which track to load next: from the lobby for race one, from the results card after that.
        void OnLoadRequested(string trackId, int race, int races)
        {
            loading = false;                       // the results card may have set it; a new race is a new load
            Load(Tracks.SceneOf(Tracks.IndexOf(trackId)));
        }

        /// Rematch: the room went back to its lobby while we sit on a results card — everyone returns to the menu's lobby.
        void OnRoomChanged(RoomClient.RoomState room)
        {
            if (IsMenu || room == null || room.state != "lobby") return;
            PendingLobby = true;
            loading = false;
            Load("Menu");
        }

        /// https://…/r/CODE or kartcup://r/CODE: from a share, a browser or the invite page's button.
        void OnDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            var i = url.IndexOf("/r/", System.StringComparison.OrdinalIgnoreCase);
            if (i < 0) return;
            var code = url.Substring(i + 3).Trim('/').Split('?', '#')[0].ToUpperInvariant();
            if (code.Length != 5) return;
            KartApi.Event("invite_open", new { code });
            if (IsMenu) { Show(Rooms, silent: true); RoomsPanel.Instance?.JoinFromLink(code); }
            else PendingJoin = code;   // mid-race: pick it up when the menu comes back
        }

        public void BackToMenu()
        {
            LeaveRoom();   // quitting a race, or leaving the results card, is leaving the room
            Load("Menu");
        }
        public void LoadCupRace() => Load(Cup.SceneName);
        public void Restart() => Load(SceneManager.GetActiveScene().name);

        void Load(string scene)
        {
            if (loading) return;
            loading = true;
            Time.timeScale = 1f;   // pause and hit-stop must never leak into the next scene
            SceneManager.LoadScene(scene);
        }
    }
}
