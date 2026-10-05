using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The fastest laps on the Worker, a track at a time: a tab per track, ten rows (medals for the top three, your
    /// own row lit). Loading, empty and offline are one calm line in the middle of the table, never a blank card.
    public sealed class LeaderboardFeed : MonoBehaviour
    {
        [SerializeField] TMP_Text rows;                    // the one-line state: loading / empty / offline
        [SerializeField] string track = "beach";
        [SerializeField] GameObject[] rowObjects = System.Array.Empty<GameObject>();
        [SerializeField] Image[] rowBacks = System.Array.Empty<Image>();
        [SerializeField] Image[] medals = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] ranks = System.Array.Empty<TMP_Text>(), names = System.Array.Empty<TMP_Text>(), times = System.Array.Empty<TMP_Text>();
        [SerializeField] Image[] tabs = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] tabLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] Color rowIdle, rowMine, tabOn, tabOff, textOn, textOff;
        [SerializeField] Color[] medalColours = System.Array.Empty<Color>();

        int asked;     // a newer tab press wins over a slower answer to an older one

        void OnEnable() => Load();

        /// A track tab.
        public void SetTrack(int i)
        {
            var id = Tracks.At(i).id;
            if (id == track && isActiveAndEnabled) return;
            track = id;
            Load();
        }

        async void Load()
        {
            if (rows == null) return;
            int me = ++asked;
            MarkTabs();
            ShowRows(0);
            rows.gameObject.SetActive(true);
            rows.text = "Loading the fastest laps…";
            try
            {
                var board = await KartApi.Leaderboard(track);
                if (me != asked || this == null) return;
                int n = board == null ? 0 : Mathf.Min(board.entries.Count, rowObjects.Length);
                rows.gameObject.SetActive(n == 0);
                rows.text = "No times on this track yet.\nSet the first one.";
                string mine = WalletFlow.Instance != null && WalletFlow.Instance.Profile != null ? WalletFlow.Instance.Profile.wallet : null;
                for (int i = 0; i < n; i++)
                {
                    var e = board.entries[i];
                    if (i < ranks.Length && ranks[i] != null) ranks[i].text = e.rank.ToString();
                    if (i < names.Length && names[i] != null) names[i].text = Fit(e.name) + (e.isSeeker ? "  <size=70%><color=#B4A7DE>SEEKER</color></size>" : "");
                    if (i < times.Length && times[i] != null) times[i].text = RaceHud.Clock(e.bestMs / 1000f);
                    if (i < medals.Length && medals[i] != null)
                    {
                        medals[i].enabled = e.rank <= medalColours.Length;
                        if (medals[i].enabled) medals[i].color = medalColours[e.rank - 1];
                    }
                    if (i < rowBacks.Length && rowBacks[i] != null) rowBacks[i].color = mine != null && e.wallet == mine ? rowMine : rowIdle;
                }
                ShowRows(n);
            }
            catch (System.Exception e)
            {
                if (me != asked || this == null) return;
                Debug.LogWarning("[board] " + e.Message);
                rows.gameObject.SetActive(true);
                rows.text = "Can't reach the leaderboard.\nCheck your connection and try again.";
            }
        }

        void ShowRows(int n)
        {
            for (int i = 0; i < rowObjects.Length; i++)
            {
                if (rowObjects[i] == null) continue;
                bool was = rowObjects[i].activeSelf;
                rowObjects[i].SetActive(i < n);
                if (i < n && !was && Application.isPlaying)       // rows land one after another
                {
                    var t = rowObjects[i].transform;
                    t.localScale = new Vector3(1f, 0.6f, 1f);
                    Tween.Scale(t, Vector3.one, 0.24f, Ease.OutBack, startDelay: 0.03f * i, useUnscaledTime: true);
                }
            }
        }

        void MarkTabs()
        {
            int at = Tracks.IndexOf(track);
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].color = i == at ? tabOn : tabOff;
                if (i < tabLabels.Length && tabLabels[i] != null) tabLabels[i].color = i == at ? textOn : textOff;
            }
        }

        /// A name longer than its column is clipped, not run into the time beside it.
        static string Fit(string name) => string.IsNullOrEmpty(name) ? "—" : name.Length <= 22 ? name : name[..21] + "…";
    }
}
