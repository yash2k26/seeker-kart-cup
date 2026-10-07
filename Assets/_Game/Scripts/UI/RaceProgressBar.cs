using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// Beach Buggy's race bar across the top: START to FINISH, every racer's face riding along it by how far round the
    /// race they are, yours bigger, ringed in gold and drawn over the rest. Who is close behind reads at a glance,
    /// which a minimap does not tell you.
    public sealed class RaceProgressBar : MonoBehaviour
    {
        [SerializeField] RectTransform bar;                 // the line; faces are its children
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Sprite[] portraits = System.Array.Empty<Sprite>();
        [SerializeField] string[] portraitIds = System.Array.Empty<string>();
        [SerializeField] Sprite dot;
        [SerializeField] float faceSize = 54f, playerSize = 72f;
        [SerializeField] Color ring = new(1f, 0.694f, 0.149f), rivalRing = new(0.05f, 0.012f, 0.15f, 0.92f);

        readonly Dictionary<KartProgress, RectTransform> faces = new();
        RectTransform mine;

        void LateUpdate()
        {
            var race = RaceManager.Instance;
            if (race == null || bar == null) return;
            int gates = Mathf.Max(1, race.checkpoints.Length);
            float total = Mathf.Max(1, race.totalLaps * gates), w = bar.rect.width;
            foreach (var r in race.racers)
            {
                if (r == null) continue;
                if (!faces.TryGetValue(r, out var f)) { f = Make(r); faces[r] = f; if (r.isPlayer) mine = f; }
                float k = r.finished ? 1f : Mathf.Clamp01(r.score / total);
                var at = new Vector2(Mathf.Lerp(-w * 0.5f, w * 0.5f, k), 0f);
                f.anchoredPosition = Vector2.Lerp(f.anchoredPosition, at, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            }
            if (mine != null) mine.SetAsLastSibling();
        }

        RectTransform Make(KartProgress r)
        {
            float size = r.isPlayer ? playerSize : faceSize;
            var rt = new GameObject(r.isPlayer ? "You" : "Rival", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(bar, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * (size + 8f);
            var back = rt.gameObject.AddComponent<Image>();
            back.sprite = dot; back.color = r.isPlayer ? ring : rivalRing; back.raycastTarget = false;
            var def = roster != null && r.characterIndex >= 0 ? roster.At(r.characterIndex) : null;
            Sprite face = null;
            if (def != null) for (int i = 0; i < portraitIds.Length && i < portraits.Length; i++) if (portraitIds[i] == def.driverId) face = portraits[i];
            var img = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
            img.rectTransform.SetParent(rt, false);
            img.rectTransform.sizeDelta = Vector2.one * size;
            img.raycastTarget = false; img.preserveAspect = true;
            if (face != null) { img.sprite = face; img.color = Color.white; }
            else { img.sprite = dot; img.color = def != null && def.tint != Color.white ? def.tint : new Color(0.58f, 0.68f, 0.74f); img.rectTransform.sizeDelta = Vector2.one * size * 0.6f; }
            return rt;
        }
    }
}
