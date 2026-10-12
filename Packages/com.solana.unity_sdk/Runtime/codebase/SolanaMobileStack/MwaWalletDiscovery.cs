using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Solana.Unity.SDK
{
    /// <summary>
    /// Reads the cached wallet package for targeted MWA associations.
    /// </summary>
    /// <remarks>
    /// On first connect the OS wallet chooser captures the package via
    /// <see cref="MwaNativeChooser"/>; subsequent calls read it from
    /// <see cref="IMwaWalletSelectionCache"/>.
    /// </remarks>
    public static class MwaWalletDiscovery
    {
        /// <summary>
        /// Returns the cached wallet package to target, or <c>null</c> when none
        /// is cached — in which case the caller should fire an untargeted intent
        /// so the OS shows its wallet chooser.
        /// </summary>
        public static async Task<string> ResolveWalletPackage(IMwaWalletSelectionCache cache)
        {
            if (cache == null)
                return null;

            string cached;
            try
            {
                cached = await cache.GetSelectedWalletPackage();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MWA][Discovery] Failed to read cached wallet package: {e.Message}. Falling back to chooser.");
                return null;
            }
            if (!string.IsNullOrEmpty(cached))
            {
                Debug.Log($"[MWA][Discovery] Using cached wallet: {cached}");
                return cached;
            }

            Debug.Log("[MWA][Discovery] No cached wallet package; intent will be untargeted (OS chooser).");
            return null;
        }

        /// <summary>
        /// Whether <paramref name="package"/> (or, with null, any app on the phone) takes a solana-wallet association
        /// intent. A wallet cached from an earlier sign-in may since have been uninstalled: launching at it threw, the
        /// chooser never showed, and the sign-in button looked dead (Kartel, 12 Oct 2026). Needs the solana-wallet
        /// &lt;queries&gt; entry in the manifest (Android 11+ package visibility). Always true off Android.
        /// </summary>
        public static bool CanHandle(string package)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                using var intent = LocalAssociationIntentCreator.CreateAssociationIntent("probe", 1, package);
                using var info = pm.Call<AndroidJavaObject>("resolveActivity", intent, 0);
                return info != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MWA][Discovery] could not ask the package manager ({e.Message}); assuming it can");
                return true;
            }
#else
            return true;
#endif
        }
    }

    /// <summary>No app on the phone takes a Mobile Wallet Adapter request: there is no Solana wallet to open.</summary>
    public sealed class MwaNoWalletException : System.Exception
    {
        public MwaNoWalletException() : base("[MWA] No Solana wallet app on this phone") { }
    }
}
