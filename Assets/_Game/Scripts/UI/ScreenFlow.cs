using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartCup
{
    /// Panel switching and scene loading for the whole front end. One place so every button does the same thing.
    public sealed class ScreenFlow : MonoBehaviour
    {
        public static ScreenFlow Instance { get; private set; }

        [SerializeField] GameObject[] panels = System.Array.Empty<GameObject>();   // 0 = title, 1 = select, 2 = leaderboard, 3 = settings
        [SerializeField] int startPanel;

        void Awake() { Instance = this; }
        void Start() { Show(startPanel); }

        public void Show(int panel)
        {
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null) panels[i].SetActive(i == panel);
            Haptics.Play(Haptics.Kind.Tick);
        }

        public void ShowTitle() => Show(0);
        public void ShowSelect() => Show(1);
        public void ShowLeaderboard() => Show(2);
        public void ShowSettings() => Show(3);

        public void StartRace()
        {
            KartApi.Event("race_enter", new { character = CharacterDatabase.SelectedIndex });
            SceneManager.LoadScene("Race");
        }

        public void BackToMenu() => SceneManager.LoadScene("Menu");
        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
