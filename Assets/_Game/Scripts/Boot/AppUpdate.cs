using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace KartCup
{
    /// A shared APK has no store to update it. On launch this asks the download host which build is newest
    /// (version.json beside the APK, written by tools/publish-apk.sh); if it is newer than the build installed, the
    /// title shows NEW VERSION, and a tap opens the APK's link: the browser downloads it and Android installs it over
    /// this one (same signing key, same package). Offline, or already newest: nothing shows.
    public sealed class AppUpdate : MonoBehaviour
    {
        /// Where shared builds update from (the public R2 bucket "kartel" tools/publish-apk.sh uploads to). Empty: no check.
        public const string Manifest = "https://pub-b5f53235900741b1ac8db6a89c249e73.r2.dev/version.json";

        [SerializeField] GameObject banner;
        [SerializeField] TMP_Text label;

        [System.Serializable] class Info { public int versionCode; public string version, url; }

        string url;

        async void Start()
        {
            if (banner != null) banner.SetActive(false);
            int mine = InstalledVersionCode();
            if (mine <= 0 || string.IsNullOrEmpty(Manifest)) return;
            using var req = UnityWebRequest.Get(Manifest + "?v=" + mine);      // a query per build: no stale cached copy
            req.timeout = 8;
            await req.SendWebRequest();
            if (this == null || req.result != UnityWebRequest.Result.Success) return;
            Info info;
            try { info = JsonUtility.FromJson<Info>(req.downloadHandler.text); } catch { return; }
            if (info == null || info.versionCode <= mine || string.IsNullOrEmpty(info.url)) return;
            url = info.url;
            if (label != null) label.text = "NEW VERSION  ·  TAP TO UPDATE";
            if (banner == null) return;
            banner.SetActive(true);
            banner.transform.localScale = Vector3.one * 0.7f;
            Tween.Scale(banner.transform, 1f, 0.35f, Ease.OutBack, useUnscaledTime: true);
        }

        /// The banner: download the new build in the browser (Android offers to install it when it lands).
        public void Open() { if (!string.IsNullOrEmpty(url)) Application.OpenURL(url); }

        /// This install's Android versionCode (0 in the editor, or if Android will not say).
        static int InstalledVersionCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var pm = activity.Call<AndroidJavaObject>("getPackageManager");
                using var info = pm.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0);
                return info.Get<int>("versionCode");              // deprecated at API 28, still filled in; minSdk is 26
            }
            catch (System.Exception e) { Debug.LogWarning("[update] version unknown: " + e.Message); return 0; }
#else
            return 0;
#endif
        }
    }
}
