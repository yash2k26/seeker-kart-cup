using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The track select. Two ways to race, as two tabs: the Grand Prix (every track in order, points across all) or a
    /// single race on one track. The four tracks are cards along the bottom — tap one to race it; in a Grand Prix they
    /// are all lit and numbered in running order. The chosen track's key art fills the screen behind, and the card on
    /// the left says what it is: its map, its laps and length, how hard, your best. Arrows and a flick still step.
    public sealed class TrackCarousel : Carousel
    {
        [SerializeField] Sprite[] art = System.Array.Empty<Sprite>();     // Tracks.All order; the track builder may not have run yet
        [SerializeField] Sprite[] maps = System.Array.Empty<Sprite>();
        [SerializeField] float[] lengths = System.Array.Empty<float>();   // metres a lap (worker/src/tracks.json, baked by MenuSetup)
        [SerializeField] int laps = 2;
        [SerializeField] Image single;
        [SerializeField] GameObject grid;
        [SerializeField] Image[] cells = System.Array.Empty<Image>();
        [SerializeField] CanvasGroup fade;
        [SerializeField] TMP_Text kickerLabel, nameLabel, blurbLabel, bestLabel, factsLabel;
        [SerializeField] GameObject difficultyRow;
        [SerializeField] StatBar difficulty;
        [SerializeField] Image mapImage;
        [SerializeField] GameObject[] gpOnly = System.Array.Empty<GameObject>();     // the GP's running order
        [SerializeField] GameObject[] singleOnly = System.Array.Empty<GameObject>(); // a track's own best (a cup has none)
        [SerializeField] RectTransform[] cards = System.Array.Empty<RectTransform>();
        [SerializeField] Image[] cardArt = System.Array.Empty<Image>();
        [SerializeField] Image[] cardRings = System.Array.Empty<Image>();
        [SerializeField] GameObject[] cardBadges = System.Array.Empty<GameObject>();
        [SerializeField] Image tabGp, tabSingle;
        [SerializeField] TMP_Text tabGpLabel, tabSingleLabel;
        [SerializeField] Color tabOn, tabOff, textOn, textOff;
        [SerializeField] float lift = 18f;

        const string Key = "kc.track", LastKey = "kc.track.last";

        /// -1 = Grand Prix, otherwise an index into Tracks.All. ScreenFlow reads it when RACE is tapped.
        public static int Selection
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Key, -1), -1, Tracks.Count - 1);
            set { PlayerPrefs.SetInt(Key, value); if (value >= 0) PlayerPrefs.SetInt(LastKey, value); PlayerPrefs.Save(); }
        }

        int index;
        float[] homeY;
        Tween[] rise;

        void Start() { index = Selection; Show(false); }

        /// Editor-side preview so the menu scene is not empty when it is opened or screenshotted.
        public void ShowInEditor(int i) { index = i; Show(false); }

        public override void Next() => Step(+1);
        public override void Previous() => Step(-1);

        /// A track card: race that track on its own.
        public void Pick(int track) { if (track != index) Go(track); }
        /// The tabs: the Grand Prix, or the last single track raced (the first one if none).
        public void GrandPrix() { if (index >= 0) Go(-1); }
        public void SingleRace() { if (index < 0) Go(Mathf.Clamp(PlayerPrefs.GetInt(LastKey, 0), 0, Tracks.Count - 1)); }

        void Step(int delta)
        {
            int n = Tracks.Count + 1;                              // the Grand Prix, then every track
            Go(((index + 1 + delta) % n + n) % n - 1);
        }

        void Go(int to)
        {
            index = to;
            Selection = index;
            Show(true);
            if (fade == null || !Application.isPlaying) return;
            Tween.StopAll(fade);
            Tween.Alpha(fade, 0.25f, 1f, 0.22f, Ease.OutCubic, useUnscaledTime: true);   // a hard cut between two full-screen pictures reads as a glitch
        }

        void Show(bool animate)
        {
            bool gp = index < 0;
            var def = gp ? null : Tracks.At(index);

            // An Image with no sprite is a white quad, so a missing picture hides rather than blanks the screen.
            var picture = gp ? null : Art(index);
            if (single != null) { single.sprite = picture; single.gameObject.SetActive(picture != null); }
            if (grid != null) grid.SetActive(gp);
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != null) { cells[i].sprite = Art(i); cells[i].enabled = cells[i].sprite != null; }

            if (kickerLabel != null) kickerLabel.text = gp ? $"{Tracks.Count} RACES  ·  EVERY TRACK" : def.kicker;
            if (nameLabel != null) nameLabel.text = gp ? "GRAND PRIX" : def.name.ToUpperInvariant();
            if (blurbLabel != null) blurbLabel.text = gp
                ? "Points across every track, in the order below. The grid reverses each race, so leading means starting from the back."
                : def.blurb;
            if (factsLabel != null) factsLabel.text = gp ? $"{Tracks.Count * laps} LAPS  ·  {TotalKm():0.0} KM" : $"{laps} LAPS  ·  {Km(index):0.0} KM";
            if (difficultyRow != null) difficultyRow.SetActive(!gp);
            if (!gp) difficulty?.Set(def.difficulty - 3);          // StatBar lights 3 + value pips: 1..3 of a 3-pip row
            float best = gp ? 0f : Tracks.Best(def.id);
            if (bestLabel != null) bestLabel.text = best > 0f ? RaceHud.Clock(best) : "No time yet";
            if (mapImage != null) { var m = gp ? null : Map(index); mapImage.sprite = m; mapImage.enabled = m != null; }
            foreach (var g in gpOnly) if (g != null) g.SetActive(gp);
            foreach (var g in singleOnly) if (g != null) g.SetActive(!gp);

            if (tabGp != null) tabGp.color = gp ? tabOn : tabOff;
            if (tabSingle != null) tabSingle.color = gp ? tabOff : tabOn;
            if (tabGpLabel != null) tabGpLabel.color = gp ? textOn : textOff;
            if (tabSingleLabel != null) tabSingleLabel.color = gp ? textOff : textOn;

            if (homeY == null || homeY.Length != cards.Length)
            {
                homeY = new float[cards.Length]; rise = new Tween[cards.Length];
                for (int i = 0; i < cards.Length; i++) homeY[i] = cards[i] != null ? cards[i].anchoredPosition.y : 0f;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                bool picked = !gp && i == index;
                bool lit = gp || picked;
                if (i < cardArt.Length && cardArt[i] != null) { cardArt[i].sprite = Art(i); cardArt[i].color = lit ? Color.white : new Color(0.5f, 0.5f, 0.56f); }
                if (i < cardRings.Length && cardRings[i] != null) cardRings[i].enabled = picked;
                if (i < cardBadges.Length && cardBadges[i] != null) cardBadges[i].SetActive(gp);
                float y = homeY[i] + (picked ? lift : 0f);
                if (rise[i].isAlive) rise[i].Stop();
                if (animate && Application.isPlaying) rise[i] = Tween.UIAnchoredPositionY(cards[i], y, 0.22f, Ease.OutBack, useUnscaledTime: true);
                else cards[i].anchoredPosition = new Vector2(cards[i].anchoredPosition.x, y);
            }
        }

        float Km(int i) => i >= 0 && i < lengths.Length ? lengths[i] * laps / 1000f : 0f;
        float TotalKm() { float s = 0f; for (int i = 0; i < Tracks.Count; i++) s += Km(i); return s; }
        Sprite Art(int i) => i >= 0 && i < art.Length ? art[i] : null;
        Sprite Map(int i) => i >= 0 && i < maps.Length ? maps[i] : null;
    }
}
