using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Kartel Tour's screen: a tab per cup (a closed one says how many stars open it), its five events with the stars
    /// each has given, the boss row with the paint it pays, and who you are driving as (CHANGE goes to the select
    /// screen and comes back here). The next event to win is the one lit gold; PLAY starts it straight away.
    public sealed class TourPanel : MonoBehaviour
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Image[] tabBacks = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] tabLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text total, cupName, cupState, driver;
        [SerializeField] GameObject[] rows = System.Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] titles = System.Array.Empty<TMP_Text>(), modes = System.Array.Empty<TMP_Text>(), tracks = System.Array.Empty<TMP_Text>();
        [SerializeField] Image[] stars = System.Array.Empty<Image>();               // three a row
        [SerializeField] Image[] playBacks = System.Array.Empty<Image>();
        [SerializeField] Button[] plays = System.Array.Empty<Button>();
        [SerializeField] Image prizeSwatch;
        [SerializeField] TMP_Text prizeLabel;
        [SerializeField] Color tabOn, tabOff, textOn, textOff, starOn, starOff, playNext, playIdle, inkOnNext = new(0.09f, 0.02f, 0.25f), inkIdle = new(0.96f, 0.94f, 0.9f);

        int cup = -1;

        void OnEnable()
        {
            if (cup < 0) cup = Enumerable.Range(0, Tour.Cups.Length).Where(Tour.Open).DefaultIfEmpty(0).Max();   // the furthest open cup
            Show();
        }

        public void SetCup(int i) { cup = Mathf.Clamp(i, 0, Tour.Cups.Length - 1); Haptics.Play(Haptics.Kind.Tick); Show(); }

        public void Play(int row)
        {
            var c = Tour.Cups[cup];
            if (!Tour.Open(cup) || row >= c.events.Length) { Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Error(); return; }
            ScreenFlow.Instance?.StartTourEvent(c.events[row], c);
        }

        public void ChangeDriver() => ScreenFlow.Instance?.ChangeDriverFromTour();

        void Show()
        {
            if (cup < 0) cup = 0;
            var c = Tour.Cups[cup];
            bool open = Tour.Open(cup);
            for (int i = 0; i < tabBacks.Length; i++)
            {
                if (tabBacks[i] != null) tabBacks[i].color = i == cup ? tabOn : tabOff;
                if (i < tabLabels.Length && tabLabels[i] != null)
                {
                    tabLabels[i].color = i == cup ? textOn : textOff;
                    tabLabels[i].text = (Tour.Open(i) ? "" : "<size=80%>LOCKED · </size>") + Tour.Cups[i].kicker;
                }
            }
            if (total != null) total.text = $"{Tour.Total} / {Tour.MaxStars}";
            if (cupName != null) cupName.text = c.name;
            if (cupState != null)
                cupState.text = open ? $"{Tour.CupStars(cup)} / {c.events.Length * Tour.StarsPerEvent} stars  ·  1st 3  ·  2nd 2  ·  3rd 1"
                                     : $"Collect {c.gate - Tour.Total} more stars to open {c.name}";
            var who = roster != null ? roster.Selected : null;
            if (driver != null) driver.text = $"DRIVING AS  {(who != null ? who.displayName.ToUpperInvariant() : "—")}";

            int next = open ? System.Array.FindIndex(c.events, e => Tour.Stars(e.id) < Tour.StarsPerEvent) : -1;
            for (int i = 0; i < rows.Length; i++)
            {
                bool has = i < c.events.Length;
                if (rows[i] != null) rows[i].SetActive(has);
                if (!has) continue;
                var e = c.events[i];
                int got = Tour.Stars(e.id);
                if (i < titles.Length) titles[i].text = e.title.ToUpperInvariant();
                if (i < modes.Length) modes[i].text = RaceMode.Name(e.mode);
                if (i < tracks.Length) tracks[i].text = Tracks.At(e.track).name.ToUpperInvariant();
                for (int k = 0; k < 3; k++)
                    if (i * 3 + k < stars.Length && stars[i * 3 + k] != null)
                    {
                        bool lit = e.mode == RaceMode.Kind.Duel ? got > 0 : k < got;
                        stars[i * 3 + k].color = lit ? starOn : starOff;
                        stars[i * 3 + k].gameObject.SetActive(e.mode != RaceMode.Kind.Duel || k == 1);   // a duel is won or not: one big star
                    }
                if (i < playBacks.Length && playBacks[i] != null) playBacks[i].color = i == next ? playNext : playIdle;
                if (i < plays.Length && plays[i] != null)                     // the words and glyph on gold go dark (the accent's own ink)
                    foreach (var g in plays[i].GetComponentsInChildren<Graphic>()) if (g.gameObject != plays[i].gameObject) g.color = i == next ? inkOnNext : inkIdle;
                if (i < plays.Length && plays[i] != null) plays[i].interactable = open;
            }
            var paint = Paints.All.FirstOrDefault(p => p.sku == c.bossPaint);
            if (prizeSwatch != null && paint != null) prizeSwatch.color = paint.color;
            if (prizeLabel != null && paint != null)
                prizeLabel.text = Tour.BossBeaten(cup) ? $"{paint.name}  ·  WON" : $"PRIZE  {paint.name}  +  GOLDEN CHEST";
        }

        /// Editor-side preview.
        public void ShowInEditor(int i) { cup = i; Show(); }
    }
}
