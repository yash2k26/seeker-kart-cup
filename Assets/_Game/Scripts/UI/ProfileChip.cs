using PrimeTween;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// The title's top-right corner: who you are. SIGN IN until a wallet is connected, then your .skr name or a short
    /// address. Whatever the wallet says while it works ("Opening wallet…", "Cancelled") shows under the chip for a
    /// moment, so a tap is never met with silence.
    public sealed class ProfileChip : MonoBehaviour
    {
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text toast;
        [SerializeField] CanvasGroup toastGroup;

        string seen;
        float hideAt;

        void OnEnable()
        {
            WalletFlow.ProfileChanged += OnProfile;
            OnProfile(WalletFlow.Instance != null ? WalletFlow.Instance.Profile : null);
            seen = WalletFlow.Instance != null ? WalletFlow.Instance.LastStatus : null;
            if (toastGroup != null) toastGroup.alpha = 0f;
        }

        void OnDisable() => WalletFlow.ProfileChanged -= OnProfile;

        /// The chip itself: sign in, or say who is signed in.
        public void Tap()
        {
            var wallet = WalletFlow.Instance;
            if (wallet == null) return;
            if (wallet.SignedIn && wallet.Profile != null) Say("Signed in as " + Name(wallet.Profile));
            else wallet.Login();
        }

        void Update()
        {
            var wallet = WalletFlow.Instance;
            var status = wallet != null ? wallet.LastStatus : null;
            if (status != seen) { seen = status; if (!string.IsNullOrEmpty(status)) Say(status); }
            if (toastGroup != null && toastGroup.alpha > 0f && Time.unscaledTime > hideAt && Tween.GetTweensCount(toastGroup) == 0)
                Tween.Alpha(toastGroup, 0f, 0.3f, useUnscaledTime: true);
        }

        void Say(string s)
        {
            if (toast == null || toastGroup == null) return;
            toast.text = s;
            hideAt = Time.unscaledTime + 2.6f;
            Tween.StopAll(toastGroup);
            Tween.Alpha(toastGroup, 1f, 0.15f, useUnscaledTime: true);
        }

        void OnProfile(KartApi.Profile p)
        {
            if (nameLabel != null) nameLabel.text = p == null ? "SIGN IN" : Name(p).ToUpperInvariant();
        }

        static string Name(KartApi.Profile p) => !string.IsNullOrEmpty(p.skrName) ? p.skrName : WalletFlow.Short(p.wallet) ?? "";
    }
}
