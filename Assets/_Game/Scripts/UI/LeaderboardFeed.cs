using System.Collections.Generic;
using System.Linq;
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

        // 13 Oct 2026, the user: "what is ranking for, when it shows nothing". With few players signed in a board was
        // empty, so it always holds two more things: your own best lap here (lit), and the five judges' par times (the
        // Tour bosses at their pace over a solid lap: the phone's autopilot at skill 1.08 on 11 Oct, plus 3%).
        internal static readonly Dictionary<string, float> SolidLap = new() { ["beach"] = 58.5f, ["alpine"] = 58.1f, ["forest"] = 46.8f, ["speedway"] = 58.8f, ["volcano"] = 49.2f };
        internal static readonly (string name, float pace)[] Judges = { ("Mert", 1.04f), ("Akshay", 1.07f), ("Chase", 1.10f), ("Toly", 1.13f), ("Beeman", 1.15f) };
        sealed class Line { public string name, wallet; public float seconds; public bool judge, me, seeker; }

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
            KartApi.Board board = null;
            try { board = await KartApi.Leaderboard(track); }
            catch (System.Exception e) { Debug.LogWarning("[board] " + e.Message); }   // offline: the judges and your best still show
            if (me != asked || this == null) return;
            string mine = WalletFlow.Instance != null && WalletFlow.Instance.Profile != null ? WalletFlow.Instance.Profile.wallet : null;
            Fill(Merge(board, mine));
        }

        /// MenuPreview: the card as it is offline (the judges and this machine's best), since OnEnable never runs in edit mode.
        public void ShowInEditor() { if (rows == null) return; MarkTabs(); Fill(Merge(null, null)); }

        void Fill(List<(int rank, Line line)> lines)
        {
            int n = Mathf.Min(lines.Count, rowObjects.Length);
            rows.gameObject.SetActive(n == 0); if (n == 0) rows.text = "No laps here yet.";
            for (int i = 0; i < n; i++)
            {
                var (rank, l) = lines[i];
                if (i < ranks.Length && ranks[i] != null) ranks[i].text = rank.ToString();
                string tag = l.judge ? "JUDGE" : l.me ? (l.name == "You" ? "" : "YOU") : l.seeker ? "SEEKER" : "";
                if (i < names.Length && names[i] != null) names[i].text = Fit(l.name) + (tag != "" ? $"  <size=70%><color={(l.me ? "#FFB126" : "#B4A7DE")}>{tag}</color></size>" : "");
                if (i < times.Length && times[i] != null) times[i].text = RaceHud.Clock(l.seconds);
                if (i < medals.Length && medals[i] != null)
                {
                    medals[i].enabled = rank <= medalColours.Length;
                    if (medals[i].enabled) medals[i].color = medalColours[rank - 1];
                }
                if (i < rowBacks.Length && rowBacks[i] != null) rowBacks[i].color = l.me ? rowMine : rowIdle;
            }
            ShowRows(n);
        }

        /// The server's laps, the judges' par times and your own best, fastest first, each with its rank; your row stays
        /// on the card even when it is below the last one shown (it takes the last place, with its real rank).
        List<(int rank, Line line)> Merge(KartApi.Board board, string mine)
        {
            var all = new List<Line>();
            bool meOnBoard = false;
            if (board != null)
                foreach (var e in board.entries)
                {
                    bool isMe = mine != null && e.wallet == mine; meOnBoard |= isMe;
                    all.Add(new Line { name = e.name, wallet = e.wallet, seconds = e.bestMs / 1000f, me = isMe, seeker = e.isSeeker });
                }
            float best = Tracks.Best(track);
            if (!meOnBoard && best > 0f) all.Add(new Line { name = WalletFlow.Instance?.Profile?.skrName ?? "You", seconds = best, me = true });
            if (SolidLap.TryGetValue(track, out float solid))
                foreach (var (name, pace) in Judges) all.Add(new Line { name = name, seconds = solid / pace, judge = true });
            var ranked = all.OrderBy(l => l.seconds).Select((l, i) => (i + 1, l)).ToList();
            int shown = rowObjects.Length;
            int at = ranked.FindIndex(r => r.l.me);
            var list = ranked.Take(shown).ToList();
            if (at >= shown && shown > 0) list[shown - 1] = ranked[at];
            return list;
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
