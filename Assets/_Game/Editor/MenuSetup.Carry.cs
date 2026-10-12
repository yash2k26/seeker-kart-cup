using System.Linq;
using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The pre-race picker (CarryPicker): its own canvas, saved as Resources/CarryPicker so every race scene can show it
// without carrying it. A card: what this is for, a tile per pack (its item, how many you hold, ×N once picked), and
// RACE WITH N ITEMS / NO ITEMS.
public static partial class MenuSetup
{
    const string CarryPickerPath = "Assets/_Game/Resources/CarryPicker.prefab";

    static void CarryPickerPrefab()
    {
        var go = new GameObject("CarryPicker", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4700;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(2400, 1080); scaler.matchWidthOrHeight = 1f;
        var group = go.GetComponent<CanvasGroup>();
        var picker = go.AddComponent<CarryPicker>();

        var back = Rect("Back", go.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(600, 600));
        back.gameObject.AddComponent<Image>().color = new Color(Deep.r, Deep.g, Deep.b, 0.86f);
        var card = Rect("Card", go.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660, 830));
        Design.AsCard(card, Panel);
        var title = Label("Title", card, display, "TAKE INTO THIS RACE", 64, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(1500, 80));
        title.enableAutoSizing = true; title.fontSizeMin = 40; title.fontSizeMax = 64;
        var hint = Label("Hint", card, body, "", 30, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(1540, 48));
        hint.enableAutoSizing = true; hint.fontSizeMin = 22; hint.fontSizeMax = 30;

        var packs = Inventory.Packs;
        var tiles = new Button[packs.Length]; var backs = new Image[packs.Length]; var icons = new Image[packs.Length];
        var counts = new TMP_Text[packs.Length]; var owned = new TMP_Text[packs.Length];
        for (int i = 0; i < packs.Length; i++)
        {
            var rt = Rect("Tile" + i, card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (packs.Length - 1) / 2f) * 252f, 20), new Vector2(228, 300));
            backs[i] = Design.AsCard(rt, new Color(0.16f, 0.12f, 0.36f));
            tiles[i] = rt.gameObject.AddComponent<Button>();
            rt.gameObject.AddComponent<PressFeel>();
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(tiles[i].onClick, picker.Tap, i);
            var icon = Rect("Icon", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-6, 40), new Vector2(100, 100));
            icons[i] = icon.gameObject.AddComponent<Image>(); icons[i].sprite = Design.Icon(Design.ItemGlyph(packs[i].item) ?? "trap"); icons[i].preserveAspect = true; icons[i].raycastTarget = false;
            var name = Label("Name", rt, strong, packs[i].item.ToUpperInvariant(), 28, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(210, 40));
            name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 28;
            owned[i] = Label("Owned", rt, body, "", 22, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -92), new Vector2(210, 32));
            counts[i] = Label("Count", rt, display, "", 48, Deep, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-18, -12), new Vector2(110, 60));
            counts[i].rectTransform.pivot = new Vector2(1, 1);
        }

        var none = Button("None", card, "NO ITEMS", Slate, Paper, 32, new Vector2(0.5f, 0), new Vector2(-340, 44), new Vector2(380, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(none.onClick, picker.None);
        var goBtn = Button("Go", card, "RACE WITH 3 ITEMS", Accent, Paper, 40, new Vector2(0.5f, 0), new Vector2(190, 44), new Vector2(640, 112));
        UnityEditor.Events.UnityEventTools.AddPersistentListener(goBtn.onClick, picker.Go);

        new SerializedObject(picker).Do(s =>
        {
            s.FindProperty("group").objectReferenceValue = group;
            s.FindProperty("card").objectReferenceValue = card;
            Fill(s.FindProperty("tiles"), tiles.Cast<Object>().ToArray());
            Fill(s.FindProperty("backs"), backs.Cast<Object>().ToArray());
            Fill(s.FindProperty("icons"), icons.Cast<Object>().ToArray());
            Fill(s.FindProperty("counts"), counts.Cast<Object>().ToArray());
            Fill(s.FindProperty("owned"), owned.Cast<Object>().ToArray());
            s.FindProperty("hint").objectReferenceValue = hint;
            s.FindProperty("goLabel").objectReferenceValue = goBtn.GetComponentInChildren<TMP_Text>(true);
            s.FindProperty("noneButton").objectReferenceValue = none.gameObject;
            s.FindProperty("idle").colorValue = new Color(0.16f, 0.12f, 0.36f);
            s.FindProperty("picked").colorValue = Accent;
            s.FindProperty("muted").colorValue = Muted;
        });
        PrefabUtility.SaveAsPrefabAsset(go, CarryPickerPath);
        Object.DestroyImmediate(go);
    }
}
