using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Fuel (Beach Buggy's race tickets, 11 Oct 2026): the tank as a chip wherever a race starts (the hub, the series header,
// the event card), and the panel an empty tank opens.
public static partial class MenuSetup
{
    /// The can, "5/6" and "+1 IN 8:32" under it, and the "-1" that pops off it when a race takes a can. A tap opens the
    /// panel.
    static Button FuelChip(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 dims)
    {
        var b = Button("Fuel", parent, "6/6", new Color(Slate.r, Slate.g, Slate.b, 0.92f), Paper, 34, anchor, pos, dims, icon: "fuel");
        var count = b.transform.Find("FuelFace/FuelLabel").GetComponent<TMP_Text>();
        // the count up top, the timer under it, both beside the can
        count.rectTransform.anchorMin = new Vector2(0, 0.42f); count.rectTransform.anchorMax = Vector2.one;
        count.alignment = TextAlignmentOptions.Left;
        var timer = Label("Timer", count.transform.parent, strong, "FULL TANK", 20, Muted, TextAlignmentOptions.Left, new Vector2(0, 0), Vector2.zero, dims);
        timer.rectTransform.anchorMin = Vector2.zero; timer.rectTransform.anchorMax = new Vector2(1, 0.46f);
        timer.rectTransform.offsetMin = new Vector2(count.rectTransform.offsetMin.x, 6); timer.rectTransform.offsetMax = new Vector2(-12, 0);
        timer.enableAutoSizing = true; timer.fontSizeMin = 13; timer.fontSizeMax = 20;
        count.rectTransform.offsetMax = new Vector2(-12, -4);
        var pop = Label("Pop", b.transform, display, "-1", 54, new Color(1f, 0.42f, 0.36f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(160, 70));
        pop.gameObject.SetActive(false);
        var chip = b.gameObject.AddComponent<KartCup.FuelChip>();
        new SerializedObject(chip).Do(s =>
        {
            s.FindProperty("count").objectReferenceValue = count;
            s.FindProperty("timer").objectReferenceValue = timer;
            s.FindProperty("pop").objectReferenceValue = pop;
            s.FindProperty("normal").colorValue = Paper;
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, chip.Open);
        return b;
    }

    /// The panel, over everything in the menu: a dim that closes it, the card, FILL UP and NOT NOW.
    static void FuelOverlay(RectTransform safe)
    {
        var root = Rect("FuelPanel", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var panel = root.gameObject.AddComponent<FuelPanel>();
        var content = Rect("Content", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        var dim = content.gameObject.AddComponent<Image>(); dim.color = new Color(Deep.r, Deep.g, Deep.b, 0.9f);
        var close = content.gameObject.AddComponent<Button>(); close.transition = Selectable.Transition.None;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, panel.Close);
        var card = Rect("Card", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1100, 620));
        Design.AsCard(card, Panel).raycastTarget = true;
        var can = Rect("Can", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(150, 150));
        can.pivot = new Vector2(0.5f, 1);
        var canImg = can.gameObject.AddComponent<Image>(); canImg.sprite = IconSprite("fuel"); canImg.color = Accent; canImg.preserveAspect = true; canImg.raycastTarget = false;
        var title = Label("Title", card, display, "OUT OF FUEL", 76, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(1000, 96));
        var words = Label("Body", card, body, "", 34, Paper, TextAlignmentOptions.Top, new Vector2(0.5f, 1), new Vector2(0, -306), new Vector2(940, 140));
        words.textWrappingMode = TextWrappingModes.Normal; words.enableAutoSizing = true; words.fontSizeMin = 22; words.fontSizeMax = 34;
        var fill = Button("Fill", card, "FILL UP  ·  25 GEMS", Accent, Paper, 46, new Vector2(0.5f, 0), new Vector2(240, 48), new Vector2(560, 124), icon: "fuel");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(fill.onClick, panel.Fill);
        var later = Button("Later", card, "NOT NOW", Design.Ink700, Paper, 42, new Vector2(0.5f, 0), new Vector2(-300, 48), new Vector2(380, 124));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(later.onClick, panel.Close);
        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("content").objectReferenceValue = content.gameObject;
            s.FindProperty("title").objectReferenceValue = title;
            s.FindProperty("body").objectReferenceValue = words;
            s.FindProperty("fillLabel").objectReferenceValue = fill.GetComponentInChildren<TMP_Text>();
        });
        content.gameObject.SetActive(false);
    }
}
