#if MERGELEGION_NOTIFICATIONS
using System;
using MergeLegion.Services;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace MergeLegion.Sdk
{
    /// <summary>Local notifications (offline coins, chest, daily reward, event) via the Unity Mobile Notifications package.</summary>
    public static class NotificationsRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => PlatformServiceOverrides.Push = () => new MobileNotificationsPush();
    }

    public sealed class MobileNotificationsPush : IPushService
    {
        private const string Channel = "merge_legion_default";

        public MobileNotificationsPush()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = Channel,
                Name = "Merge Legion",
                Importance = Importance.Default,
                Description = "Chests, castle coins and rewards"
            });
#endif
        }

        public void RequestPermission(Action<bool> onComplete)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android 13+ needs the runtime permission
            if (!Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
            onComplete?.Invoke(true);
#elif UNITY_IOS && !UNITY_EDITOR
            var auth = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true);
            var wait = new GameObject("[NotifPermission]").AddComponent<IosPermissionWaiter>();
            wait.Begin(auth, onComplete);
#else
            onComplete?.Invoke(true);
#endif
        }

        public void ScheduleLocal(string id, string title, string body, DateTime fireAtUtc)
        {
            if (fireAtUtc <= DateTime.UtcNow) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            var n = new AndroidNotification { Title = title, Text = body, FireTime = fireAtUtc.ToLocalTime() };
            AndroidNotificationCenter.SendNotificationWithExplicitID(n, Channel, Math.Abs(id.GetHashCode()));
#elif UNITY_IOS && !UNITY_EDITOR
            var trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = fireAtUtc - DateTime.UtcNow, Repeats = false };
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = id, Title = title, Body = body, ShowInForeground = false, Trigger = trigger
            });
#endif
        }

        public void CancelLocal(string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelScheduledNotification(Math.Abs(id.GetHashCode()));
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveScheduledNotification(id);
#endif
        }

        public void CancelAllLocal()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelAllScheduledNotifications();
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
#endif
        }

        public string GetPushToken()
        {
#if MERGELEGION_FIREBASE
            return PushTokenHolder.Token;
#else
            return "";
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private sealed class IosPermissionWaiter : MonoBehaviour
        {
            public void Begin(AuthorizationRequest request, Action<bool> done) { StartCoroutine(Wait(request, done)); }

            private System.Collections.IEnumerator Wait(AuthorizationRequest request, Action<bool> done)
            {
                while (!request.IsFinished) yield return null;
                done?.Invoke(request.Granted);
                Destroy(gameObject);
            }
        }
#endif
    }
}
#endif
