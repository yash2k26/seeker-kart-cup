using UnityEngine;

namespace KartCup
{
    /// Android haptics: VibrationEffect.Composition primitives (API 31+) → predefined effects (29) → one-shot (26).
    /// Effects are built once; each Play is one JNI call. Rate-limited to 25/s. ~0.1 mAh per race.
    public static class Haptics
    {
        public enum Kind { Tick, Pickup, TierUp, Boost, Hit, Land, Win }
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vib, attrs; static AndroidJavaObject[] fx; static float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            try
            {
                int sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                var act = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
                vib = sdk >= 31
                    ? act.Call<AndroidJavaObject>("getSystemService", "vibrator_manager").Call<AndroidJavaObject>("getDefaultVibrator")
                    : act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (sdk >= 33)
                {
                    var VA = new AndroidJavaClass("android.os.VibrationAttributes");
                    attrs = VA.CallStatic<AndroidJavaObject>("createForUsage", VA.GetStatic<int>("USAGE_MEDIA"));
                }
                var VE = new AndroidJavaClass("android.os.VibrationEffect");
                bool comp = sdk >= 31 && vib.Call<bool>("areAllPrimitivesSupported", new[] { 0, 1, 3, 4, 6, 7 });
                Debug.Log($"[haptics] sdk={sdk} composition={comp} amplitude={vib.Call<bool>("hasAmplitudeControl")}");

                AndroidJavaObject C(params (int id, float s, int d)[] p)
                {
                    var b = VE.CallStatic<AndroidJavaObject>("startComposition");
                    foreach (var x in p) b = b.Call<AndroidJavaObject>("addPrimitive", x.id, x.s, x.d);
                    return b.Call<AndroidJavaObject>("compose");
                }
                AndroidJavaObject P(int e) => VE.CallStatic<AndroidJavaObject>("createPredefined", e);
                AndroidJavaObject O(long ms, int a) => VE.CallStatic<AndroidJavaObject>("createOneShot", ms, a);

                // Kind order: Tick, Pickup, TierUp, Boost, Hit, Land, Win
                fx = comp ? new[] { C((6, .5f, 0)), C((0, .6f, 0)), C((3, .5f, 0), (0, 1f, 60)), C((4, .8f, 0)), C((1, 1f, 0), (0, .6f, 90), (7, .4f, 90)), C((1, 1f, 0)), C((0, .4f, 0), (0, .7f, 100), (0, 1f, 100)) }
                   : sdk >= 29 ? new[] { P(2), P(0), P(1), P(5), P(5), P(5), P(1) }
                   : new[] { O(12, 90), O(18, 140), O(15, 200), O(40, 180), O(60, 255), O(25, 255), O(15, 200) };
            }
            catch (System.Exception e) { Debug.LogWarning("[haptics] unavailable: " + e.Message); vib = null; }
        }

        public static void Play(Kind k)
        {
            if (!Enabled || vib == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.04f;
            if (attrs != null) vib.Call("vibrate", fx[(int)k], attrs); else vib.Call("vibrate", fx[(int)k]);
        }
#else
        public static void Play(Kind k) { }
        static void KeepPermission() { Handheld.Vibrate(); } // ponytail: forces VIBRATE into the manifest
#endif
    }
}
