using UnityEngine;

namespace KartCup
{
    /// The Android share sheet, for room invites. One intent, no plugin. On other platforms it copies to the clipboard.
    public static class Share
    {
        public static void Text(string text, string title = "Invite to a race")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using var intent = new AndroidJavaObject("android.content.Intent");
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title);
                activity.Call("startActivity", chooser);
                return;
            }
            catch (System.Exception e) { Debug.LogWarning("[share] " + e.Message); }
#endif
            GUIUtility.systemCopyBuffer = text;
        }
    }
}
