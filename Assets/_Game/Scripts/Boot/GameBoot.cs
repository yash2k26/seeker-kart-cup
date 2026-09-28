using UnityEngine;

namespace KartCup
{
    /// Runs once, first scene. Frame pacing, device diagnostics for the gate weekend, SDK auto-behaviour off.
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBoot : MonoBehaviour
    {
        [SerializeField] int targetFps = 60;

        static GameBoot instance;

        void Awake()
        {
            // Destroy the duplicate COMPONENT, never its GameObject: in the race scene that object also carries HitStop,
            // and without HitStop's Update nothing ever restores timeScale after a hit and the game crawls at 5% speed.
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = targetFps;          // exact divisor of 120 Hz; Swappy asks the panel for 60 Hz
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Settings.Apply();                                 // haptics off stays off across a restart
            Cup.Restore();                                    // a cup half-run yesterday is still yours to finish
            QualitySettings.vSyncCount = 0;                   // Android ignores it anyway

            // The SDK fires GetTokenAccountsByOwner + Metaplex metadata for every NFT on connect unless this is off.
            Solana.Unity.SDK.Web3.AutoLoadNfts = false;

            Debug.Log($"[boot] input: keyboard={(UnityEngine.InputSystem.Keyboard.current != null)} devices=[{string.Join(" ", System.Linq.Enumerable.Select(UnityEngine.InputSystem.InputSystem.devices, d => d.GetType().Name))}]");
            Debug.Log($"[boot] {SystemInfo.deviceModel} · {SystemInfo.graphicsDeviceType} · " +
                      $"msaaAutoResolve={SystemInfo.supportsMultisampleAutoResolve} · " +
                      $"screen={Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio.value:F0}Hz · " +
                      $"dpi={Screen.dpi:F0} · msaa={(UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset ? UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.msaaSampleCount : 0)}");   // URP owns MSAA; QualitySettings.antiAliasing reads 0
        }
    }
}
