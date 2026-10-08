using KartCup;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// The daily wheel (8 Oct 2026, Beach Buggy's "Daily Reward", without its ads): a tile on the title beside the chest,
// and the wheel over the title. Textures: tools/fx/feel_textures.py (fx_wheel, fx_pointer).
public static partial class MenuSetup
{
    static void DailyWheel(GameObject title)
    {
        var wheelSprite = FeelSetup.Sprite("fx_wheel"); var pointerSprite = FeelSetup.Sprite("fx_pointer");
        var spin = title.AddComponent<SpinWheel>();

        // the tile: beside the chest, the wheel's face and whether today's spin is waiting
        var tile = Rect("Spin", title.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-316, 40), new Vector2(220, 288));
        tile.pivot = new Vector2(1, 0);
        Design.AsTile(tile, Deep, 28f, out float lip);
        var button = tile.gameObject.AddComponent<Button>();
        var feel = tile.gameObject.AddComponent<PressFeel>();
        var face = Face("Spin", tile, lip);
        var icon = Rect("Wheel", face, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -98), new Vector2(150, 150));   // centred 98 down: inside the tile (Rect pivots at the middle)
        var iconImg = icon.gameObject.AddComponent<Image>(); iconImg.sprite = wheelSprite; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
        icon.gameObject.AddComponent<SlowTurn>();
        var line = Label("Line", face, display, "FREE SPIN", 30, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(210, 44));
        var dot = Rect("Ready", tile, new Vector2(1, 1), new Vector2(1, 1), new Vector2(14, 14), new Vector2(48, 48));
        dot.pivot = new Vector2(1, 1);
        Design.AsPill(dot, Design.Tiers[0]).raycastTarget = false;
        feel.Configure(Design.TileDown, face, lip * (Design.TileLip - 3f) / Design.TileLip);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, spin.Open);

        // the wheel, over the whole title: a dim that closes it, the wheel with its prizes, the pointer, the line, SPIN
        var overlay = Rect("SpinOverlay", title.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var dim = overlay.gameObject.AddComponent<Image>(); dim.color = new Color(Deep.r, Deep.g, Deep.b, 0.9f);
        var close = overlay.gameObject.AddComponent<Button>(); close.transition = Selectable.Transition.None;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, spin.Close);
        // the wheel on the left, the words and SPIN on the right: it all fits the 1080-high screen
        var stage = Rect("Stage", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1900, 900));
        Label("Title", stage, display, "DAILY SPIN", 76, Paper, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(480, 210), new Vector2(820, 96));
        Label("Sub", stage, body, "One free spin every day. Every slice is as likely.\nMiss a day and nothing is lost.", 30, Muted, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(480, 120), new Vector2(820, 90));
        var wheel = Rect("Wheel", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -20), new Vector2(720, 720));
        var wheelImg = wheel.gameObject.AddComponent<Image>(); wheelImg.sprite = wheelSprite; wheelImg.raycastTarget = false;
        var chestSprites = ChestSprites(false);
        int n = DailySpin.Wheel.Length; float R = 360f;
        for (int i = 0; i < n; i++)
        {
            var p = DailySpin.Wheel[i];
            float a = i * 360f / n, rad = a * Mathf.Deg2Rad;
            var slot = Rect("Prize" + i, wheel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
            slot.localRotation = Quaternion.Euler(0, 0, -a);                    // its up points out along the slice
            var ic = Rect("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, R * 0.63f), new Vector2(118, 118));
            var img = ic.gameObject.AddComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false;
            if (p.kind == DailySpin.Kind.Chest) img.sprite = chestSprites[(int)p.tier];
            else { img.sprite = IconSprite(p.glyph == "star" ? "star" : p.glyph); img.color = p.kind == DailySpin.Kind.Stars ? Accent : Paper; }
            var word = p.kind == DailySpin.Kind.Chest ? p.label.Replace(" CHEST", "") : p.kind == DailySpin.Kind.Stars ? $"{p.amount} STARS" : $"×{p.amount}";
            Label("Label", slot, display, word, 26, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, R * 0.38f), new Vector2(170, 34));
        }
        var pointer = Rect("Pointer", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -20 + 360 + 22), new Vector2(100, 100));
        var pImg = pointer.gameObject.AddComponent<Image>(); pImg.sprite = pointerSprite; pImg.raycastTarget = false;
        var result = Label("Result", stage, strong, "", 34, Paper, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(480, 0), new Vector2(820, 60));
        var spinBtn = Button("SpinButton", stage, "SPIN", Accent, Paper, 56, new Vector2(0.5f, 0.5f), new Vector2(330, -160), new Vector2(520, 136), icon: "play");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(spinBtn.onClick, spin.Spin);
        Shine(spinBtn);
        overlay.gameObject.SetActive(false);

        new SerializedObject(spin).Do(s =>
        {
            s.FindProperty("overlay").objectReferenceValue = overlay.gameObject;
            s.FindProperty("wheel").objectReferenceValue = wheel;
            s.FindProperty("result").objectReferenceValue = result;
            s.FindProperty("tileLine").objectReferenceValue = line;
            s.FindProperty("spinLabel").objectReferenceValue = spinBtn.GetComponentInChildren<TMP_Text>();
            s.FindProperty("spinButton").objectReferenceValue = spinBtn;
            s.FindProperty("readyDot").objectReferenceValue = dot.gameObject;
        });
    }
}
