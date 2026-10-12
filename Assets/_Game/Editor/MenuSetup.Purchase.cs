using System.Collections.Generic;
using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The purchase sheet (PurchaseSheet): its own canvas over the menu, opened by every buy. A card with what is being bought,
// its picture and price, the wallet's step under a spinner, then a PURCHASED stamp, PAID · CONFIRMING, or the picture in
// grey with what went wrong in red, and DONE / CLOSE.
public static partial class MenuSetup
{
    static void PurchaseOverlay()
    {
        var go = new GameObject("PurchaseSheet", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4600;                                  // over the chest reveal (4500)
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2400, 1080);
        scaler.matchWidthOrHeight = 1f;
        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;
        var sheet = go.AddComponent<PurchaseSheet>();

        var back = Rect("Back", go.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        back.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.9f);   // a dead tap while it works

        var card = Rect("Card", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1160, 900));
        Design.AsCard(card, Panel).raycastTarget = true;
        var title = Label("Title", card, display, "BUYING", 60, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(1060, 80));
        title.enableAutoSizing = true; title.fontSizeMin = 36; title.fontSizeMax = 60;

        // the picture, with a soft light behind it
        var glow = Rect("Glow", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(760, 520));
        var glowImg = glow.gameObject.AddComponent<Image>(); glowImg.sprite = Design.Glow; glowImg.color = new Color(1, 1, 1, 0.16f); glowImg.raycastTarget = false;
        var pic = Rect("Picture", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(600, 380));
        var picImg = pic.gameObject.AddComponent<Image>(); picImg.preserveAspect = true; picImg.raycastTarget = false;
        var price = Label("Price", card, strong, "", 44, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -126), new Vector2(900, 60));

        // PURCHASED: a rubber stamp, two gold borders and the word, landed crooked across the picture
        var stamp = Rect("Stamp", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 40), new Vector2(660, 184));
        var stampGroup = stamp.gameObject.AddComponent<CanvasGroup>(); stampGroup.blocksRaycasts = false;
        var outer = Rect("Outer", stamp, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var outerImg = outer.gameObject.AddComponent<Image>(); outerImg.sprite = Design.CardEdge; outerImg.type = Image.Type.Sliced; outerImg.pixelsPerUnitMultiplier = 0.45f; outerImg.color = Accent; outerImg.raycastTarget = false;
        var inner = Rect("Inner", stamp, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        inner.offsetMin = new Vector2(18, 18); inner.offsetMax = new Vector2(-18, -18);
        var innerImg = inner.gameObject.AddComponent<Image>(); innerImg.sprite = Design.CardEdge; innerImg.type = Image.Type.Sliced; innerImg.pixelsPerUnitMultiplier = 0.9f; innerImg.color = Accent; innerImg.raycastTarget = false;
        var word = Label("Word", stamp, display, "PURCHASED", 104, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(640, 150));
        word.enableAutoSizing = true; word.fontSizeMin = 60; word.fontSizeMax = 104; word.characterSpacing = 6f;
        stamp.gameObject.SetActive(false);

        // the wallet's step under a turning ring, and where the outcome goes once it is known
        var spinner = Rect("Spinner", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -214), new Vector2(64, 64));
        var spinImg = spinner.gameObject.AddComponent<Image>(); spinImg.sprite = Design.RingEdge; spinImg.color = Accent; spinImg.raycastTarget = false;
        spinImg.type = Image.Type.Filled; spinImg.fillMethod = Image.FillMethod.Radial360; spinImg.fillAmount = 0.72f;
        var step = Label("Step", card, body, "Getting today's price…", 36, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -290), new Vector2(1040, 56));
        step.enableAutoSizing = true; step.fontSizeMin = 26; step.fontSizeMax = 36;
        var result = Label("Result", card, display, "", 64, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -206), new Vector2(1040, 80));
        result.enableAutoSizing = true; result.fontSizeMin = 40; result.fontSizeMax = 64;
        var reason = Label("Reason", card, body, "", 32, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -272), new Vector2(1040, 52));
        reason.enableAutoSizing = true; reason.fontSizeMin = 22; reason.fontSizeMax = 32;

        var close = Button("Close", card, "DONE", Accent, Paper, 36, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(420, 100));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, sheet.Close);
        close.gameObject.SetActive(false);

        // the gray material for a picture that did not get bought
        const string grayPath = "Assets/_Game/Art/Materials/UI_Gray.mat";
        var gray = AssetDatabase.LoadAssetAtPath<Material>(grayPath);
        if (gray == null) { gray = new Material(Shader.Find("KartCup/UIGray")); AssetDatabase.CreateAsset(gray, grayPath); }

        // pictures by key: karts, drivers, item packs, gems
        var keys = new List<string>(); var sprites = new List<Object>();
        foreach (var id in KartSetup.Karts) { keys.Add("kart:" + id); sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(KartThumbs.Dir + id + ".png")); }
        var roster = AssetDatabase.LoadAssetAtPath<CharacterDatabase>("Assets/_Game/Data/Characters.asset");
        if (roster != null) foreach (var c in roster.characters.Where(c => c != null)) { keys.Add("driver:" + c.driverId); sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSetup.Dir + c.driverId + ".png")); }
        foreach (var p in Inventory.Packs) { keys.Add("item:" + p.id); sprites.Add(Design.Icon(Design.ItemGlyph(p.item))); }
        keys.Add("gems"); sprites.Add(Design.Icon("gem"));

        new SerializedObject(sheet).Do(s =>
        {
            s.FindProperty("group").objectReferenceValue = group;
            s.FindProperty("stampGroup").objectReferenceValue = stampGroup;
            s.FindProperty("card").objectReferenceValue = card;
            s.FindProperty("stamp").objectReferenceValue = stamp;
            s.FindProperty("spinner").objectReferenceValue = spinner;
            s.FindProperty("picture").objectReferenceValue = picImg;
            s.FindProperty("title").objectReferenceValue = title;
            s.FindProperty("price").objectReferenceValue = price;
            s.FindProperty("step").objectReferenceValue = step;
            s.FindProperty("result").objectReferenceValue = result;
            s.FindProperty("reason").objectReferenceValue = reason;
            s.FindProperty("closeLabel").objectReferenceValue = close.GetComponentInChildren<TMP_Text>(true);
            s.FindProperty("closeButton").objectReferenceValue = close.gameObject;
            s.FindProperty("gray").objectReferenceValue = gray;
            s.FindProperty("stampClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audio/SFX/stamp.wav");
            var k = s.FindProperty("pictureKeys"); k.arraySize = keys.Count;
            for (int i = 0; i < keys.Count; i++) k.GetArrayElementAtIndex(i).stringValue = keys[i];
            Fill(s.FindProperty("pictures"), sprites.ToArray());
            s.FindProperty("fallback").objectReferenceValue = IconSprite("kart");
        });
    }
}
