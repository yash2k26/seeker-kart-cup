using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// On the results card: the chest a podium earned (a sticker over the card's top right corner, OPEN hands it to the
/// overlay the menu keeps across scenes) and, after a Kartel Weekly time trial, where the lap stands this week.
public static partial class TrackSetup
{
    static void ResultsRewards(RectTransform card, TMP_FontAsset display, TMP_FontAsset body)
    {
        var sticker = Rect("Chest", card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(70, 70), new Vector2(330, 370));
        sticker.pivot = new Vector2(1, 1);
        var content = Rect("Content", sticker, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = Rect("Picture", content, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(250, 250));
        img.pivot = new Vector2(0.5f, 1);
        var chest = img.gameObject.AddComponent<Image>();
        chest.preserveAspect = true; chest.raycastTarget = false;
        var label = Text("Label", content, display, "", 30, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -246), new Vector2(330, 40));
        label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 30;
        var stars = Text("Stars", content, body, "", 26, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -284), new Vector2(330, 34));
        var open = TextButton("Open", content, "OPEN", Design.Ink600, Design.Paper, 34, display, new Vector2(0.5f, 0), new Vector2(0, -50), new Vector2(220, 84));
        var badge = sticker.gameObject.AddComponent<ChestBadge>();
        new SerializedObject(badge).Do(s =>
        {
            s.FindProperty("content").objectReferenceValue = content.gameObject;
            s.FindProperty("openButton").objectReferenceValue = open.gameObject;
            s.FindProperty("chest").objectReferenceValue = chest;
            s.FindProperty("label").objectReferenceValue = label;
            s.FindProperty("stars").objectReferenceValue = stars;
            var sprites = MenuSetup.ChestSprites(false);
            var arr = s.FindProperty("closedSprites"); arr.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(open.onClick, badge.Open);

        var weekly = Text("Weekly", card, body, "", 32, Design.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(1100, 50));
        weekly.enableAutoSizing = true; weekly.fontSizeMin = 22; weekly.fontSizeMax = 32;
        var result = card.gameObject.AddComponent<WeeklyResult>();
        new SerializedObject(result).Do(s => s.FindProperty("line").objectReferenceValue = weekly);
    }

}
