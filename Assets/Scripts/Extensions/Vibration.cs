using UnityEngine;
using System.Collections;

public static class Vibration
{

#if UNITY_ANDROID && !UNITY_EDITOR
    public static AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    public static AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    public static AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
#else
    public static AndroidJavaClass unityPlayer;
    public static AndroidJavaObject currentActivity;
    public static AndroidJavaObject vibrator;
#endif

    public static void Vibrate()
    {
        Debug.Log("Called Continuous vibrate");
        if (DataManager.vibration == 1)
        {
            if (isAndroid())
                vibrator.Call("vibrate");
            else
                Handheld.Vibrate();
        }
    }


    public static void Vibrate(long milliseconds)
    {
        if (DataManager.vibration == 1)
        {
            if (isAndroid())
                vibrator.Call("vibrate", milliseconds);
            else
                Handheld.Vibrate();
        }
    }

    public static void Vibrate(long[] pattern, int repeat)
    {
        if (DataManager.vibration == 1)
        {
            if (isAndroid())
                vibrator.Call("vibrate", pattern, repeat);
            else
                Handheld.Vibrate();
        }
    }

    public static bool HasVibrator()
    {
        return isAndroid();
    }

    public static void Cancel()
    {
        Debug.Log("cancelled Continuous vibrate");
        if (isAndroid())
            vibrator.Call("cancel");
    }

    private static bool isAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
	return true;
#else
        return false;
#endif
    }
}