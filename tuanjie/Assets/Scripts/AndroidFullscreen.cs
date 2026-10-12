// Android 沉浸式全屏 / 横屏
using UnityEngine;

public static class AndroidFullscreen
{
    public static void Apply()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.fullScreen = true;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
            {
                // LAYOUT_STABLE|LAYOUT_HIDE_NAVIGATION|LAYOUT_FULLSCREEN|HIDE_NAVIGATION|FULLSCREEN|IMMERSIVE_STICKY
                decor.Call("setSystemUiVisibility", 1 | 2 | 4 | 8 | 16 | 4096);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("沉浸式全屏设置失败: " + e.Message);
        }
#endif
    }
}
