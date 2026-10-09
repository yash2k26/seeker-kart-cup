using TMPro;
using UnityEngine;

namespace KartCup
{
    /// First launch: connect the Seeker wallet, or play as a guest. One screen, once (10 Oct 2026). On a Seeker the
    /// wallet is one tap and a fingerprint (Seed Vault), so it comes first; it can always be skipped, because progress
    /// lives on the phone and a hung or missing wallet must never stop anyone reaching a race. Buying, the Weekly and
    /// paid rooms still ask for the wallet at the moment they need it.
    public sealed class ConnectPanel : MonoBehaviour
    {
        const string WelcomedKey = "kc.welcomed", StarterKey = "kc.starter.gems";
        public const int StarterGems = 100;   // a judge's worth: Mert or Akshay, recruited on the first visit to the driver screen
        [SerializeField] TMP_Text status, connectLabel;
        bool busy;

        /// Show this screen at launch? Not once it has been answered, and not for a phone that already has a session.
        public static bool Due => PlayerPrefs.GetInt(WelcomedKey, 0) == 0 && !KartApi.HasSession;
        static void Welcomed() { PlayerPrefs.SetInt(WelcomedKey, 1); PlayerPrefs.Save(); }

        void OnEnable() { busy = false; if (status != null) status.text = ""; if (connectLabel != null) connectLabel.text = "CONNECT"; }

        public async void Connect()
        {
            if (busy) return;
            var w = WalletFlow.Instance;
            if (w == null) { Guest(); return; }
            busy = true;
            if (connectLabel != null) connectLabel.text = "OPENING WALLET…";
            if (status != null) status.text = "";
            bool ok = false;
            try { ok = await w.LoginAsync(); } catch (System.Exception e) { Debug.LogWarning("[connect] " + e.Message); }
            busy = false;
            if (this == null) return;
            if (!ok)
            {
                if (connectLabel != null) connectLabel.text = "TRY AGAIN";
                if (status != null) status.text = string.IsNullOrEmpty(w.LastStatus) ? "Couldn't connect. Try again, or play as a guest." : w.LastStatus + "  ·  or play as a guest";
                return;
            }
            Welcomed();
            if (PlayerPrefs.GetInt(StarterKey, 0) == 0) { PlayerPrefs.SetInt(StarterKey, 1); PlayerPrefs.Save(); Gems.Earn(StarterGems, "connect"); }
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm();
            KartApi.Event("onboard", new { connected = true });
            ScreenFlow.Instance?.ShowFront();
        }

        /// The small link under CONNECT, and Android back on this screen.
        public void Guest()
        {
            if (busy) return;
            Welcomed();
            KartApi.Event("onboard", new { connected = false });
            ScreenFlow.Instance?.ShowFront();
        }
    }
}
