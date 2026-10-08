using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// What brings a player back, in the menu: Kartel Weekly (the title's line and its screen), the chests (the title's
/// chest and the overlay that opens them, kept across scenes), and the one-line toast at the top of the title.
public static partial class MenuSetup
{
    public const string ChestDir = Art + "Textures/UI/Chests/";
    static readonly string[] TierIds = { "wood", "silver", "gold", "treasure" };
    /// The tiers' light: wood brown, silver, gold, and the golden chest in the brand's own gold.
    public static readonly Color[] TierColours = { new(0.80f, 0.52f, 0.32f), new(0.80f, 0.86f, 0.96f), new(1.00f, 0.78f, 0.25f), Design.Accent };

    public static Sprite[] ChestSprites(bool open) => TierIds.Select(t => UiSprite(ChestDir + $"chest_{t}_{(open ? "open" : "closed")}.png")).ToArray();

    // ---------- the title ----------
    /// Kartel Weekly's line where the on-chain line was (it comes back when the Worker can't be reached), the chest at
    /// bottom right (NEW VERSION moves up over it), and the toast across the top.
    static void TitleRewards(GameObject screen, RectTransform plate)
    {
        // Today's challenge and this week's, side by side where the on-chain line was: the two reasons to come back
        if (plate.Find("Sub") is Transform sub) Object.DestroyImmediate(sub.gameObject);
        var (dailyCard, dailyButton, dailyHead, dailyLine) = EventCard("Daily", plate, new Vector2(120, -500), "flag");
        var daily = plate.gameObject.AddComponent<DailyCard>();
        var doneTick = Rect("Done", dailyCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(40, 40));
        doneTick.pivot = new Vector2(1, 1);
        var dt = doneTick.gameObject.AddComponent<Image>(); dt.sprite = IconSprite("star"); dt.color = Accent; dt.raycastTarget = false;
        new SerializedObject(daily).Do(s =>
        {
            s.FindProperty("title").objectReferenceValue = dailyHead;
            s.FindProperty("line").objectReferenceValue = dailyLine;
            s.FindProperty("done").objectReferenceValue = doneTick.gameObject;
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(dailyButton.onClick, daily.Tap);

        var (card, button, head, line) = EventCard("Weekly", plate, new Vector2(600, -500), "trophy");
        // on the plate, not the card: the card is what it hides, and a hidden card would never run again
        var weekly = plate.gameObject.AddComponent<WeeklyCard>();
        new SerializedObject(weekly).Do(s =>
        {
            s.FindProperty("content").objectReferenceValue = card.gameObject;
            s.FindProperty("title").objectReferenceValue = head;
            s.FindProperty("line").objectReferenceValue = line;
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, weekly.Tap);
        card.gameObject.SetActive(false);                 // until the Worker answers

        // the Tour beside the title's three doors
        if (plate.Find("Actions") is RectTransform actions)
        {
            var tour = Tile("Tour", actions, "flag", "TOUR", new Vector2(0, 0), new Vector2(780, 64), new Vector2(200, 176));
            Wire(tour, nameof(ScreenFlow.ShowTour));
            // and the achievements (Beach Buggy's trophy counter, as a door): what is done, what each pays
            var awards = Tile("Awards", actions, "star", "AWARDS", new Vector2(0, 0), new Vector2(1000, 64), new Vector2(200, 176));
            Wire(awards, nameof(ScreenFlow.ShowAchievements));
        }

        // the chest: what is waiting (bobbing, with a count), or the golden one the stars fill toward
        var tile = Rect("Chest", screen.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 96), new Vector2(232, 232));
        tile.pivot = new Vector2(1, 0);
        Design.AsTile(tile, Deep, 28f, out float tlip);
        var tbutton = tile.gameObject.AddComponent<Button>();
        var tfeel = tile.gameObject.AddComponent<PressFeel>();
        var tface = Face("Chest", tile, tlip);
        var bob = Rect("Bob", tface, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(200, 200));
        var chestImg = bob.gameObject.AddComponent<Image>();
        chestImg.preserveAspect = true; chestImg.raycastTarget = false;
        var closed = ChestSprites(false);
        chestImg.sprite = closed[3];
        var badge = Rect("Count", tile, new Vector2(1, 1), new Vector2(1, 1), new Vector2(14, 14), new Vector2(68, 68));
        badge.pivot = new Vector2(1, 1);
        Design.AsPill(badge, Design.Tiers[0]).raycastTarget = false;
        var count = Label("CountLabel", badge, display, "1", 38, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(68, 68));
        tfeel.Configure(Design.TileDown, tface, tlip * (Design.TileLip - 3f) / Design.TileLip);
        var meter = Rect("Meter", screen.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 40), new Vector2(232, 40));
        meter.pivot = new Vector2(1, 0);
        Design.AsPill(meter, Deep).raycastTarget = false;
        var fillRt = Rect("Fill", meter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fillRt.offsetMin = new Vector2(4, 4); fillRt.offsetMax = new Vector2(-4, -4);
        var fill = Design.AsPill(fillRt, Accent);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0.4f; fill.raycastTarget = false;
        var star = Rect("Star", meter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(34, 34));
        var starImg = star.gameObject.AddComponent<Image>();
        starImg.sprite = IconSprite("star"); starImg.color = Paper; starImg.preserveAspect = true; starImg.raycastTarget = false;
        var meterText = Label("MeterLabel", meter, strong, "0/15", 26, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(12, 1), new Vector2(200, 40));
        var chestButton = tile.gameObject.AddComponent<ChestButton>();
        new SerializedObject(chestButton).Do(s =>
        {
            s.FindProperty("chest").objectReferenceValue = chestImg;
            s.FindProperty("meterFill").objectReferenceValue = fill;
            s.FindProperty("count").objectReferenceValue = count;
            s.FindProperty("meter").objectReferenceValue = meterText;
            s.FindProperty("countBadge").objectReferenceValue = badge.gameObject;
            Fill(s.FindProperty("closedSprites"), closed.Cast<Object>().ToArray());
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tbutton.onClick, chestButton.Tap);
        if (screen.transform.Find("Update") is RectTransform update) update.anchoredPosition = new Vector2(-64, 360);

        // the toast: one line, one action, under the profile chip, over the hoarding
        var toastRoot = Rect("Toast", screen.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -156), new Vector2(1060, 108));
        toastRoot.pivot = new Vector2(1, 1);
        var group = toastRoot.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;
        var pill = Rect("Pill", toastRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Design.AsCard(pill, Panel);
        var text = Label("Text", pill, strong, "", 30, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(770, 80));
        text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 30;
        var act = Button("Act", pill, "RACE", Design.Ink600, Paper, 34, new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(220, 84));
        var toast = toastRoot.gameObject.AddComponent<TitleToast>();
        new SerializedObject(toast).Do(s =>
        {
            s.FindProperty("group").objectReferenceValue = group;
            s.FindProperty("pill").objectReferenceValue = pill;
            s.FindProperty("text").objectReferenceValue = text;
            s.FindProperty("actionLabel").objectReferenceValue = act.GetComponentInChildren<TMP_Text>();
        });
        UnityEditor.Events.UnityEventTools.AddPersistentListener(act.onClick, toast.Act);
    }

    /// A half-width pressable card on the title: a glyph, an eyebrow, one line, and a chevron.
    static (RectTransform card, Button button, TMP_Text head, TMP_Text line) EventCard(string name, RectTransform plate, Vector2 at, string icon)
    {
        var card = Rect(name, plate, new Vector2(0, 1), new Vector2(0, 1), at, new Vector2(460, 118));
        card.pivot = new Vector2(0, 1);
        Design.AsTile(card, Slate, 28f, out float lip);
        var button = card.gameObject.AddComponent<Button>();
        var feel = card.gameObject.AddComponent<PressFeel>();
        var face = Face(name, card, lip);
        var glyph = Rect("Icon", face, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, 0), new Vector2(52, 52));
        var gi = glyph.gameObject.AddComponent<Image>();
        gi.sprite = IconSprite(icon); gi.color = Accent; gi.preserveAspect = true; gi.raycastTarget = false;
        var head = Label("Head", face, strong, "", 24, Accent, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(92, -14), new Vector2(340, 36));
        head.characterSpacing = 4f; head.enableAutoSizing = true; head.fontSizeMin = 18; head.fontSizeMax = 24;
        var line = Label("Line", face, body, "", 26, Paper, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(92, 14), new Vector2(340, 44));
        line.enableAutoSizing = true; line.fontSizeMin = 18; line.fontSizeMax = 26;
        feel.Configure(Design.TileDown, face, lip * (Design.TileLip - 3f) / Design.TileLip);
        return (card, button, head, line);
    }

    // ---------- Kartel Tour's screen ----------
    static GameObject TourScreen(RectTransform parent)
    {
        var screen = Rect("Tour", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.72f);
        var bar = Header(screen.transform, "KARTEL TOUR", nameof(ScreenFlow.ShowTitle));
        var panel = screen.AddComponent<TourPanel>();
        var starIcon = Rect("Star", bar, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(56, 56));
        var si = starIcon.gameObject.AddComponent<Image>(); si.sprite = IconSprite("star"); si.color = Accent; si.raycastTarget = false;
        var total = Label("Total", bar, display, "0 / 60", 48, Paper, TextAlignmentOptions.Left, new Vector2(1, 0.5f), new Vector2(-290, 0), new Vector2(240, 64));

        var names = Tour.Cups.Select(c => c.kicker).ToArray();
        var (tabBacks, tabLabels, tabButtons) = Segmented("Cups", screen.transform, names, new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(1480, 92), 30);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(tabButtons[i].onClick, panel.SetCup, i);
            tabBacks[i].color = i == 0 ? Design.Ink600 : new Color(0, 0, 0, 0);
            tabLabels[i].color = i == 0 ? Paper : Muted;
        }

        // left: the cup, how far through it you are, the boss's prize, and who is driving
        var info = Rect("Card", screen.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(64, 40), new Vector2(700, 760));
        info.pivot = Vector2.zero;
        Design.AsCard(info, Panel);
        var cupName = Label("CupName", info, display, "ROOKIE RUN", 64, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(44, -40), new Vector2(620, 80));
        cupName.enableAutoSizing = true; cupName.fontSizeMin = 40; cupName.fontSizeMax = 64;
        var cupState = Label("CupState", info, body, "", 28, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(46, -130), new Vector2(610, 90));
        cupState.textWrappingMode = TextWrappingModes.Normal;
        Label("PrizeCap", info, strong, "BOSS PRIZE", 24, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(46, -250), new Vector2(400, 34)).characterSpacing = 8f;
        var swatch = Rect("Swatch", info, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -296), new Vector2(110, 110));
        swatch.pivot = new Vector2(0, 1);
        var sw = Design.AsPill(swatch, Accent); sw.raycastTarget = false;
        var prize = Label("Prize", info, strong, "", 30, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(180, -330), new Vector2(480, 80));
        prize.textWrappingMode = TextWrappingModes.Normal; prize.enableAutoSizing = true; prize.fontSizeMin = 22; prize.fontSizeMax = 30;
        var who = Label("Driver", info, strong, "", 30, Paper, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(46, 190), new Vector2(610, 44));
        who.characterSpacing = 4f;
        var change = Button("Change", info, "CHANGE DRIVER", Slate, Paper, 34, new Vector2(0, 0), new Vector2(40, 40), new Vector2(420, 120), icon: "user");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(change.onClick, panel.ChangeDriver);

        // right: the five events
        var list = Rect("List", screen.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 40), new Vector2(1420, 760));
        list.pivot = new Vector2(1, 0);
        Design.AsCard(list, Panel);
        const int count = 5; const float rowH = 128f, gap = 14f;
        var rows = new GameObject[count]; var titles = new TMP_Text[count]; var modes = new TMP_Text[count]; var tracks = new TMP_Text[count];
        var stars = new Image[count * 3]; var playBacks = new Image[count]; var plays = new Button[count];
        for (int i = 0; i < count; i++)
        {
            var rt = Rect("Event" + i, list, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30 - i * (rowH + gap)), new Vector2(1360, rowH));
            rt.pivot = new Vector2(0.5f, 1);
            Design.AsRow(rt, Slate).raycastTarget = false;
            var num = Rect("Num", rt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(76, 76));
            Design.AsPill(num, Deep).raycastTarget = false;
            Label("NumLabel", num, display, i == count - 1 ? "B" : (i + 1).ToString(), 40, i == count - 1 ? Accent : Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(76, 76));
            titles[i] = Label("Title", rt, display, "", 40, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(124, 20), new Vector2(620, 52));
            titles[i].enableAutoSizing = true; titles[i].fontSizeMin = 26; titles[i].fontSizeMax = 40;
            modes[i] = Label("Mode", rt, strong, "", 24, Accent, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(126, -26), new Vector2(240, 34));
            modes[i].characterSpacing = 6f;
            tracks[i] = Label("Track", rt, body, "", 24, Muted, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(330, -26), new Vector2(360, 34));
            for (int k = 0; k < 3; k++)
            {
                var st = Rect("Star" + k, rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-440 + k * 64, 0), new Vector2(56, 56));
                stars[i * 3 + k] = st.gameObject.AddComponent<Image>();
                stars[i * 3 + k].sprite = IconSprite("star"); stars[i * 3 + k].raycastTarget = false;
            }
            var play = Button("Play", rt, "PLAY", Slate, Paper, 36, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(210, 96), icon: "play");
            plays[i] = play; playBacks[i] = play.GetComponent<Image>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(play.onClick, panel.Play, i);
            rows[i] = rt.gameObject;
        }

        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("roster").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
            Fill(s.FindProperty("tabBacks"), tabBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("tabLabels"), tabLabels.Cast<Object>().ToArray());
            s.FindProperty("total").objectReferenceValue = total;
            s.FindProperty("cupName").objectReferenceValue = cupName;
            s.FindProperty("cupState").objectReferenceValue = cupState;
            s.FindProperty("driver").objectReferenceValue = who;
            Fill(s.FindProperty("rows"), rows.Cast<Object>().ToArray());
            Fill(s.FindProperty("titles"), titles.Cast<Object>().ToArray());
            Fill(s.FindProperty("modes"), modes.Cast<Object>().ToArray());
            Fill(s.FindProperty("tracks"), tracks.Cast<Object>().ToArray());
            Fill(s.FindProperty("stars"), stars.Cast<Object>().ToArray());
            Fill(s.FindProperty("playBacks"), playBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("plays"), plays.Cast<Object>().ToArray());
            s.FindProperty("prizeSwatch").objectReferenceValue = sw;
            s.FindProperty("prizeLabel").objectReferenceValue = prize;
            s.FindProperty("tabOn").colorValue = Design.Ink600;
            s.FindProperty("tabOff").colorValue = new Color(0, 0, 0, 0);
            s.FindProperty("textOn").colorValue = Paper;
            s.FindProperty("textOff").colorValue = Muted;
            s.FindProperty("starOn").colorValue = Accent;
            s.FindProperty("starOff").colorValue = new Color(1f, 1f, 1f, 0.16f);
            s.FindProperty("playNext").colorValue = Accent;
            s.FindProperty("playIdle").colorValue = Slate;
        });
        return screen;
    }

    // ---------- Kartel Weekly's screen ----------
    static GameObject WeeklyScreen(RectTransform parent)
    {
        var screen = Rect("Weekly", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var dim = Rect("Dim", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        dim.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.72f);
        Header(screen.transform, "KARTEL WEEKLY", nameof(ScreenFlow.ShowTitle));
        var panel = screen.AddComponent<WeeklyPanel>();

        // left: the circuit, the clock, the prizes, the rules in a line, RACE
        var info = Rect("Card", screen.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(64, 40), new Vector2(820, 790));
        info.pivot = Vector2.zero;
        Design.AsCard(info, Panel);
        var artBox = Rect("Art", info, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(764, 250));
        artBox.pivot = new Vector2(0.5f, 1);
        Design.AsCard(artBox, Slate).raycastTarget = false;
        artBox.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var art = Rect("Picture", artBox, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        art.preserveAspect = false; art.raycastTarget = false;
        var trackArt = Tracks.All.Select(t => UiSprite(Art + $"Textures/UI/Tracks/{t.id}.png")).ToArray();
        art.sprite = trackArt.FirstOrDefault();
        var name = Label("Track", info, display, "PALM BAY", 66, Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(40, -296), new Vector2(740, 80));
        var clock = Label("Clock", info, strong, "", 26, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(42, -376), new Vector2(740, 40));
        clock.characterSpacing = 6f;
        var prizes = Label("Prizes", info, strong, "", 32, Paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(42, -428), new Vector2(740, 90));
        prizes.textWrappingMode = TextWrappingModes.Normal;
        var rules = Label("Rules", info, body, "Free to enter  ·  the same kart for everyone, items off  ·  your best lap counts  ·  one prize per Seeker  ·  18+  ·  not open in India",
                          24, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(42, -526), new Vector2(740, 84));
        rules.textWrappingMode = TextWrappingModes.Normal;
        var race = Button("Race", info, "RACE", Accent, Paper, 56, new Vector2(0, 0), new Vector2(40, 36), new Vector2(456, 132), icon: "flag");
        Shine(race);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(race.onClick, panel.Race);
        var rulesButton = Button("RulesButton", info, "RULES", Slate, Paper, 38, new Vector2(1, 0), new Vector2(-40, 36), new Vector2(260, 132));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(rulesButton.onClick, panel.Rules);

        // right: the board
        var board = Rect("Board", screen.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-64, 40), new Vector2(1290, 790));
        board.pivot = new Vector2(1, 0);
        Design.AsCard(board, Panel);
        Label("RankCap", board, strong, "#", 26, Muted, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(72, -28), new Vector2(80, 36));
        Label("NameCap", board, strong, "DRIVER", 26, Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(150, -28), new Vector2(400, 36)).characterSpacing = 8f;
        Label("PrizeCap", board, strong, "PRIZE", 26, Muted, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-330, -28), new Vector2(240, 36)).characterSpacing = 8f;
        Label("TimeCap", board, strong, "BEST LAP", 26, Muted, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-56, -28), new Vector2(240, 36)).characterSpacing = 8f;
        const int count = 8; const float rowH = 64f, rowGap = 8f;
        var rowObjects = new GameObject[count]; var rowBacks = new Image[count];
        var ranks = new TMP_Text[count]; var names = new TMP_Text[count]; var times = new TMP_Text[count]; var prizeLabels = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            var rt = Rect("Row" + i, board, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80 - i * (rowH + rowGap)), new Vector2(1210, rowH));
            rt.pivot = new Vector2(0.5f, 1);
            rowBacks[i] = Design.AsRow(rt, Slate); rowBacks[i].raycastTarget = false;
            ranks[i] = Label("Rank", rt, display, (i + 1).ToString(), 32, Paper, TextAlignmentOptions.Center, new Vector2(0, 0.5f), new Vector2(68, 0), new Vector2(80, 50));
            ranks[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            names[i] = Label("Driver", rt, strong, "", 32, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(640, 50));
            prizeLabels[i] = Label("Prize", rt, strong, "", 30, Accent, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-290, 0), new Vector2(240, 50));
            times[i] = Label("Time", rt, strong, "", 32, Paper, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-36, 0), new Vector2(240, 50));
            rowObjects[i] = rt.gameObject;
            rt.gameObject.SetActive(false);
        }
        var state = Label("State", board, body, "Loading this week…", 34, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1100, 140));
        state.textWrappingMode = TextWrappingModes.Normal;
        var mine = Label("Mine", board, strong, "", 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1210, 50));
        mine.enableAutoSizing = true; mine.fontSizeMin = 22; mine.fontSizeMax = 30;

        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("art").objectReferenceValue = art;
            Fill(s.FindProperty("trackArt"), trackArt.Cast<Object>().ToArray());
            s.FindProperty("trackName").objectReferenceValue = name;
            s.FindProperty("clock").objectReferenceValue = clock;
            s.FindProperty("prizes").objectReferenceValue = prizes;
            s.FindProperty("mine").objectReferenceValue = mine;
            s.FindProperty("state").objectReferenceValue = state;
            Fill(s.FindProperty("rowObjects"), rowObjects.Cast<Object>().ToArray());
            Fill(s.FindProperty("rowBacks"), rowBacks.Cast<Object>().ToArray());
            Fill(s.FindProperty("ranks"), ranks.Cast<Object>().ToArray());
            Fill(s.FindProperty("names"), names.Cast<Object>().ToArray());
            Fill(s.FindProperty("times"), times.Cast<Object>().ToArray());
            Fill(s.FindProperty("prizeLabels"), prizeLabels.Cast<Object>().ToArray());
            s.FindProperty("rowIdle").colorValue = Slate;
            s.FindProperty("rowMine").colorValue = Design.Ink600;
        });
        return screen;
    }

    // ---------- opening chests ----------
    /// ChestReveal's overlay: its own canvas over the menus and the race (under the loading curtain), kept across
    /// scenes once the menu has run.
    static void ChestOverlay()
    {
        var go = new GameObject("ChestReveal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400, 1080);
        scaler.matchWidthOrHeight = 1f;
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;
        var reveal = go.AddComponent<ChestReveal>();

        var back = Rect("Back", go.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        back.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.985f);   // blended in linear, 5% left the title's words legible
        var tap = back.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(tap.onClick, reveal.Tap);

        var rays = Rect("Rays", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 210), new Vector2(1500, 1500));
        var raysImg = rays.gameObject.AddComponent<Image>();
        raysImg.sprite = Design.LightRays; raysImg.color = new Color(1, 1, 1, 0f); raysImg.raycastTarget = false;
        rays.gameObject.SetActive(false);
        var glow = Rect("Glow", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1000, 1000));
        var glowImg = glow.gameObject.AddComponent<Image>();
        glowImg.sprite = Design.Glow; glowImg.color = new Color(1, 1, 1, 0.2f); glowImg.raycastTarget = false;
        var chest = Rect("Chest", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(420, 420));
        var chestImg = chest.gameObject.AddComponent<Image>();
        chestImg.preserveAspect = true; chestImg.raycastTarget = false;
        var tier = Label("Tier", go.transform, display, "", 76, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(2000, 96));
        tier.enableAutoSizing = true; tier.fontSizeMin = 48; tier.fontSizeMax = 76;
        var hint = Label("Hint", go.transform, strong, "", 40, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1400, 60));
        hint.characterSpacing = 12f;

        // what is inside: up to eight cards in two rows of four, flipped over one at a time
        var cards = new GameObject[8]; var icons = new Image[8]; var edges = new Image[8]; var labels = new TMP_Text[8];
        for (int i = 0; i < 8; i++)
        {
            int row = i / 4, col = i % 4;
            var card = Rect("Card" + i, go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-606 + col * 404, -60 - row * 214), new Vector2(380, 196));
            Design.AsCard(card, Panel).raycastTarget = false;
            edges[i] = card.Find("Edge")?.GetComponent<Image>();
            var icon = Rect("Icon", card, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(88, 0), new Vector2(120, 120));
            icons[i] = icon.gameObject.AddComponent<Image>(); icons[i].preserveAspect = true; icons[i].raycastTarget = false;
            labels[i] = Label("Label", card, strong, "", 30, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(170, 0), new Vector2(196, 170));
            labels[i].textWrappingMode = TextWrappingModes.Normal;
            labels[i].enableAutoSizing = true; labels[i].fontSizeMin = 20; labels[i].fontSizeMax = 30;
            cards[i] = card.gameObject;
            card.gameObject.SetActive(false);
        }

        // what flies out of an opened chest, and what twinkles round it (pooled, off until a chest bursts)
        var coins = new RectTransform[20];
        for (int i = 0; i < coins.Length; i++)
        {
            coins[i] = Rect("Coin" + i, go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            var ci = coins[i].gameObject.AddComponent<Image>(); ci.sprite = Design.Coin; ci.raycastTarget = false;
            coins[i].gameObject.SetActive(false);
        }
        var sparkles = new RectTransform[10];
        for (int i = 0; i < sparkles.Length; i++)
        {
            sparkles[i] = Rect("Sparkle" + i, go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
            var si = sparkles[i].gameObject.AddComponent<Image>(); si.sprite = IconSprite("star"); si.color = new Color(1f, 0.95f, 0.75f, 0.9f); si.raycastTarget = false;
            sparkles[i].gameObject.SetActive(false);
        }
        // with nothing to open: how chests come, three tiles to glance at; the drop rates (info) behind DROP RATES
        var infoRoot = Rect("Info", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2000, 1080));
        var howTo = Rect("HowTo", infoRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(1620, 270));
        var stars = Chests.StarsFor;
        var tiles = new (Sprite icon, bool tinted, string title, string line)[]
        {
            (IconSprite("trophy"), true, "FINISH TOP 3", "win a chest"),
            (IconSprite("star"), true, "EARN STARS", $"1st {stars[0]}  ·  2nd {stars[1]}  ·  3rd {stars[2]}"),
            (ChestSprites(false)[(int)Chests.Tier.Treasure], false, "FILL THE METER", "Golden Treasure Chest"),
        };
        for (int i = 0; i < tiles.Length; i++)
        {
            var tile = Rect("Tile" + i, howTo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 560, 0), new Vector2(500, 270));
            Design.AsCard(tile, Panel).raycastTarget = false;
            var big = tiles[i].tinted ? 110 : 150;   // the chest sprite has room round it; the glyphs don't
            var icon = Rect("Icon", tile, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 52), new Vector2(big, big)).gameObject.AddComponent<Image>();
            icon.sprite = tiles[i].icon; icon.color = tiles[i].tinted ? Accent : Color.white; icon.preserveAspect = true; icon.raycastTarget = false;
            Label("Title", tile, strong, tiles[i].title, 40, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(470, 54));
            Label("Line", tile, body, tiles[i].line, 28, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -92), new Vector2(470, 40));
            if (i == 0) continue;
            var arrow = Rect("Arrow" + i, howTo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 560, 0), new Vector2(44, 44)).gameObject.AddComponent<Image>();
            arrow.sprite = IconSprite("next"); arrow.color = Muted; arrow.raycastTarget = false;
        }
        var info = Label("Rates", infoRoot, body, "", 34, Paper, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(40, -80), new Vector2(1060, 300));
        info.lineSpacing = 36f;
        info.gameObject.SetActive(false);
        var rates = Button("RatesButton", infoRoot, "DROP RATES", Slate, Paper, 30, new Vector2(0.5f, 0.5f), new Vector2(0, -290), new Vector2(340, 84));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(rates.onClick, reveal.ToggleRates);
        infoRoot.gameObject.SetActive(false);

        var collect = Button("Collect", go.transform, "COLLECT", Accent, Paper, 46, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(520, 124));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(collect.onClick, reveal.Collect);
        collect.gameObject.SetActive(false);

        new SerializedObject(reveal).Do(s =>
        {
            s.FindProperty("group").objectReferenceValue = group;
            s.FindProperty("chest").objectReferenceValue = chest;
            s.FindProperty("glow").objectReferenceValue = glow;
            s.FindProperty("chestImage").objectReferenceValue = chestImg;
            s.FindProperty("glowImage").objectReferenceValue = glowImg;
            s.FindProperty("tierLabel").objectReferenceValue = tier;
            s.FindProperty("hint").objectReferenceValue = hint;
            s.FindProperty("info").objectReferenceValue = info;
            s.FindProperty("collectLabel").objectReferenceValue = collect.GetComponentInChildren<TMP_Text>();
            s.FindProperty("collectButton").objectReferenceValue = collect.gameObject;
            s.FindProperty("infoRoot").objectReferenceValue = infoRoot.gameObject;
            s.FindProperty("howTo").objectReferenceValue = howTo.gameObject;
            s.FindProperty("ratesLabel").objectReferenceValue = rates.GetComponentInChildren<TMP_Text>();
            Fill(s.FindProperty("cards"), cards.Cast<Object>().ToArray());
            Fill(s.FindProperty("cardIcons"), icons.Cast<Object>().ToArray());
            Fill(s.FindProperty("cardEdges"), edges.Cast<Object>().ToArray());
            Fill(s.FindProperty("cardLabels"), labels.Cast<Object>().ToArray());
            Fill(s.FindProperty("closedSprites"), ChestSprites(false).Cast<Object>().ToArray());
            Fill(s.FindProperty("openSprites"), ChestSprites(true).Cast<Object>().ToArray());
            var colours = s.FindProperty("tierColors");
            colours.arraySize = TierColours.Length;
            for (int i = 0; i < TierColours.Length; i++) colours.GetArrayElementAtIndex(i).colorValue = TierColours[i];
            var ids = s.FindProperty("itemIds");
            ids.arraySize = Inventory.Packs.Length;
            for (int i = 0; i < Inventory.Packs.Length; i++) ids.GetArrayElementAtIndex(i).stringValue = Inventory.Packs[i].id;
            Fill(s.FindProperty("itemIcons"), Inventory.Packs.Select(p => (Object)Design.Icon(Design.ItemGlyph(p.item))).ToArray());
            s.FindProperty("kartIcon").objectReferenceValue = IconSprite("kart");
            s.FindProperty("paintIcon").objectReferenceValue = Design.Pill;
            s.FindProperty("karts").objectReferenceValue = AssetDatabase.LoadAssetAtPath<KartDatabase>("Assets/_Game/Data/Karts.asset");
            s.FindProperty("rays").objectReferenceValue = rays;
            s.FindProperty("raysImage").objectReferenceValue = raysImg;
            Fill(s.FindProperty("coins"), coins.Cast<Object>().ToArray());
            Fill(s.FindProperty("sparkles"), sparkles.Cast<Object>().ToArray());
        });
    }
}
