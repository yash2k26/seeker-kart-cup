using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The achievements, all on one screen: a card each with what to do, how far along it is, and what it pays (a
    /// chest and stars, paid the moment it is done). Done ones are lit gold.
    public sealed class AchievementsPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text total;
        [SerializeField] Image[] icons = System.Array.Empty<Image>(), fills = System.Array.Empty<Image>(), edges = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] titles = System.Array.Empty<TMP_Text>(), lines = System.Array.Empty<TMP_Text>(), counts = System.Array.Empty<TMP_Text>();
        [SerializeField] Color done = new(1f, 0.694f, 0.149f), open = new(0.706f, 0.655f, 0.871f), doneEdge = new(1f, 0.694f, 0.149f, 0.9f), openEdge = new(0.29f, 0.145f, 0.69f, 0.6f);

        void OnEnable() => Show();
        public void ShowInEditor() => Show();

        void Show()
        {
            var all = Achievements.All;
            if (total != null) total.text = $"{Achievements.DoneCount} / {all.Length} DONE";
            for (int i = 0; i < all.Length; i++)
            {
                var d = all[i];
                bool ok = Achievements.IsDone(d);
                int p = Achievements.Progress(d);
                if (i < titles.Length && titles[i] != null) titles[i].text = d.title.ToUpperInvariant();
                if (i < lines.Length && lines[i] != null) lines[i].text = d.line;
                if (i < counts.Length && counts[i] != null)
                    counts[i].text = (ok ? "DONE" : $"{p} / {d.goal}") + $"   ·   + {Chests.Names[(int)d.reward].Replace(" CHEST", "")} CHEST, {d.stars} STAR{(d.stars > 1 ? "S" : "")}";
                if (i < fills.Length && fills[i] != null) { fills[i].fillAmount = d.goal > 0 ? p / (float)d.goal : 0f; fills[i].color = ok ? done : open; }
                if (i < icons.Length && icons[i] != null) icons[i].color = ok ? done : new Color(open.r, open.g, open.b, 0.45f);
                if (i < edges.Length && edges[i] != null) edges[i].color = ok ? doneEdge : openEdge;
            }
        }
    }
}
