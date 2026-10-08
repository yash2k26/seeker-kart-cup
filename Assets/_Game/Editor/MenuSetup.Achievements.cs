using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The achievements screen (8 Oct 2026, after Beach Buggy's 48): every one on one screen, four across, a card each.
public static partial class MenuSetup
{
    static GameObject AchievementsScreen(RectTransform parent)
    {
        var screen = Rect("Achievements", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.86f);
        Header(screen.transform, "ACHIEVEMENTS", nameof(ScreenFlow.ShowTitle));
        var total = Label("Total", screen.transform, strong, "0 / 24 DONE", 34, Accent, TextAlignmentOptions.Right,
                          new Vector2(1, 1), new Vector2(-72, -56), new Vector2(520, 56));

        var all = Achievements.All;
        const int cols = 4; const float w = 540f, h = 128f, gx = 20f, gy = 12f;
        var grid = Rect("Grid", screen.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -168), new Vector2(cols * w + (cols - 1) * gx, 6 * (h + gy)));
        grid.pivot = new Vector2(0.5f, 1);
        var icons = new Image[all.Length]; var fills = new Image[all.Length]; var edges = new Image[all.Length];
        var titles = new TMP_Text[all.Length]; var lines = new TMP_Text[all.Length]; var counts = new TMP_Text[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            var card = Rect("A" + i, grid, new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % cols) * (w + gx), -(i / cols) * (h + gy)), new Vector2(w, h));
            card.pivot = new Vector2(0, 1);
            Design.AsCard(card, Panel).raycastTarget = false;
            edges[i] = card.Find("Edge")?.GetComponent<Image>();
            var icon = Rect("Icon", card, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(52, 6), new Vector2(64, 64));
            icons[i] = icon.gameObject.AddComponent<Image>(); icons[i].sprite = IconSprite("trophy"); icons[i].preserveAspect = true; icons[i].raycastTarget = false;
            titles[i] = Label("Title", card, display, "", 28, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(104, -10), new Vector2(420, 36));
            lines[i] = Label("Line", card, body, "", 21, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(104, -44), new Vector2(420, 28));
            // a plain bar (a pill sprite filled part-way squashes into a streak): a dark track, the fill a scaled child
            var bar = Rect("Bar", card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(104, 38), new Vector2(408, 8));   // its own line, under the text
            bar.pivot = Vector2.zero;
            var track = bar.gameObject.AddComponent<Image>(); track.color = Deep; track.raycastTarget = false;
            var fillRt = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fills[i] = fillRt.gameObject.AddComponent<Image>(); fills[i].color = Accent; fills[i].raycastTarget = false;
            fills[i].type = Image.Type.Filled; fills[i].fillMethod = Image.FillMethod.Horizontal; fills[i].sprite = Design.Row;
            counts[i] = Label("Count", card, strong, "", 19, Paper, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(104, 8), new Vector2(420, 26));
            counts[i].rectTransform.pivot = Vector2.zero;
        }

        var panel = screen.AddComponent<AchievementsPanel>();
        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("total").objectReferenceValue = total;
            Fill(s.FindProperty("icons"), icons.Cast<Object>().ToArray());
            Fill(s.FindProperty("fills"), fills.Cast<Object>().ToArray());
            Fill(s.FindProperty("edges"), edges.Where(e => e != null).Cast<Object>().ToArray());
            Fill(s.FindProperty("titles"), titles.Cast<Object>().ToArray());
            Fill(s.FindProperty("lines"), lines.Cast<Object>().ToArray());
            Fill(s.FindProperty("counts"), counts.Cast<Object>().ToArray());
        });
        return screen;
    }
}
