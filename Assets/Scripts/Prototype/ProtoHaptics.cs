using UnityEngine;

// Short haptic pulses for the prototype. Kept separate from Vibration.cs, which is gated on DataManager
// (not present in the prototype scene).
public static class ProtoHaptics
{
#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject vibrator;

    public static void Pulse(long milliseconds)
    {
        try
        {
            if (vibrator == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            vibrator?.Call("vibrate", milliseconds);
        }
        catch (System.Exception) { }
    }
#else
    public static void Pulse(long milliseconds) { }
#endif
}
