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
        var (dailyCard, dailyButton, dailyHead, dailyLine) = EventCard("Daily", plate, new Vector2(64, -300), "flag");   // the left edge, clear of the hero
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

        var (card, button, head, line) = EventCard("Weekly", plate, new Vector2(64, -440), "trophy");
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

        // the chest in the top bar (Beach Buggy's gift box): what is waiting (bobbing, with a count), or the golden one the stars fill toward
        var tile = Rect("Chest", screen.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(704, -16), new Vector2(124, 108));
        tile.pivot = new Vector2(0, 1);
        Design.AsTile(tile, Deep, 28f, out float tlip);
        var tbutton = tile.gameObject.AddComponent<Button>();
        var tfeel = tile.gameObject.AddComponent<PressFeel>();
        var tface = Face("Chest", tile, tlip);
        var bob = Rect("Bob", tface, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(92, 92));
        var chestImg = bob.gameObject.AddComponent<Image>();
        chestImg.preserveAspect = true; chestImg.raycastTarget = false;
        var closed = ChestSprites(false);
        chestImg.sprite = closed[3];
        var badge = Rect("Count", tile, new Vector2(1, 1), new Vector2(1, 1), new Vector2(12, 12), new Vector2(52, 52));
        badge.pivot = new Vector2(1, 1);
        Design.AsPill(badge, Design.Tiers[0]).raycastTarget = false;
        var count = Label("CountLabel", badge, display, "1", 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(52, 52));
        tfeel.Configure(Design.TileDown, tface, tlip * (Design.TileLip - 3f) / Design.TileLip);
        // the star meter beside it (Beach Buggy's trophy counter): how far to the next golden chest
        var meter = Rect("Meter", screen.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(448, -40), new Vector2(232, 56));
        meter.pivot = new Vector2(0, 1);
        Design.AsPill(meter, Deep).raycastTarget = false;
        var fillRt = Rect("Fill", meter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fillRt.offsetMin = new Vector2(4, 4); fillRt.offsetMax = new Vector2(-4, -4);
        var fill = Design.AsPill(fillRt, Accent);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0.4f; fill.raycastTarget = false;
        var star = Rect("Star", meter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(34, 34));
        var starImg = star.gameObject.AddComponent<Image>();
        starImg.sprite = IconSprite("star"); starImg.color = Paper; starImg.preserveAspect = true; starImg.raycastTarget = false;
        var meterText = Label("MeterLabel", meter, strong, "0/15", 28, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(12, 1), new Vector2(200, 44));
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
        DailyWheel(screen);   // the daily wheel (MenuSetup.Spin.cs), then into the top bar beside the chest: the wheel at the left, the timer at the right
        if (screen.transform.Find("Spin") is RectTransform spin)
        {
            spin.anchorMin = spin.anchorMax = spin.pivot = new Vector2(0, 1); spin.anchoredPosition = new Vector2(852, -16); spin.sizeDelta = new Vector2(216, 108);
            if (spin.Find("SpinFace/Wheel") is RectTransform wheel) { wheel.anchorMin = wheel.anchorMax = new Vector2(0, 0.5f); wheel.pivot = new Vector2(0.5f, 0.5f); wheel.anchoredPosition = new Vector2(50, 0); wheel.sizeDelta = new Vector2(76, 76); }
            if (spin.Find("SpinFace/Line") is RectTransform timer)
            {
                timer.anchorMin = timer.anchorMax = new Vector2(1, 0.5f); timer.pivot = new Vector2(1, 0.5f); timer.anchoredPosition = new Vector2(-12, 0); timer.sizeDelta = new Vector2(112, 48);
                var t = timer.GetComponent<TMP_Text>(); t.fontSize = 22; t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 22;
            }
        }

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
        var bar = Header(screen.transform, "CHOOSE SERIES", nameof(ScreenFlow.TourBack));
        var panel = screen.AddComponent<TourPanel>();
        var starIcon = Rect("Star", bar, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-330, 0), new Vector2(56, 56));
        var si = starIcon.gameObject.AddComponent<Image>(); si.sprite = IconSprite("star"); si.color = Accent; si.raycastTarget = false;
        var total = Label("Total", bar, display, "0 / 60", 48, Paper, TextAlignmentOptions.Left, new Vector2(1, 0.5f), new Vector2(-290, 0), new Vector2(240, 64));

        // Beach Buggy's Choose Series: a row of cards, one a series. The name on top, a picture of its first circuit, a
        // padlock with the boss's face while it is locked, the stars in it (or what opens it) along the bottom. A tap
        // opens that series' events (Detail); the header's back returns here, then home.
        var pick = Rect("Pick", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        int n = Tour.Cups.Length; const float cardW = 400f, cardH = 700f, cardGap = 36f;
        var seriesPics = new Image[n]; var seriesBoss = new Image[n]; var seriesLocks = new GameObject[n]; var seriesStars = new GameObject[n]; var seriesFoots = new TMP_Text[n];
        var rosterDb = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        for (int i = 0; i < n; i++)
        {
            float x = (i - (n - 1) / 2f) * (cardW + cardGap);
            var rt = Rect("Series" + i, pick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -36), new Vector2(cardW, cardH));
            Design.AsCard(rt, Panel);
            var b = rt.gameObject.AddComponent<Button>(); rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(b.onClick, panel.Pick, i);
            var head = Rect("Head", rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(cardW - 28, 88));
            head.pivot = new Vector2(0.5f, 1);
            Design.AsRow(head, Design.Ink700).raycastTarget = false;
            var nm = Label("Name", head, display, Tour.Cups[i].name, 38, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(cardW - 44, 80));
            nm.enableAutoSizing = true; nm.fontSizeMin = 24; nm.fontSizeMax = 38;
            var picRt = Rect("Picture", rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(cardW - 28, 436));
            picRt.pivot = new Vector2(0.5f, 1);
            picRt.gameObject.AddComponent<RectMask2D>();
            var art = Rect("Art", picRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(872, 436));   // the 2:1 circuit art, cropped to the card
            seriesPics[i] = art.gameObject.AddComponent<Image>(); seriesPics[i].raycastTarget = false;
            var lockRoot = Rect("Lock", picRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var dimImg = lockRoot.gameObject.AddComponent<Image>(); dimImg.color = new Color(Deep.r, Deep.g, Deep.b, 0.55f); dimImg.raycastTarget = false;
            var padlock = Rect("Padlock", lockRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(250, 250));
            var pl = padlock.gameObject.AddComponent<Image>(); pl.sprite = IconSprite("lock"); pl.color = Paper; pl.preserveAspect = true; pl.raycastTarget = false;
            var bossRt = Rect("Boss", padlock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(104, 104));
            seriesBoss[i] = bossRt.gameObject.AddComponent<Image>(); seriesBoss[i].preserveAspect = true; seriesBoss[i].raycastTarget = false;
            seriesLocks[i] = lockRoot.gameObject;
            var foot = Rect("Foot", rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(cardW - 28, 92));
            foot.pivot = new Vector2(0.5f, 0);
            Design.AsRow(foot, Design.Ink700).raycastTarget = false;
            var st = Rect("Star", foot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(50, 50));
            var si2 = st.gameObject.AddComponent<Image>(); si2.sprite = IconSprite("star"); si2.color = Accent; si2.preserveAspect = true; si2.raycastTarget = false;
            seriesStars[i] = st.gameObject;
            seriesFoots[i] = Label("Foot", foot, strong, "", 32, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(92, 0), new Vector2(cardW - 130, 60));
            seriesFoots[i].enableAutoSizing = true; seriesFoots[i].fontSizeMin = 20; seriesFoots[i].fontSizeMax = 32;
        }
        Label("Caption", pick, body, "Choose the race series you want to play.", 34, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1600, 50));
        var detail = Rect("Detail", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        // Inside a series (Beach Buggy's): its events as tiles, three stars across each top, the number on a faint
        // chequered flag; a locked one a padlock with the stars it needs (Tour.EventGates); the boss's tile wears the
        // boss's face. A bar along the bottom: the series and its stars. A tap opens the event card.
        int ev = 5; const float tileW = 300f, tileH = 230f, tileGap = 40f;
        var tiles = new GameObject[ev]; var tileNums = new TMP_Text[ev]; var tileStars = new Image[ev * 3];
        var tileLocks = new GameObject[ev]; var tileGates = new TMP_Text[ev]; var tileBoss = new Image[ev];
        var tileModes = new TMP_Text[ev]; var tileWheres = new TMP_Text[ev];
        for (int i = 0; i < ev; i++)
        {
            int row = i < 3 ? 0 : 1, col = row == 0 ? i : i - 3, inRow = row == 0 ? 3 : 2;
            float x = (col - (inRow - 1) / 2f) * (tileW + tileGap), y = row == 0 ? 190f : -110f;
            var rt = Rect("Event" + i, detail, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(tileW, tileH));
            Design.AsCard(rt, Design.Ink700);
            var b = rt.gameObject.AddComponent<Button>(); rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(b.onClick, panel.Play, i);
            // the number (or the boss's face, or a padlock) in the top two thirds; what the event is under it, so the tiles
            // say NITRO RUSH and PALM BAY · REVERSE before a tap (10 Oct 2026: the numbers alone said nothing)
            var flag = Rect("Flag", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(120, 120));
            var fi = flag.gameObject.AddComponent<Image>(); fi.sprite = IconSprite("flag"); fi.color = new Color(1f, 1f, 1f, 0.12f); fi.preserveAspect = true; fi.raycastTarget = false;
            tileNums[i] = Label("Num", rt, display, (i + 1).ToString(), 96, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(tileW, 110));
            var bossRt = Rect("Boss", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(116, 116));
            tileBoss[i] = bossRt.gameObject.AddComponent<Image>(); tileBoss[i].preserveAspect = true; tileBoss[i].raycastTarget = false;
            tileModes[i] = Label("Mode", rt, display, "", 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(tileW - 24, 40));
            tileModes[i].enableAutoSizing = true; tileModes[i].fontSizeMin = 16; tileModes[i].fontSizeMax = 30;
            tileWheres[i] = Label("Where", rt, body, "", 22, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(tileW - 24, 30));
            tileWheres[i].enableAutoSizing = true; tileWheres[i].fontSizeMin = 13; tileWheres[i].fontSizeMax = 22;
            var lockRoot = Rect("Lock", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var padlock = Rect("Padlock", lockRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(124, 124));
            var pl = padlock.gameObject.AddComponent<Image>(); pl.sprite = IconSprite("lock"); pl.color = Paper; pl.preserveAspect = true; pl.raycastTarget = false;
            var gateStar = Rect("GateStar", padlock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16, -21), new Vector2(32, 32));
            var gs = gateStar.gameObject.AddComponent<Image>(); gs.sprite = IconSprite("star"); gs.color = Accent; gs.preserveAspect = true; gs.raycastTarget = false;
            tileGates[i] = Label("Gate", padlock, display, "1", 34, Deep, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(17, -19), new Vector2(44, 44));
            tileLocks[i] = lockRoot.gameObject;
            for (int k = 0; k < 3; k++)   // over the top edge, as Beach Buggy hangs them
            {
                var st = Rect("Star" + k, rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((k - 1) * 72, 8), new Vector2(70, 70));
                tileStars[i * 3 + k] = st.gameObject.AddComponent<Image>(); tileStars[i * 3 + k].sprite = IconSprite("star"); tileStars[i * 3 + k].raycastTarget = false;
            }
            tiles[i] = rt.gameObject;
        }
        var bar2 = Rect("SeriesBar", detail, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 116));
        bar2.pivot = new Vector2(0.5f, 0);
        Design.AsRow(bar2, Design.Ink700).raycastTarget = false;
        // the series' name is the header's now; the bar says what opens next, or why a tapped event is shut
        var hint = Label("Hint", bar2, strong, "", 38, Muted, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(1500, 80));
        hint.enableAutoSizing = true; hint.fontSizeMin = 22; hint.fontSizeMax = 38;
        var seriesTotal = Label("Stars", bar2, display, "0 / 15", 60, Paper, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-200, 0), new Vector2(400, 96));
        var barStar = Rect("Star", bar2, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(84, 84));
        var bsi = barStar.gameObject.AddComponent<Image>(); bsi.sprite = IconSprite("star"); bsi.color = Accent; bsi.preserveAspect = true; bsi.raycastTarget = false;

        // The event card (Beach Buggy's): the circuit at the left, the mode with its stars, goal and reward in the middle,
        // the best time and the rule at the right, PLAY at the bottom right. A tap on the dim closes it.
        var cardRoot = Rect("EventCard", screen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var cardDim = cardRoot.gameObject.AddComponent<Image>(); cardDim.color = new Color(Deep.r, Deep.g, Deep.b, 0.93f);   // the tiles' stars showed through at 0.82
        var closeCard = cardRoot.gameObject.AddComponent<Button>(); closeCard.transition = Selectable.Transition.None;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(closeCard.onClick, panel.CloseCard);
        var ec = Rect("Card", cardRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1640, 620));
        Image Panel3(string name, Vector2 at, Vector2 size) { var r = Rect(name, ec, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, size); var img = Design.AsCard(r, Panel); img.raycastTarget = true; return img; }
        var left = Panel3("Track", new Vector2(-600, -10), new Vector2(420, 480)).rectTransform;
        var mid = Panel3("Middle", new Vector2(0, 0), new Vector2(700, 600)).rectTransform;
        var right = Panel3("Best", new Vector2(600, -10), new Vector2(420, 480)).rectTransform;
        var picMask = Rect("Picture", left, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(380, 230));
        picMask.pivot = new Vector2(0.5f, 1); picMask.gameObject.AddComponent<RectMask2D>();
        var cardArt = Rect("Art", picMask, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 230));
        var cPic = cardArt.gameObject.AddComponent<Image>(); cPic.raycastTarget = false;
        var cTrack = Label("Track", left, display, "", 44, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -276), new Vector2(390, 60));
        cTrack.enableAutoSizing = true; cTrack.fontSizeMin = 18; cTrack.fontSizeMax = 44;   // "FROST PEAK · REVERSE · MIST" fits
        var cTitle = Label("Title", left, strong, "", 30, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -346), new Vector2(390, 44));
        cTitle.enableAutoSizing = true; cTitle.fontSizeMin = 20; cTitle.fontSizeMax = 30;
        var modeHead = Rect("Head", mid, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(640, 110));
        modeHead.pivot = new Vector2(0.5f, 1);
        Design.AsRow(modeHead, Design.Ink700).raycastTarget = false;
        var cMode = Label("Mode", modeHead, display, "RACE", 66, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(620, 100));
        var cardStars = new Image[3];
        for (int k = 0; k < 3; k++)
        {
            var st = Rect("Star" + k, mid, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((k - 1) * 170, k == 1 ? -210 : -230), new Vector2(k == 1 ? 170f : 150f, k == 1 ? 170f : 150f));
            cardStars[k] = st.gameObject.AddComponent<Image>(); cardStars[k].sprite = IconSprite("star"); cardStars[k].raycastTarget = false;
        }
        var goalRow = Rect("GoalRow", mid, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(640, 170));
        goalRow.pivot = new Vector2(0.5f, 0);
        Design.AsRow(goalRow, Design.Ink700).raycastTarget = false;
        Label("GoalCap", goalRow, strong, "GOAL", 30, Muted, TextAlignmentOptions.Center, new Vector2(0.25f, 1), new Vector2(0, -18), new Vector2(300, 40)).characterSpacing = 6f;
        Label("RewardCap", goalRow, strong, "REWARD", 30, Muted, TextAlignmentOptions.Center, new Vector2(0.75f, 1), new Vector2(0, -18), new Vector2(300, 40)).characterSpacing = 6f;
        var cGoal = Label("Goal", goalRow, display, "1ST", 64, Paper, TextAlignmentOptions.Center, new Vector2(0.25f, 0), new Vector2(0, 18), new Vector2(300, 90));
        cGoal.enableAutoSizing = true; cGoal.fontSizeMin = 32; cGoal.fontSizeMax = 64;
        var cPrize = Label("Reward", goalRow, display, "5 GEMS", 64, Accent, TextAlignmentOptions.Center, new Vector2(0.75f, 0), new Vector2(0, 18), new Vector2(300, 90));
        cPrize.enableAutoSizing = true; cPrize.fontSizeMin = 28; cPrize.fontSizeMax = 64;
        Label("BestCap", right, strong, "HIGH SCORE", 32, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(390, 44)).characterSpacing = 6f;
        var cBest = Label("BestTime", right, display, "--:--", 76, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(390, 96));
        var cRule = Label("Rule", right, body, "", 32, Paper, TextAlignmentOptions.Top, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 220));
        cRule.textWrappingMode = TextWrappingModes.Normal;
        var raceBtn = Button("Race", cardRoot, "PLAY", Accent, Paper, 64, new Vector2(1, 0), new Vector2(-64, 40), new Vector2(480, 150), icon: "flag");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(raceBtn.onClick, panel.Race);
        Shine(raceBtn);
        cardRoot.gameObject.SetActive(false);

        new SerializedObject(panel).Do(s =>
        {
            s.FindProperty("roster").objectReferenceValue = rosterDb;
            s.FindProperty("total").objectReferenceValue = total;
            Fill(s.FindProperty("tiles"), tiles.Cast<Object>().ToArray());
            Fill(s.FindProperty("tileNums"), tileNums.Cast<Object>().ToArray());
            Fill(s.FindProperty("tileStars"), tileStars.Cast<Object>().ToArray());
            Fill(s.FindProperty("tileLocks"), tileLocks.Cast<Object>().ToArray());
            Fill(s.FindProperty("tileGates"), tileGates.Cast<Object>().ToArray());
            Fill(s.FindProperty("tileBoss"), tileBoss.Cast<Object>().ToArray());
            s.FindProperty("headerTitle").objectReferenceValue = bar.Find("Title")?.GetComponent<TMP_Text>();
            s.FindProperty("hint").objectReferenceValue = hint;
            Fill(s.FindProperty("tileModes"), tileModes.Cast<Object>().ToArray());
            Fill(s.FindProperty("tileWheres"), tileWheres.Cast<Object>().ToArray());
            s.FindProperty("seriesTotal").objectReferenceValue = seriesTotal;
            s.FindProperty("card").objectReferenceValue = cardRoot.gameObject;
            s.FindProperty("cardPic").objectReferenceValue = cPic;
            s.FindProperty("cardMode").objectReferenceValue = cMode; s.FindProperty("cardTitle").objectReferenceValue = cTitle;
            s.FindProperty("cardTrack").objectReferenceValue = cTrack; s.FindProperty("cardRule").objectReferenceValue = cRule;
            s.FindProperty("cardGoal").objectReferenceValue = cGoal; s.FindProperty("cardPrize").objectReferenceValue = cPrize;
            s.FindProperty("cardBest").objectReferenceValue = cBest;
            Fill(s.FindProperty("cardStars"), cardStars.Cast<Object>().ToArray());
            s.FindProperty("pick").objectReferenceValue = pick.gameObject;
            s.FindProperty("detail").objectReferenceValue = detail.gameObject;
            Fill(s.FindProperty("seriesPics"), seriesPics.Cast<Object>().ToArray());
            Fill(s.FindProperty("seriesBoss"), seriesBoss.Cast<Object>().ToArray());
            Fill(s.FindProperty("seriesLocks"), seriesLocks.Cast<Object>().ToArray());
            Fill(s.FindProperty("seriesStars"), seriesStars.Cast<Object>().ToArray());
            Fill(s.FindProperty("seriesFoots"), seriesFoots.Cast<Object>().ToArray());
            Fill(s.FindProperty("trackArt"), Tracks.All.Select(t => AssetDatabase.LoadAssetAtPath<Sprite>(Art + $"Textures/UI/Tracks/{t.id}.png")).Cast<Object>().ToArray());
            var driverIds = rosterDb != null ? rosterDb.characters.Where(c => c != null).Select(c => c.driverId).ToArray() : new string[0];
            var ids = s.FindProperty("portraitIds"); ids.arraySize = driverIds.Length;
            for (int i = 0; i < driverIds.Length; i++) ids.GetArrayElementAtIndex(i).stringValue = driverIds[i];
            Fill(s.FindProperty("portraits"), driverIds.Select(id => AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSetup.Dir + id + ".png")).Cast<Object>().ToArray());
            s.FindProperty("starOn").colorValue = Accent;
            s.FindProperty("starOff").colorValue = new Color(1f, 1f, 1f, 0.16f);
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
