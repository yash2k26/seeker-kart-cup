using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The circuit in the corner with everyone on it: rivals as dots in their colours, you as your driver's face,
    /// sliding along the line as you drive. The map picture and the world-to-map fit come from the track builder.
    public sealed class Minimap : MonoBehaviour
    {
        [SerializeField] RectTransform map;               // the picture; markers are its children
        [SerializeField] Vector2 worldMin;                 // world XZ of the picture's bottom-left
        [SerializeField] float worldSize = 1f;             // world metres across the (square) picture
        [SerializeField] CharacterDatabase roster;
        [SerializeField] Sprite[] portraits = System.Array.Empty<Sprite>();   // by driverId, from PortraitSetup
        [SerializeField] string[] portraitIds = System.Array.Empty<string>();
        [SerializeField] Sprite dot;
        [SerializeField] float dotSize = 22f, faceSize = 64f;

        readonly Dictionary<KartProgress, RectTransform> markers = new();
        RaceManager race;

        void Start() { race = RaceManager.Instance; }

        void LateUpdate()
        {
            if (race == null || map == null) return;
            foreach (var r in race.racers)
            {
                if (r == null) continue;
                if (!markers.TryGetValue(r, out var m)) { m = Make(r); markers[r] = m; }
                var p = r.transform.position;
                float u = Mathf.Clamp01((p.x - worldMin.x) / worldSize), v = Mathf.Clamp01((p.z - worldMin.y) / worldSize);
                m.anchoredPosition = new Vector2((u - 0.5f) * map.rect.width, (v - 0.5f) * map.rect.height);
                if (r.isPlayer) m.SetAsLastSibling();   // the face draws over the dots
            }
        }

        RectTransform Make(KartProgress r)
        {
            var go = new GameObject(r.isPlayer ? "You" : "Rival", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(map, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var def = roster != null && r.characterIndex >= 0 ? roster.At(r.characterIndex) : null;
            Sprite face = null;
            if (r.isPlayer && def != null)
                for (int i = 0; i < portraitIds.Length && i < portraits.Length; i++) if (portraitIds[i] == def.driverId) face = portraits[i];
            if (face != null)
            {
                // a dark ring behind the face so it reads on the pale road line
                var ring = new GameObject("Ring", typeof(RectTransform)).GetComponent<RectTransform>();
                ring.SetParent(rt, false); ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
                ring.sizeDelta = Vector2.one * (faceSize + 10f);
                var ringImg = ring.gameObject.AddComponent<Image>();
                ringImg.sprite = dot; ringImg.color = new Color(0.04f, 0.08f, 0.11f, 0.9f); ringImg.raycastTarget = false;
                ring.SetAsFirstSibling();
                img.sprite = face; img.color = Color.white; img.preserveAspect = true;
                rt.sizeDelta = Vector2.one * faceSize;
            }
            else
            {
                img.sprite = dot;
                img.color = r.isPlayer ? Design_Ember : (def != null && def.tint != Color.white ? def.tint : new Color(0.58f, 0.68f, 0.74f));
                rt.sizeDelta = Vector2.one * (r.isPlayer ? dotSize * 1.5f : dotSize);
            }
            return rt;
        }

        static readonly Color Design_Ember = new(1f, 0.694f, 0.149f);   // Design.Accent
    }
}
