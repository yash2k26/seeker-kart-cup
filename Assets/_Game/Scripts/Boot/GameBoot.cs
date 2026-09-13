using UnityEngine;

namespace KartCup
{
    /// Runs once, first scene. Frame pacing, device diagnostics for the gate weekend, SDK auto-behaviour off.
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBoot : MonoBehaviour
    {
        [SerializeField] int targetFps = 60;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = targetFps;          // exact divisor of 120 Hz; Swappy asks the panel for 60 Hz
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;                   // Android ignores it anyway

            // The SDK fires GetTokenAccountsByOwner + Metaplex metadata for every NFT on connect unless this is off.
            Solana.Unity.SDK.Web3.AutoLoadNfts = false;

            Debug.Log($"[boot] {SystemInfo.deviceModel} · {SystemInfo.graphicsDeviceType} · " +
                      $"msaaAutoResolve={SystemInfo.supportsMultisampleAutoResolve} · " +
                      $"screen={Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio.value:F0}Hz · " +
                      $"dpi={Screen.dpi:F0} · msaa={QualitySettings.antiAliasing}");
        }
    }
}
