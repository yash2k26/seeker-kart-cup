using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// The title's hero (10 Oct 2026, Beach Buggy's hub): the chosen driver standing by the chosen kart, in its paint, on
    /// the plinth MenuMeet put in front of the street ("HomeStage"). Rebuilt every time the title shows, so a new pick,
    /// paint or purchase is on the doorstep when you come back.
    public sealed class HomeStage : MonoBehaviour
    {
        [SerializeField] CharacterCarousel carousel;
        [SerializeField] KartDatabase karts;
        [SerializeField] CharacterDatabase roster;
        [SerializeField] GameObject[] kartPrefabs = System.Array.Empty<GameObject>();
        GameObject kart, driver;

        void OnEnable() => Build();
        void OnDisable() => Clear();
        public void ShowInEditor() => Build();

        void Clear()
        {
            foreach (var go in new[] { kart, driver })
                if (go != null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            kart = driver = null;
        }

        void Build()
        {
            Clear();
            var backdrop = FindFirstObjectByType<MenuBackdrop>();
            var stage = backdrop != null && backdrop.Meet != null ? backdrop.Meet.Find("HomeStage") : null;
            if (stage == null || karts == null) return;
            var def = roster != null ? roster.Selected : null;
            string kartId = karts.EffectiveKartFor(def, allowTrial: false) ?? "";
            var prefab = kartPrefabs.FirstOrDefault(k => k != null && k.name.EndsWith(kartId)) ?? kartPrefabs.FirstOrDefault();
            if (prefab != null)
            {
                var paint = Paints.Find(Paints.SelectedId);
                var colour = paint != null ? paint.color : Paints.ColorFor(def, kartId);
                kart = CharacterCarousel.Mount(prefab, null, stage, colour, false);
                if (paint != null ? paint.gold : Paints.GoldOn(kartId)) CharacterDef.Paint(kart, colour, gold: true);
                // right of the plinth's centre, nose three-quarters to the camera; the driver stands at its left front wheel
                kart.transform.localPosition += new Vector3(0.9f, 0f, 0.3f);
                kart.transform.localRotation = Quaternion.Euler(0f, 226f, 0f);
                foreach (var r in kart.GetComponentsInChildren<Renderer>()) if (r.name != "BlobShadow") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (def != null && carousel != null && (driver = carousel.StandFor(def, stage)) != null)
            {
                driver.transform.localPosition += new Vector3(-1.5f, 0f, -0.6f);
                driver.transform.localRotation = Quaternion.Euler(0f, 195f, 0f);
                foreach (var r in driver.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var head = driver.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head");
                driver.AddComponent<MeetActor>().Bind(driver.GetComponent<Animation>(), head, System.Array.Empty<string>(), false);   // watches the camera, as the street does
            }
        }
    }
}
