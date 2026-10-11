using PrimeTween;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The fuel tank as a chip (the hub, the series header, the event card): "5/6" and, while a can is on its way,
    /// "+1 IN 8:32". A can spent pops "-1" off it and the chip jolts, so starting a race reads 5 -> 4 (the user, after
    /// Beach Buggy's tickets); a can back pops "+1". Tapping it opens FuelPanel (wired in MenuSetup).
    public sealed class FuelChip : MonoBehaviour
    {
        [SerializeField] TMP_Text count, timer, pop;
        [SerializeField] Color normal = Color.white, empty = new(1f, 0.42f, 0.36f);
        float next;

        void OnEnable() { Fuel.Changed += OnChanged; if (pop != null) pop.gameObject.SetActive(false); Refresh(); }
        void OnDisable() => Fuel.Changed -= OnChanged;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            Refresh();
        }

        void Refresh()
        {
            int c = Fuel.Count;
            if (count != null) { count.text = $"{c}/{Fuel.Max}"; count.color = c == 0 ? empty : normal; }
            if (timer != null) timer.text = c >= Fuel.Max ? "FULL TANK" : $"+1 IN {Fuel.NextIn()}";
        }

        void OnChanged(int before, int after)
        {
            Refresh();
            if (!isActiveAndEnabled || before == after) return;
            Tween.PunchScale(transform, new Vector3(0.18f, 0.18f, 0f), 0.35f, useUnscaledTime: true);
            if (pop == null) return;
            pop.text = after < before ? $"-{before - after}" : $"+{after - before}";
            pop.color = after < before ? empty : new Color(0.55f, 1f, 0.6f);
            var rt = pop.rectTransform; var home = rt.anchoredPosition;
            pop.gameObject.SetActive(true); pop.alpha = 1f;
            Tween.UIAnchoredPositionY(rt, home.y + 70f, 0.7f, Ease.OutCubic, useUnscaledTime: true)
                 .OnComplete(() => { if (pop != null) { pop.gameObject.SetActive(false); rt.anchoredPosition = home; } });
            Tween.Alpha(pop, 0f, 0.7f, Ease.InQuad, startDelay: 0.15f, useUnscaledTime: true);
        }

        public void Open() => FuelPanel.Open();
        public void ShowInEditor() => Refresh();
    }
}
