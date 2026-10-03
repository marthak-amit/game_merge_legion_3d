#if BLOCKBLOOM_SOCIAL
using System;
using System.Collections.Generic;
using BlockBloom.Services;
using UnityEngine;
using UnityEngine.SocialPlatforms;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace BlockBloom.Sdk
{
    /// <summary>
    /// Google Play Games (Android) and Game Center (iOS) leaderboards. Scores are mirrored from the main
    /// leaderboard service (UGS or mock), so the in-game boards and the platform boards stay in sync.
    /// Platform leaderboard ids come from Resources/Config/sdk_keys.json.
    /// </summary>
    public static class SocialRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Activate();
#endif
            PlatformServiceOverrides.LeaderboardDecorators.Add(inner => new PlatformLeaderboardMirror(inner));
        }
    }

    public sealed class PlatformLeaderboardMirror : ILeaderboardService
    {
        private readonly ILeaderboardService _inner;
        private bool _signedIn;
        private bool _authenticating;

        public PlatformLeaderboardMirror(ILeaderboardService inner) { _inner = inner; }

        private void EnsureSignedIn(Action<bool> done)
        {
#if UNITY_EDITOR
            done(false);
#else
            if (_signedIn) { done(true); return; }
            if (_authenticating) { done(false); return; }
            _authenticating = true;
            Social.localUser.Authenticate(ok => { _authenticating = false; _signedIn = ok; done(ok); });
#endif
        }

        public void SubmitScore(string boardId, long score, Action<bool> onComplete)
        {
            _inner.SubmitScore(boardId, score, onComplete);
            string id = SdkKeys.Instance.LeaderboardId(boardId);
            if (string.IsNullOrEmpty(id)) return;
            EnsureSignedIn(ok =>
            {
                if (ok) Social.ReportScore(score, id, success => { });
            });
        }

        public void GetTop(string boardId, int count, Action<IReadOnlyList<LeaderboardEntry>> onComplete) => _inner.GetTop(boardId, count, onComplete);
        public void GetPlayerEntry(string boardId, Action<LeaderboardEntry> onComplete) => _inner.GetPlayerEntry(boardId, onComplete);

        /// <summary>Opens the native leaderboard overlay (optional button in Settings / Leaderboards).</summary>
        public static void ShowNative(string boardId)
        {
            string id = SdkKeys.Instance.LeaderboardId(boardId);
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.Instance.ShowLeaderboardUI(id);
#elif UNITY_IOS && !UNITY_EDITOR
            Social.ShowLeaderboardUI();
#endif
        }
    }
}
#endif
