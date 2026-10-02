using MergeLegion.Core;
using MergeLegion.Save;
using UnityEngine;

namespace MergeLegion.Audio
{
    /// <summary>Device vibration with a user toggle (Settings). Light/medium/heavy map to pulse lengths on Android.</summary>
    public static class Haptics
    {
        private static bool Enabled => !ServiceLocator.TryGet<SaveService>(out var s) || s.Data.settings.hapticsOn;

        public static void Light() => Pulse(12);
        public static void Medium() => Pulse(25);
        public static void Heavy() => Pulse(55);

        private static void Pulse(long ms)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidPulse(ms);
#elif UNITY_IOS && !UNITY_EDITOR
            if (ms >= 25) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;

        private static void AndroidPulse(long ms)
        {
            if (_vibrator == null)
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            if (_vibrator == null) { if (ms >= 25) Handheld.Vibrate(); return; }
            _vibrator.Call("vibrate", ms);
        }
#endif
    }
}
