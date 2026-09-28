using UnityEngine;
using UnityEngine.InputSystem;

namespace KartCup
{
    /// Race pause. Android back (Escape) and the pause button toggle it; time, physics and audio freeze while the
    /// UI keeps its unscaled motion. After the finish, back is a one-way door out to the menu, never into the race.
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject overlay;
        [SerializeField] GameObject[] hideWhilePaused = System.Array.Empty<GameObject>();   // the pause button itself
        [SerializeField] GameObject restartButton;        // meaningless online, so it hides there

        public bool Paused { get; private set; }

        void Start()
        {
            if (overlay != null) overlay.SetActive(false);
            // Pausing after the finish is refused, so leaving the button on screen over the results card is a dead control.
            RaceManager.Instance?.onFinish.AddListener(p =>
            {
                if (!p.isPlayer) return;
                foreach (var go in hideWhilePaused) if (go != null) go.SetActive(false);
            });
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            var race = RaceManager.Instance;
            if (race != null && race.state == RaceManager.State.Finished) { ScreenFlow.Instance?.BackToMenu(); return; }
            Toggle();
        }

        public void Toggle() { if (Paused) Resume(); else Pause(); }

        public void Pause()
        {
            var race = RaceManager.Instance;
            if (Paused || (race != null && race.state == RaceManager.State.Finished)) return;
            Paused = true;
            // Online the race clock is everyone's clock: the overlay opens, but nothing stops and there is no restart.
            bool online = RoomClient.InRoom;
            if (restartButton != null) restartButton.SetActive(!online);
            if (!online) { Time.timeScale = 0f; HitStop.Paused = true; AudioListener.pause = true; }
            foreach (var go in hideWhilePaused) if (go != null) go.SetActive(false);
            UiMotion.Show(overlay, overlay != null && overlay.transform.childCount > 0 ? overlay.transform.GetChild(0) : null);
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
        }

        public void Resume()
        {
            if (!Paused) return;
            Paused = false;
            UiMotion.Hide(overlay);
            foreach (var go in hideWhilePaused) if (go != null) go.SetActive(true);
            AudioListener.pause = false;
            HitStop.Paused = false;
            Time.timeScale = 1f;
            AudioDirector.Instance?.Back();
        }

        public void Restart() { if (RoomClient.InRoom) return; Resume(); ScreenFlow.Instance?.Restart(); }
        public void Quit() { Resume(); ScreenFlow.Instance?.BackToMenu(); }   // online: BackToMenu leaves the room

        void OnDestroy() { if (Paused) { Time.timeScale = 1f; AudioListener.pause = false; HitStop.Paused = false; } }
    }
}
