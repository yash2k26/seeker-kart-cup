using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The achievements screen. 8 Oct 2026: every one on one screen, four across, a card each, at 19-28 px on a phone.
// 12 Oct 2026 (the user: "there must be a good amount of lists, so it should be good UX wise"): a scrolling list in two
// columns of big cards, TO DO and DONE tabs, what can be claimed first with a CLAIM on it, and CLAIM ALL. The panel
// (AchievementsPanel) clones one row made here for every achievement and lays them out in order at runtime.
public static partial class MenuSetup
{
    static GameObject AchievementsScreen(RectTransform parent)
    {
        var screen = Rect("Achievements", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.9f);
        Header(screen.transform, "ACHIEVEMENTS", nameof(ScreenFlow.ShowTitle));

        // how far through them all: the count and a bar under it, top right
        var total = Label("Total", screen.transform, strong, "0 / 24 DONE", 36, Accent, TextAlignmentOptions.Right,
                          new Vector2(1, 1), new Vector2(-72, -40), new Vector2(520, 48));
        var totalBar = Rect("TotalBar", screen.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-72, -98), new Vector2(420, 18));
        totalBar.pivot = new Vector2(1, 1);
        Design.AsPill(totalBar, Panel).raycastTarget = false;
        var totalFillRt = Rect("Fill", totalBar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        totalFillRt.offsetMin = new Vector2(3, 3); totalFillRt.offsetMax = new Vector2(-3, -3);
        var totalFill = Design.AsPill(totalFillRt, Accent);
        totalFill.type = Image.Type.Filled; totalFill.fillMethod = Image.FillMethod.Horizontal; totalFill.raycastTarget = false;

        var (tabBacks, tabLabels, tabButtons) = Segmented("Tabs", screen.transform, new[] { "TO DO", "DONE" }, new Vector2(0, 1), new Vector2(72, -150), new Vector2(640, 88), 34);
        var claimAll = Button("ClaimAll", screen.transform, "CLAIM ALL", Accent, Paper, 34, new Vector2(1, 1), new Vector2(-72, -150), new Vector2(440, 88));
        var claimAllLabel = claimAll.GetComponentInChildren<TMP_Text>(true);

        // the list: a viewport under the tabs to the foot of the screen, two columns of rows in its content
        const float rowW = 1000f, rowH = 172f, gapX = 32f;
        var view = Rect("List", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        view.offsetMin = new Vector2(72, 24); view.offsetMax = new Vector2(-72, -262);
        view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);     // something to drag on between the cards
        view.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", view, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(rowW * 2 + gapX, 800));
        content.pivot = new Vector2(0.5f, 1);
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content; scroll.viewport = view; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic; scroll.elasticity = 0.12f; scroll.inertia = true; scroll.decelerationRate = 0.12f; scroll.scrollSensitivity = 40f;
        var empty = Label("Empty", view, body, "", 34, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 60));

        // one row, cloned for every achievement: trophy, title and what to do, a bar and its count, the chest and stars
        // it pays, and CLAIM (ready) or a tick (claimed) at the right
        var row = Rect("Row", content, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(rowW, rowH));
        row.pivot = new Vector2(0, 1);
        Design.AsCard(row, Panel).raycastTarget = false;
        var icon = Rect("Icon", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(92, 92));
        var iconImg = icon.gameObject.AddComponent<Image>(); iconImg.sprite = IconSprite("trophy"); iconImg.preserveAspect = true; iconImg.raycastTarget = false;
        var title = Label("Title", row, display, "", 40, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(132, -20), new Vector2(430, 50));
        title.enableAutoSizing = true; title.fontSizeMin = 28; title.fontSizeMax = 40;
        var line = Label("Line", row, body, "", 28, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(132, -74), new Vector2(430, 38));
        line.enableAutoSizing = true; line.fontSizeMin = 20; line.fontSizeMax = 28;
        var bar = Rect("Bar", row, new Vector2(0, 0), new Vector2(0, 0), new Vector2(132, 28), new Vector2(300, 16));
        bar.pivot = Vector2.zero;
        var trackImg = Design.AsPill(bar, Deep); trackImg.raycastTarget = false;
        var fillRt = Rect("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var fillImg = Design.AsPill(fillRt, Accent);
        fillImg.type = Image.Type.Filled; fillImg.fillMethod = Image.FillMethod.Horizontal; fillImg.raycastTarget = false;
        var count = Label("Count", row, strong, "", 26, Paper, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(446, 18), new Vector2(124, 36));
        count.enableAutoSizing = true; count.fontSizeMin = 18; count.fontSizeMax = 26;
        var reward = Rect("Reward", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-224, 0), new Vector2(196, 120));
        reward.pivot = new Vector2(1, 0.5f);
        var chestRt = Rect("Chest", reward, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, 4), new Vector2(96, 96));
        var chestImg = chestRt.gameObject.AddComponent<Image>(); chestImg.preserveAspect = true; chestImg.raycastTarget = false;
        var starRt = Rect("Star", reward, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-62, 0), new Vector2(36, 36));
        var starImg = starRt.gameObject.AddComponent<Image>(); starImg.sprite = IconSprite("star"); starImg.color = Accent; starImg.preserveAspect = true; starImg.raycastTarget = false;
        var stars = Label("Stars", reward, display, "+2", 34, Paper, TextAlignmentOptions.Left, new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(50, 44));
        stars.rectTransform.pivot = new Vector2(1, 0.5f);
        var claim = Button("Claim", row, "CLAIM", Accent, Paper, 34, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(190, 92));
        var tick = Rect("Done", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(64, 64));
        var tickImg = tick.gameObject.AddComponent<Image>(); tickImg.sprite = IconSprite("icon_checkmark"); tickImg.color = Accent; tickImg.preserveAspect = true; tickImg.raycastTarget = false;

        var panel = screen.AddComponent<AchievementsPanel>();
        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("total").objectReferenceValue = total;
            s.FindProperty("totalFill").objectReferenceValue = totalFill;
            s.FindProperty("scroll").objectReferenceValue = scroll;
            s.FindProperty("content").objectReferenceValue = content;
            s.FindProperty("rowTemplate").objectReferenceValue = row.gameObject;
            Fill(s.FindProperty("tabBacks"), tabBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("tabLabels"), tabLabels.Cast<Object>().ToArray());
            s.FindProperty("claimAll").objectReferenceValue = claimAll.gameObject;
            s.FindProperty("claimAllLabel").objectReferenceValue = claimAllLabel;
            s.FindProperty("empty").objectReferenceValue = empty;
            Fill(s.FindProperty("chests"), ChestSprites(false).Cast<Object>().ToArray());
            s.FindProperty("rowW").floatValue = rowW; s.FindProperty("rowH").floatValue = rowH; s.FindProperty("gapX").floatValue = gapX;
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tabButtons[0].onClick, panel.ToDo);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tabButtons[1].onClick, panel.Done);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(claimAll.onClick, panel.ClaimAll);
        row.gameObject.SetActive(false);
        return screen;
    }

    /// A red count in the corner of an Awards button: how many achievements wait to be claimed (AwardsBadge).
    static void AwardsBadge(Transform button)
    {
        var pill = Rect("ClaimCount", button, new Vector2(1, 1), new Vector2(1, 1), new Vector2(14, 14), new Vector2(54, 54));
        pill.pivot = new Vector2(1, 1);
        Design.AsPill(pill, Design.Tiers[0]).raycastTarget = false;
        var n = Label("N", pill, display, "1", 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(54, 54));
        var badge = button.gameObject.AddComponent<KartCup.AwardsBadge>();
        new SerializedObject(badge).Do(s => { s.FindProperty("pill").objectReferenceValue = pill.gameObject; s.FindProperty("count").objectReferenceValue = n; });
    }
}
