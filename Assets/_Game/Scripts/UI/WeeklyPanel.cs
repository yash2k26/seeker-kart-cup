using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Kartel Weekly's screen: this week's circuit, the clock, the prizes, the rules in one line (and the full rules a
    /// tap away), the board, and RACE (the time trial). The board shows everyone under nine entrants; otherwise the top
    /// three and the places either side of you, so where you stand is always on screen.
    public sealed class WeeklyPanel : MonoBehaviour
    {
        [SerializeField] Image art;
        [SerializeField] Sprite[] trackArt = System.Array.Empty<Sprite>();
        [SerializeField] TMP_Text trackName, clock, prizes, mine, state;
        [SerializeField] GameObject[] rowObjects = System.Array.Empty<GameObject>();
        [SerializeField] Image[] rowBacks = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] ranks = System.Array.Empty<TMP_Text>(), names = System.Array.Empty<TMP_Text>(),
                                    times = System.Array.Empty<TMP_Text>(), prizeLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] Color rowIdle, rowMine;

        float next;

        void OnEnable() { Weekly.Changed += Show; Show(); if (Application.isPlaying) _ = Load(); }
        void OnDisable() => Weekly.Changed -= Show;
        void Update() { if (Time.unscaledTime > next) { next = Time.unscaledTime + 15f; ShowClock(); } }

        async Awaitable Load()
        {
            if (state != null && Weekly.Current == null) state.text = "Loading this week…";
            var b = await Weekly.Fetch(force: true);
            if (this != null && b == null && state != null) state.text = "Can't reach Kartel Weekly.\nCheck your connection and try again.";
        }

        public void Race() => ScreenFlow.Instance?.StartWeekly();
        public void Rules() { Haptics.Play(Haptics.Kind.Tick); Application.OpenURL(Weekly.RulesUrl); }

        void ShowClock()
        {
            if (clock != null && Weekly.Current != null) clock.text = $"ENDS IN {Weekly.Countdown(Weekly.SecondsLeft).ToUpperInvariant()}   ·   MONDAY 00:00 UTC";
        }

        void Show()
        {
            var b = Weekly.Current;
            int ti = Mathf.Max(0, Weekly.TrackIndex);
            if (trackName != null) trackName.text = b == null ? "" : Tracks.At(ti).name.ToUpperInvariant();
            if (art != null && ti < trackArt.Length && trackArt[ti] != null) art.sprite = trackArt[ti];
            ShowClock();
            if (prizes != null)
                prizes.text = b == null ? ""
                    : b.pot == null ? "Top 3 Seekers win SKR prizes"
                    : $"1ST  {Weekly.Prize(1)}   ·   2ND  {Weekly.Prize(2)}   ·   3RD  {Weekly.Prize(3)}"
                      + (b.seekers < b.minSeekers ? $"\n<size=80%><color=#B4A7DE>Paid when {b.minSeekers} Seekers have driven it ({b.seekers} so far)</color></size>" : "");
            if (mine != null)
                mine.text = b == null ? ""
                    : b.me != null ? $"You're #{b.me.rank} of {b.me.entrants}  ·  best lap {RaceHud.Clock(b.me.bestMs / 1000f)}" + (b.me.rank > 1 ? $"  ·  {b.me.gapMs / 1000f:0.00} s to #{b.me.rank - 1}" : "")
                    : KartApi.HasSession ? "You haven't driven it this week" : "Sign in to put your lap on the board";

            var rows = Visible(b);
            int n = Mathf.Min(rows.Count, rowObjects.Length);
            if (state != null) { state.gameObject.SetActive(n == 0); if (b != null) state.text = "No laps yet this week.\nSet the first one."; }
            string me = b?.me?.wallet;
            for (int i = 0; i < rowObjects.Length; i++)
            {
                if (rowObjects[i] != null) rowObjects[i].SetActive(i < n);
                if (i >= n) continue;
                var e = rows[i];
                if (i < ranks.Length) ranks[i].text = e.rank.ToString();
                if (i < names.Length) names[i].text = Fit(e.name) + (e.isSeeker ? "  <size=70%><color=#B4A7DE>SEEKER</color></size>" : "");
                if (i < times.Length) times[i].text = RaceHud.Clock(e.bestMs / 1000f);
                if (i < prizeLabels.Length) prizeLabels[i].text = e.prizePlace is { } p && Weekly.Prize(p) is { } amount ? amount : "";
                if (i < rowBacks.Length) rowBacks[i].color = me != null && e.wallet == me ? rowMine : rowIdle;
            }
        }

        /// Everyone under nine; else the top three and the places either side of you.
        List<Weekly.Row> Visible(Weekly.Board b)
        {
            if (b == null) return new List<Weekly.Row>();
            var all = b.entries;
            if (all.Count <= rowObjects.Length || b.me == null || b.me.rank <= rowObjects.Length) return all.Take(rowObjects.Length).ToList();
            var around = all.Where(e => e.rank >= b.me.rank - 2 && e.rank <= b.me.rank + 2).ToList();
            if (!around.Any(e => e.wallet == b.me.wallet)) around.Add(b.me);   // past the top 50 the Worker sends your row alone
            return all.Take(3).Concat(around.Where(e => e.rank > 3)).Take(rowObjects.Length).ToList();
        }

        static string Fit(string name) => string.IsNullOrEmpty(name) ? "—" : name.Length <= 22 ? name : name[..21] + "…";

        /// Editor-side preview.
        public void ShowInEditor() => Show();
    }
}
