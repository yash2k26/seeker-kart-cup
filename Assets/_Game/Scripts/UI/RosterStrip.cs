using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The whole grid at a glance: every driver's face in a row beside the kart. Tap a face to put that driver on the
    /// turntable; the one on it stands up out of the row with a cream ring. The swipe still steps through the same list.
    public sealed class RosterStrip : MonoBehaviour
    {
        [SerializeField] CharacterCarousel carousel;
        [SerializeField] RectTransform[] tiles = System.Array.Empty<RectTransform>();
        [SerializeField] Image[] backs = System.Array.Empty<Image>();
        [SerializeField] Image[] rings = System.Array.Empty<Image>();
        [SerializeField] Color idle, picked;
        [SerializeField] float lift = 16f;

        float[] homeY;
        Tween[] rise;      // only the lift is stopped on a change: StopAll would also freeze PressFeel's scale mid-release

        void OnEnable()
        {
            CharacterCarousel.Changed += Mark;
            Mark(carousel != null && Application.isPlaying ? carousel.Index : CharacterDatabase.SelectedIndex, false);
            // the screen opens on a driver who says hello once it has slid in (not at scene load: every panel is
            // enabled for a frame before ScreenFlow hides all but the title)
            if (carousel != null && Time.timeSinceLevelLoad > 0.3f) carousel.Greet(0.45f);
        }

        void OnDisable()
        {
            CharacterCarousel.Changed -= Mark;
            if (carousel != null) carousel.Greet(-1f);         // left before they spoke: they don't
        }

        public void Pick(int i) => carousel?.Pick(i);

        void Mark(int index) => Mark(index, true);

        /// Edit-mode previews: the strip as it opens on `index`.
        public void ShowInEditor(int index) => Mark(index, false);

        void Mark(int index, bool animate)
        {
            if (homeY == null || homeY.Length != tiles.Length)
            {
                homeY = new float[tiles.Length];
                rise = new Tween[tiles.Length];
                for (int i = 0; i < tiles.Length; i++) homeY[i] = tiles[i] != null ? tiles[i].anchoredPosition.y : 0f;
            }
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                bool on = i == index;
                if (i < backs.Length && backs[i] != null) backs[i].color = on ? picked : idle;
                if (i < rings.Length && rings[i] != null) rings[i].enabled = on;
                float y = homeY[i] + (on ? lift : 0f);
                if (rise[i].isAlive) rise[i].Stop();
                if (animate && Application.isPlaying) rise[i] = Tween.UIAnchoredPositionY(tiles[i], y, 0.2f, Ease.OutBack, useUnscaledTime: true);
                else tiles[i].anchoredPosition = new Vector2(tiles[i].anchoredPosition.x, y);
            }
        }
    }
}
