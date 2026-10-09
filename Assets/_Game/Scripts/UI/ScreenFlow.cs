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
        CharacterCarousel carousel;
        [SerializeField] TMP_Text playLabel;      // says CONTINUE CUP when one is half-run

        const int Select = 1, Track = 4, Rooms = 5, Lobby = 6, Garage = 7, Items = 8, WeeklyScreen = 9, TourScreen = 10, AchievementsScreen = 11, GemsScreen = 12;
        // How deep each panel sits under the title: going deeper slides in from the right, coming back from the left.
        static readonly int[] Depth = { 0, 1, 1, 1, 2, 1, 2, 1, 1, 1, 1, 1 };

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
            if (backdrop != null && roster != null) backdrop.Home = backdrop.BayOf(roster.Selected?.displayName);   // the title shows your driver
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
            if (IsMenu) { Weekly.Racing = false; RaceMode.Clear(); }   // back from a time trial or an event
            Show(startPanel, silent: true);
            // A cup already under way turns the front door into a continue: the character is settled, so re-picking
            // one would just throw the standings away.
            if (playLabel != null) playLabel.text = Resuming ? "CONTINUE CUP" : "PLAY";
            if (!IsMenu) return;
            if (PendingLobby && RoomClient.Member) { PendingLobby = false; Show(Lobby, silent: true); return; }
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
            if (current == Lobby) { AudioDirector.Instance?.Back(); LeaveRoom(); Show(Rooms, silent: false); }        // back out of a lobby = leave it
            else if (current > 0) { AudioDirector.Instance?.Back(); Show(current == Track ? Select : current == Items ? Garage : 0, silent: false); }   // back retraces one step: track to select, items to karts
            else Application.Quit();
        }

        public void Show(int panel) => Show(panel, silent: false);

        /// `silent`: a jump the player did not ask for (start-up, an invite link, a rematch) fades in place; a step
        /// they took slides the way they went. The tap itself already ticked and clicked (PressFeel).
        void Show(int panel, bool silent)
        {
            int dir = silent ? 0 : Direction(current, panel);
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && i != panel) UiMotion.Hide(panels[i], dir);
            if (panel >= 0 && panel < panels.Length)
            {
                var t = panels[panel].transform;
                UiMotion.Show(panels[panel], t.Find("Card"), dir);   // cards scale; the title plate is edge-anchored, so it only fades
            }
            current = panel;
            lastShow = Time.unscaledTime;
            if (panel == 0) backToTour = false;   // left the select screen by BACK: PLAY goes on to the tracks again
            if (backdrop != null) backdrop.Focused = panel == Select || panel == Garage || panel == Items;   // hold a composed angle while a kart is on show; the track panel covers the vista
            if (backdrop != null) backdrop.Close = panel == Select;                                          // the driver screen frames a person, close
            if (backdrop != null)
            {   // the street select's bay only while the driver screen is up; the garage and items use the turntable
                if (panel == Select) { carousel ??= FindFirstObjectByType<CharacterCarousel>(); carousel?.Refocus(); }
                else backdrop.Spot = null;
            }
        }

        int Direction(int from, int to)
        {
            if (from < 0 || to < 0 || from == to) return 0;
            if ((from == Garage && to == Items) || (from == Items && to == Garage)) return to == Items ? 1 : -1;   // the shop's tabs, left to right
            int a = from < Depth.Length ? Depth[from] : 1, b = to < Depth.Length ? Depth[to] : 1;
            return b > a ? 1 : b < a ? -1 : 0;
        }

        public void ShowTitle() => Show(0);
        public void ShowSelect() => Show(Select);
        public void ShowLeaderboard() => Show(2);
        public void ShowSettings() => Show(3);
        public void ShowTrack()
        {
            if (Time.unscaledTime - lastShow < 0.3f) return;   // NEXT overlaps PLAY: a double tap must not skip character select
            if (current == Select) { carousel ??= FindFirstObjectByType<CharacterCarousel>(); if (carousel != null && carousel.Locked) { carousel.Recruit(); return; } }   // a judge not yet recruited or beaten
            if (current == Select && roster != null) Voices.Say(roster.At(CharacterDatabase.SelectedIndex)?.driverId, "go");   // the chosen driver is in
            if (backToTour) { backToTour = false; Show(TourScreen); return; }        // the driver was changed from the Tour
            Show(Track);
        }
        public void ShowRooms() => Show(Rooms);
        public void ShowLobby() => Show(Lobby);
        public void ShowGarage() => Show(Garage);
        public void ShowItems() => Show(Items);
        public void ShowWeekly() => Show(WeeklyScreen);
        public void ShowTour() => Show(TourScreen);
        int gemsFrom;
        /// The gem shop, from the title's chip, the driver card or a short balance; BACK returns to wherever that was.
        public void ShowGems() { if (current != GemsScreen) gemsFrom = current; Show(GemsScreen); }
        public void BackFromGems() => Show(gemsFrom == GemsScreen ? 0 : gemsFrom);
        public void ShowAchievements() => Show(AchievementsScreen);

        bool backToTour;
        /// The Tour's CHANGE: the select screen, whose NEXT then comes back to the Tour.
        public void ChangeDriverFromTour() { backToTour = true; Show(Select); }

        /// A Tour event with the selected driver: its circuit, mode, rival and pace.
        public void StartTourEvent(Tour.Event e, Tour.CupDef c)
        {
            if (loading) return;
            AudioDirector.Instance?.Confirm();
            Weekly.Racing = false;
            Tour.Arm(e, c);
            Cup.Begin(CharacterDatabase.SelectedIndex, RosterSize, new[] { e.track });
            Load(Cup.SceneName);
        }

        /// Today's Daily Challenge with the selected driver.
        public void StartDaily()
        {
            if (loading) return;
            AudioDirector.Instance?.Confirm();
            Weekly.Racing = false;
            var c = Daily.Current;
            Daily.Arm(c);
            Cup.Begin(CharacterDatabase.SelectedIndex, RosterSize, new[] { c.track });
            Load(Cup.SceneName);
        }

        /// Kartel Weekly's time trial: this week's circuit with the selected driver, alone on track (RaceSpawner).
        public void StartWeekly()
        {
            if (loading || Weekly.TrackIndex < 0) return;
            AudioDirector.Instance?.Confirm();
            RaceMode.Clear();
            Weekly.Racing = true;
            KartApi.Event("weekly_start", new { track = Weekly.Current.track });
            Cup.Begin(CharacterDatabase.SelectedIndex, RosterSize, new[] { Weekly.TrackIndex });
            Load(Cup.SceneName);
        }

        /// A single race on one circuit with the selected driver (the rival toast's RACE).
        public void RaceOn(int track)
        {
            if (loading || track < 0 || track >= Tracks.Count) return;
            AudioDirector.Instance?.Confirm();
            RaceMode.Clear();
            Cup.Begin(CharacterDatabase.SelectedIndex, RosterSize, new[] { track });
            Load(Cup.SceneName);
        }

        /// Starts a NEW cup — every track, or the one chosen on the track panel — with the selected character.
        /// Continuing an existing cup goes through ContinueCup.
        public void StartRace()
        {
            if (loading || Time.unscaledTime - lastShow < 0.3f) return;   // RACE sits exactly under NEXT: a double tap must not skip track select
            AudioDirector.Instance?.Confirm();
            RaceMode.Clear();
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

        static string loadedFor;   // "CODE:round:race index" of the room race this phone last loaded

        /// The room says which track to load next: from the lobby for race one, from the results card after that.
        void OnLoadRequested(string trackId, int race, int races)
        {
            loading = false;                       // the results card may have set it; a new race is a new load
            var r = RoomClient.Instance != null ? RoomClient.Instance.Room : null;
            loadedFor = r != null ? $"{r.code}:{r.round}:{race - 1}" : null;
            Load(Tracks.SceneOf(Tracks.IndexOf(trackId)));
        }

        /// Rematch: the room went back to its lobby while we sit on a results card — everyone returns to the menu's lobby.
        void OnRoomChanged(RoomClient.RoomState room)
        {
            if (room == null) return;
            if (room.state == "lobby") loadedFor = null;   // back in the lobby, the next start is a new load even in the same round
            // A `load` that went out while this phone was reconnecting is never sent again, but the room's snapshot
            // says which track is loading: load it, or the room starts without us.
            if (room.state == "loading" && !loading && room.raceIndex >= 0 && room.raceIndex < room.tracks.Count
                && loadedFor != $"{room.code}:{room.round}:{room.raceIndex}")
            {
                OnLoadRequested(room.tracks[room.raceIndex], room.raceIndex + 1, room.tracks.Count);
                return;
            }
            if (IsMenu || room.state != "lobby") return;
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
            // Under the curtain: the tap is answered with where you are going while the scene loads behind it.
            int number = 0;
            bool race = scene.StartsWith("Race") && int.TryParse(scene.Substring(4), out number);
            var track = race ? Tracks.At(number - 1) : null;
            string heading = race ? track.name.ToUpperInvariant() : "KARTEL";
            string sub = race ? (Cup.Active && Cup.Races > 1 ? $"RACE {Cup.RaceNumber} OF {Cup.Races}  ·  {track.kicker}" : track.kicker) : "BACK TO THE COAST";
            if (!LoadingCurtain.Load(scene, heading, sub)) SceneManager.LoadScene(scene);
        }
    }
}
