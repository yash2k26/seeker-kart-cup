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
        // The sticker overhangs the card's top only: its OPEN pill and both lines of words stay inside the card, above the
        // series card's time box. (Its words ran past the card's edge and OPEN hung off it and over the times, 10 Oct 2026.)
        var sticker = Rect("Chest", card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, 70), new Vector2(320, 380));
        sticker.pivot = new Vector2(1, 1);
        var content = Rect("Content", sticker, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = Rect("Picture", content, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(200, 200));
        img.pivot = new Vector2(0.5f, 1);
        var chest = img.gameObject.AddComponent<Image>();
        chest.preserveAspect = true; chest.raycastTarget = false;
        var label = Text("Label", content, display, "", 30, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -204), new Vector2(300, 40));
        label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 30;
        var stars = Text("Stars", content, body, "", 24, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -246), new Vector2(300, 32));
        stars.enableAutoSizing = true; stars.fontSizeMin = 15; stars.fontSizeMax = 24;
        var open = TextButton("Open", content, "OPEN", Design.Ink600, Design.Paper, 32, display, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(200, 72));
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

        // a Tour event's stars, a cup it opened, a boss's prize; or the Daily Challenge met (or still to meet)
        var tourLine = Text("Tour", card, body, "", 32, Design.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 258), new Vector2(1100, 50));   // clear of the boxes above it
        tourLine.enableAutoSizing = true; tourLine.fontSizeMin = 22; tourLine.fontSizeMax = 32;
        var tour = card.gameObject.AddComponent<TourResult>();
        new SerializedObject(tour).Do(s => s.FindProperty("line").objectReferenceValue = tourLine);
        EventResultLayout(card, display, body);
    }

    /// A series race's card (Beach Buggy's): the circuit, the stars, what you got against the goal, the reward, your time
    /// against the best. EventResult switches it on in place of the standings table for a Tour event only.
    static void EventResultLayout(RectTransform card, TMP_FontAsset display, TMP_FontAsset body)
    {
        var layout = Rect("EventLayout", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform Box(string name, Vector2 at, Vector2 size)
        {
            var r = Rect(name, layout, new Vector2(0, 1), new Vector2(0, 1), at, size); r.pivot = new Vector2(0, 1);
            Design.AsRow(r, Design.Ink700).raycastTarget = false; return r;
        }
        var left = Box("Track", new Vector2(40, -176), new Vector2(330, 330));
        var mask = Rect("Picture", left, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(300, 180));
        mask.pivot = new Vector2(0.5f, 1); mask.gameObject.AddComponent<RectMask2D>();
        var art = Rect("Art", mask, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 180));
        var pic = art.gameObject.AddComponent<Image>(); pic.raycastTarget = false;
        var trackName = Text("TrackName", left, display, "", 36, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(310, 50));
        trackName.enableAutoSizing = true; trackName.fontSizeMin = 22; trackName.fontSizeMax = 36;
        var eventTitle = Text("EventTitle", left, body, "", 26, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(310, 40));
        var mid = Box("Middle", new Vector2(390, -176), new Vector2(450, 330));
        var stars = new Image[3];
        for (int k = 0; k < 3; k++)
        {
            var st = Rect("Star" + k, mid, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((k - 1) * 130, k == 1 ? -80 : -94), new Vector2(k == 1 ? 130f : 112f, k == 1 ? 130f : 112f));
            stars[k] = st.gameObject.AddComponent<Image>(); stars[k].sprite = Design.Icon("star"); stars[k].raycastTarget = false;
        }
        Text("GotCap", mid, body, "YOU GOT", 26, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.25f, 0), new Vector2(0, 116), new Vector2(220, 36));
        Text("GoalCap", mid, body, "GOAL", 26, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.75f, 0), new Vector2(0, 116), new Vector2(220, 36));
        var got = Text("Got", mid, display, "", 58, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.25f, 0), new Vector2(0, 52), new Vector2(200, 70));
        var goal = Text("Goal", mid, display, "", 58, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.75f, 0), new Vector2(0, 52), new Vector2(200, 70));
        foreach (var t in new[] { got, goal }) { t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = 58; }   // each to its own half: "12 SMASHED" fits
        var reward = Text("Reward", mid, display, "", 36, Design.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(430, 44));
        reward.enableAutoSizing = true; reward.fontSizeMin = 20; reward.fontSizeMax = 36;
        var right = Box("Times", new Vector2(860, -334), new Vector2(320, 172));   // under the chest sticker
        Text("YourCap", right, body, "YOUR TIME", 24, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(300, 32));
        var yourTime = Text("YourTime", right, display, "", 44, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(300, 52));
        Text("BestCap", right, body, "HIGH SCORE", 24, Design.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(300, 32));
        var best = Text("Best", right, display, "", 40, Design.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(300, 48));
        layout.gameObject.SetActive(false);
        var er = card.gameObject.AddComponent<EventResult>();
        TMP_Text Find(string path) => card.Find(path)?.GetComponentInChildren<TMP_Text>();
        new SerializedObject(er).Do(s =>
        {
            s.FindProperty("layout").objectReferenceValue = layout.gameObject;
            s.FindProperty("headline").objectReferenceValue = Find("Headline");
            s.FindProperty("table").objectReferenceValue = Find("Table");
            s.FindProperty("status").objectReferenceValue = Find("Status");
            s.FindProperty("primaryLabel").objectReferenceValue = Find("Again");
            s.FindProperty("secondaryLabel").objectReferenceValue = Find("Menu");
            s.FindProperty("picture").objectReferenceValue = pic;
            s.FindProperty("trackName").objectReferenceValue = trackName; s.FindProperty("eventTitle").objectReferenceValue = eventTitle;
            s.FindProperty("gotValue").objectReferenceValue = got; s.FindProperty("goalValue").objectReferenceValue = goal;
            s.FindProperty("reward").objectReferenceValue = reward; s.FindProperty("yourTime").objectReferenceValue = yourTime;
            s.FindProperty("best").objectReferenceValue = best;   // the rule stays on the event card: here the stars line has the room
            var arr = s.FindProperty("stars"); arr.arraySize = 3; for (int i = 0; i < 3; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            var ta = s.FindProperty("trackArt"); ta.arraySize = Tracks.All.Length;
            for (int i = 0; i < Tracks.All.Length; i++) ta.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Textures/UI/Tracks/{Tracks.All[i].id}.png");
        });
    }

}
