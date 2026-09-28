using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The track select: the chosen circuit's key art full-bleed behind a card, stepped by the arrows or a flick.
    /// Card -1 is the Grand Prix, every track in order, shown as all the pictures at once.
    public sealed class TrackCarousel : Carousel
    {
        [SerializeField] Sprite[] art = System.Array.Empty<Sprite>();     // Tracks.All order; the track builder may not have run yet
        [SerializeField] Image single;
        [SerializeField] GameObject grid;
        [SerializeField] Image[] cells = System.Array.Empty<Image>();
        [SerializeField] CanvasGroup fade;
        [SerializeField] TMP_Text kickerLabel, nameLabel, blurbLabel, bestLabel;
        [SerializeField] GameObject difficultyRow;
        [SerializeField] StatBar difficulty;

        const string Key = "kc.track";

        /// -1 = Grand Prix, otherwise an index into Tracks.All. ScreenFlow reads it when RACE is tapped.
        public static int Selection
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Key, -1), -1, Tracks.Count - 1);
            set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }

        int index;

        void Start() { index = Selection; Show(); }

        /// Editor-side preview so the menu scene is not empty when it is opened or screenshotted.
        public void ShowInEditor(int i) { index = i; Show(); }

        public override void Next() => Step(+1);
        public override void Previous() => Step(-1);

        void Step(int delta)
        {
            int n = Tracks.Count + 1;                              // the Grand Prix card, then every track
            index = ((index + 1 + delta) % n + n) % n - 1;
            Selection = index;
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
            Show();
            if (fade == null) return;
            Tween.StopAll(fade);
            Tween.Alpha(fade, 0f, 1f, 0.18f, Ease.OutCubic, useUnscaledTime: true);   // a hard cut between two full-screen pictures reads as a glitch
        }

        void Show()
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
                ? $"Points across all {Tracks.Count} tracks. The grid reverses each race, so leading means starting from the back."
                : def.blurb;
            if (difficultyRow != null) difficultyRow.SetActive(!gp);
            if (!gp) difficulty?.Set(def.difficulty - 3);          // StatBar lights 3 + value pips: 1..3 of a 3-pip row
            float best = gp ? 0f : Tracks.Best(def.id);
            if (bestLabel != null) bestLabel.text = best > 0f ? RaceHud.Clock(best) : "—";
        }

        Sprite Art(int i) => i >= 0 && i < art.Length ? art[i] : null;
    }
}
