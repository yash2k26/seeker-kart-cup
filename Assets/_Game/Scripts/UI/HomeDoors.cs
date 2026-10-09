using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The title's DRIVER and KART doors wear what is chosen (Beach Buggy's Switch Driver / Switch Car): the driver's
    /// portrait and the kart's thumbnail, each with a swap badge MenuSetup put in the corner. Refreshed when the title shows.
    public sealed class HomeDoors : MonoBehaviour
    {
        [SerializeField] CharacterDatabase roster;
        [SerializeField] KartDatabase karts;
        [SerializeField] Image driverImage, kartImage;
        [SerializeField] Sprite driverFallback, kartFallback;
        [SerializeField] string[] portraitIds = System.Array.Empty<string>();
        [SerializeField] Sprite[] portraits = System.Array.Empty<Sprite>();
        [SerializeField] string[] kartIds = System.Array.Empty<string>();
        [SerializeField] Sprite[] thumbs = System.Array.Empty<Sprite>();

        void OnEnable() => Refresh();
        public void ShowInEditor() => Refresh();

        void Refresh()
        {
            var def = roster != null ? roster.Selected : null;
            int i = def != null ? System.Array.IndexOf(portraitIds, def.driverId) : -1;
            var face = i >= 0 && i < portraits.Length ? portraits[i] : null;
            if (driverImage != null) { driverImage.sprite = face != null ? face : driverFallback; driverImage.color = Color.white; }
            string kartId = karts != null ? karts.EffectiveKartFor(def, allowTrial: false) : null;
            int k = kartId != null ? System.Array.IndexOf(kartIds, kartId) : -1;
            var thumb = k >= 0 && k < thumbs.Length ? thumbs[k] : null;
            if (kartImage != null) { kartImage.sprite = thumb != null ? thumb : kartFallback; kartImage.color = Color.white; }
        }
    }
}
