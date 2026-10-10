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
        // Beach Buggy's event card, before the race: the mode and its one rule, the stars still to earn, the goal, what
        // it pays, your best on that circuit, and a big RACE
        [SerializeField] GameObject card;
        [SerializeField] TMP_Text cardMode, cardTitle, cardTrack, cardRule, cardGoal, cardPrize, cardBest;
        [SerializeField] Image[] cardStars = System.Array.Empty<Image>();
        // Beach Buggy's Choose Series (10 Oct 2026): the cards first (pick), a tap opens that series' events (detail)
        [SerializeField] GameObject pick, detail;
        [SerializeField] Image[] seriesPics = System.Array.Empty<Image>(), seriesBoss = System.Array.Empty<Image>();
        [SerializeField] GameObject[] seriesLocks = System.Array.Empty<GameObject>(), seriesStars = System.Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] seriesFoots = System.Array.Empty<TMP_Text>();
        [SerializeField] Sprite[] trackArt = System.Array.Empty<Sprite>();
        [SerializeField] string[] portraitIds = System.Array.Empty<string>();
        [SerializeField] Sprite[] portraits = System.Array.Empty<Sprite>();
        bool picking = true;
        /// Set by the race results' CONTINUE: the menu opens on this series' events.
        public static int Reopen = -1;
        // inside a series: a tile an event, a bar along the bottom
        [SerializeField] GameObject[] tiles = System.Array.Empty<GameObject>(), tileLocks = System.Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] tileNums = System.Array.Empty<TMP_Text>(), tileGates = System.Array.Empty<TMP_Text>();
        [SerializeField] Image[] tileStars = System.Array.Empty<Image>(), tileBoss = System.Array.Empty<Image>();
        [SerializeField] TMP_Text seriesTitle, seriesTotal;
        // the header's own title (the series' name inside it), each tile's mode and circuit, and the bar's one line on
        // what to do next (or why a tapped event is shut)
        [SerializeField] TMP_Text headerTitle, hint;
        [SerializeField] TMP_Text[] tileModes = System.Array.Empty<TMP_Text>(), tileWheres = System.Array.Empty<TMP_Text>();
        string told;   // a closed event's reason, shown until the next tap
        Color hintColour;
        [SerializeField] Image cardPic;
        int pendingRow = -1;

        int cup = -1;

        void OnEnable()
        {
            if (cup < 0) cup = Enumerable.Range(0, Tour.Cups.Length).Where(Tour.Open).DefaultIfEmpty(0).Max();   // the furthest open cup
            picking = true;
            if (Reopen >= 0) { cup = Mathf.Clamp(Reopen, 0, Tour.Cups.Length - 1); picking = false; }   // ScreenFlow clears it once the screen is up
            told = null;
            Show();
        }

        /// A series card: its events. A locked one only says what opens it.
        public void Pick(int i)
        {
            cup = Mathf.Clamp(i, 0, Tour.Cups.Length - 1);
            if (!Tour.Open(cup)) { Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Error(); Show(); return; }
            picking = false; Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click(); Show();
        }
        /// From the events back to the cards; false when the cards are already up (the flow goes home then).
        public bool Back()
        {
            if (card != null && card.activeSelf) { CloseCard(); return true; }   // the card first, then the events, then the cards
            if (picking) return false;
            picking = true; told = null; Show();
            return true;
        }

        public void SetCup(int i) { cup = Mathf.Clamp(i, 0, Tour.Cups.Length - 1); Haptics.Play(Haptics.Kind.Tick); Show(); }

        public void Play(int row)
        {
            var c = Tour.Cups[cup];
            if (row >= c.events.Length || !Tour.EventOpen(cup, row))
            {
                // a shut event says why, in the bar (Beach Buggy's "earn N more stars"), and shakes no
                told = row < c.events.Length ? $"EVENT {row + 1}  ·  {Tour.Needs(cup, row)}" : null;
                Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Error();
                if (row < tiles.Length && tiles[row] != null) PrimeTween.Tween.ShakeLocalPosition(tiles[row].transform, new Vector3(14f, 0f, 0f), 0.3f, 18f, useUnscaledTime: true);
                Show();
                return;
            }
            told = null;
            if (card == null) { ScreenFlow.Instance?.StartTourEvent(c.events[row], c); return; }
            pendingRow = row;
            ShowCard(c, row);
            UiMotion.Show(card, card.transform.childCount > 0 ? card.transform.GetChild(0) : card.transform);
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
        }

        /// The card's RACE.
        public void Race()
        {
            var c = Tour.Cups[cup];
            if (pendingRow < 0 || pendingRow >= c.events.Length) return;
            ScreenFlow.Instance?.StartTourEvent(c.events[pendingRow], c);
        }

        /// The card's back: the list again.
        public void CloseCard() { if (card != null && card.activeSelf) UiMotion.Hide(card); pendingRow = -1; }

        void OnDisable() { if (card != null) card.SetActive(false); pendingRow = -1; }

        void ShowCard(Tour.CupDef c, int row)
        {
            // Beach Buggy's event card: the circuit at the left, the mode, stars, goal and reward in the middle, the best
            // time and the rule at the right, PLAY at the bottom right
            var e = c.events[row];
            bool boss = row == c.events.Length - 1;
            var rival = e.mode == RaceMode.Kind.Duel && roster != null && e.rival >= 0 ? roster.At(e.rival) : null;
            int got = Tour.Stars(e.id);
            var track = Tracks.At(e.track);
            if (cardMode != null) cardMode.text = boss ? "BOSS DUEL" : e.reverse && e.mode == RaceMode.Kind.Race && e.items == RaceMode.Items.Normal ? "REVERSE RACE" : Tour.ModeName(e);
            if (cardTitle != null) cardTitle.text = e.title.ToUpperInvariant();
            if (cardTrack != null) cardTrack.text = Tour.Where(e);
            if (cardPic != null) { cardPic.sprite = e.track < trackArt.Length ? trackArt[e.track] : null; cardPic.enabled = cardPic.sprite != null; }
            if (cardRule != null) cardRule.text = Tour.Rule(e, rival?.displayName);
            if (cardGoal != null) cardGoal.text = Tour.Goal(e);
            var paint = boss ? Paints.All.FirstOrDefault(p => p.sku == c.bossPaint) : null;
            if (cardPrize != null) cardPrize.text = boss ? (Tour.BossBeaten(cup) ? "WON" : $"{paint?.name ?? "PAINT"}") : got >= Tour.StarsPerEvent ? "ALL STARS" : "5 GEMS";
            float best = Tracks.Best(Tour.BestKey(e));
            if (cardBest != null) cardBest.text = best > 0f ? RaceHud.Clock(best) : "--:--";
            for (int k = 0; k < cardStars.Length; k++)
                if (cardStars[k] != null) { cardStars[k].color = k < got ? starOn : starOff; cardStars[k].gameObject.SetActive(true); }
        }

        public void ChangeDriver() => ScreenFlow.Instance?.ChangeDriverFromTour();

        void Show()
        {
            if (cup < 0) cup = 0;
            var c = Tour.Cups[cup];
            bool open = Tour.Open(cup);
            if (pick != null) pick.SetActive(picking);
            if (detail != null) detail.SetActive(!picking);
            for (int i = 0; i < seriesPics.Length && i < Tour.Cups.Length; i++)
            {
                var k = Tour.Cups[i]; bool isOpen = Tour.Open(i);
                int art = k.events.Length > 0 ? k.events[0].track : 0;
                if (seriesPics[i] != null) { seriesPics[i].sprite = art < trackArt.Length ? trackArt[art] : null; seriesPics[i].color = isOpen ? Color.white : new Color(0.5f, 0.5f, 0.6f); }
                if (i < seriesLocks.Length && seriesLocks[i] != null) seriesLocks[i].SetActive(!isOpen);
                if (i < seriesBoss.Length && seriesBoss[i] != null)
                {
                    var bossDef = roster != null && k.boss >= 0 && k.boss < roster.characters.Length ? roster.characters[k.boss] : null;
                    int p = bossDef != null ? System.Array.IndexOf(portraitIds, bossDef.driverId) : -1;
                    seriesBoss[i].sprite = p >= 0 && p < portraits.Length ? portraits[p] : null;
                    seriesBoss[i].enabled = seriesBoss[i].sprite != null;
                }
                if (i < seriesStars.Length && seriesStars[i] != null) seriesStars[i].SetActive(isOpen);
                if (i < seriesFoots.Length && seriesFoots[i] != null)
                {
                    var f = seriesFoots[i];
                    f.text = isOpen ? $"{Tour.CupStars(i)} / {k.events.Length * Tour.StarsPerEvent}" : $"LOCKED  ·  {Tour.Opener(i)}";
                    // beside the star while open; the whole foot, centred, while shut (the hidden star's room pushed it into
                    // the card's edge, 10 Oct 2026)
                    var rt = f.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(isOpen ? 0f : 0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(isOpen ? 92f : 0f, 0f);
                    rt.sizeDelta = new Vector2(isOpen ? 270f : 340f, 60f);
                    f.alignment = isOpen ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
                }
            }
            for (int i = 0; i < tabBacks.Length; i++)
            {
                if (tabBacks[i] != null) tabBacks[i].color = i == cup ? tabOn : tabOff;
                if (i < tabLabels.Length && tabLabels[i] != null)
                {
                    tabLabels[i].color = i == cup ? textOn : textOff;
                    var k = Tour.Cups[i];
                    tabLabels[i].text = Tour.Open(i)
                        ? $"{k.name}\n<size=62%><color=#FFB126>{Tour.CupStars(i)} / {k.events.Length * Tour.StarsPerEvent} STARS</color></size>"
                        : $"{k.name}\n<size=62%>LOCKED  ·  {Tour.Opener(i)} TO OPEN</size>";
                }
            }
            // inside the series: the tiles and the bar
            for (int i = 0; i < tiles.Length; i++)
            {
                bool has = i < c.events.Length;
                if (tiles[i] != null) tiles[i].SetActive(has);
                if (!has) continue;
                var e = c.events[i]; int got = Tour.Stars(e.id); bool evOpen = Tour.EventOpen(cup, i), boss = i == c.events.Length - 1;
                if (i < tileNums.Length && tileNums[i] != null) { tileNums[i].text = (i + 1).ToString(); tileNums[i].gameObject.SetActive(!boss && evOpen); }
                if (i < tileBoss.Length && tileBoss[i] != null)
                {
                    var bossDef = roster != null && c.boss >= 0 && c.boss < roster.characters.Length ? roster.characters[c.boss] : null;
                    int p = bossDef != null ? System.Array.IndexOf(portraitIds, bossDef.driverId) : -1;
                    tileBoss[i].sprite = p >= 0 && p < portraits.Length ? portraits[p] : null;
                    tileBoss[i].enabled = boss && tileBoss[i].sprite != null;
                    tileBoss[i].color = evOpen ? Color.white : new Color(0.35f, 0.32f, 0.5f);
                }
                if (i < tileLocks.Length && tileLocks[i] != null) tileLocks[i].SetActive(!evOpen);
                if (i < tileGates.Length && tileGates[i] != null) tileGates[i].text = Tour.EventGate(i).ToString();
                if (i < tileModes.Length && tileModes[i] != null) tileModes[i].text = boss ? $"BOSS  ·  {e.title.Replace("BOSS: ", "")}" : Tour.ModeName(e);
                if (i < tileWheres.Length && tileWheres[i] != null) tileWheres[i].text = Tour.Where(e);
                for (int k = 0; k < 3; k++)
                    if (i * 3 + k < tileStars.Length && tileStars[i * 3 + k] != null) tileStars[i * 3 + k].color = k < got ? starOn : starOff;
            }
            if (seriesTitle != null) seriesTitle.text = c.name;
            if (headerTitle != null) headerTitle.text = picking ? "CHOOSE SERIES" : c.name;
            if (hint != null)
            {
                if (hintColour.a == 0f) hintColour = hint.color;   // the builder's muted ink (textOff belonged to the old tabs and is unset)
                hint.text = told ?? Next(cup); hint.color = told != null ? starOn : hintColour;
            }
            if (seriesTotal != null) seriesTotal.text = $"{Tour.CupStars(cup)} / {c.events.Length * Tour.StarsPerEvent}";
            if (total != null) total.text = $"{Tour.Total} / {Tour.MaxStars}";
            if (cupName != null) cupName.text = c.name;
            if (cupState != null)
                cupState.text = open ? $"{Tour.CupStars(cup)} / {c.events.Length * Tour.StarsPerEvent} stars  ·  1st 3  ·  2nd 2  ·  3rd 1"
                                     : $"{Tour.Opener(cup)} TO OPEN {c.name}";
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
                if (i < modes.Length) modes[i].text = Tour.ModeName(e);
                if (i < tracks.Length) tracks[i].text = Tour.Where(e);
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

        /// The bar's line inside a series: what opens next, or that it is won.
        static string Next(int cup)
        {
            var c = Tour.Cups[cup];
            if (Tour.BossBeaten(cup))
                return cup + 1 < Tour.Cups.Length ? $"SERIES WON  ·  {Tour.Cups[cup + 1].name} IS OPEN" : "SERIES WON  ·  THE TOUR IS YOURS";
            for (int i = 0; i < c.events.Length; i++)
                if (!Tour.EventOpen(cup, i)) return $"EVENT {i + 1}  ·  {Tour.Needs(cup, i)}";
            return $"{c.events[^1].title.Replace("BOSS: ", "BEAT ")} TO WIN THE SERIES";
        }

        /// Editor-side preview.
        public void ShowInEditor(int i) { cup = i; picking = false; Show(); if (card != null) card.SetActive(false); }
        public void PickInEditor() { picking = true; Show(); if (card != null) card.SetActive(false); }
        /// Previews: the event card for a row, open.
        public void CardInEditor(int i, int row) { cup = i; Show(); pendingRow = row; ShowCard(Tour.Cups[cup], row); if (card != null) card.SetActive(true); }
    }
}
