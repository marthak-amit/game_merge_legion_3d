#if MERGELEGION_UGS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MergeLegion.Data;
using MergeLegion.Meta.Arena;
using MergeLegion.Services;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace MergeLegion.Sdk.Ugs
{
    /// <summary>Registers the Unity Gaming Services implementations (Authentication, Cloud Save, Leaderboards, Cloud Code).</summary>
    public static class UgsRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PlatformServiceOverrides.Auth = save => new UgsAuthService();
            PlatformServiceOverrides.CloudSave = () => new UgsCloudSaveService();
            PlatformServiceOverrides.Leaderboards = auth => new UgsLeaderboardService(auth);
            PlatformServiceOverrides.ArenaBackend = (db, cfg) => new UgsArenaBackend(db, cfg);
        }
    }

    internal static class UgsCore
    {
        private static Task _init;

        public static Task EnsureInitialized()
        {
            if (_init == null || _init.IsFaulted) _init = UnityServices.InitializeAsync();
            return _init;
        }

        public static async void Run(Func<Task> work, Action<bool> done)
        {
            try
            {
                await EnsureInitialized();
                await work();
                done?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UGS] " + e.Message);
                done?.Invoke(false);
            }
        }
    }

    public sealed class UgsAuthService : IAuthService
    {
        public bool IsSignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;
        public string DisplayName { get; set; } = "Commander";

        public void SignInAnonymously(Action<bool> onComplete)
        {
            UgsCore.Run(async () =>
            {
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }, onComplete);
        }

        public void DeleteAccount(Action<bool> onComplete)
        {
            UgsCore.Run(async () =>
            {
                if (AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.DeleteAccountAsync();
            }, onComplete);
        }
    }

    public sealed class UgsCloudSaveService : ICloudSaveService
    {
        private const string Key = "save";

        public void Save(string json, Action<bool> onComplete)
        {
            UgsCore.Run(async () =>
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, json } }), onComplete);
        }

        public async void Load(Action<CloudLoadResult> onComplete)
        {
            try
            {
                await UgsCore.EnsureInitialized();
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
                if (data.TryGetValue(Key, out var item))
                    onComplete?.Invoke(new CloudLoadResult(CloudLoadStatus.Ok, item.Value.GetAs<string>()));
                else onComplete?.Invoke(new CloudLoadResult(CloudLoadStatus.NotFound));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UGS] cloud load failed: " + e.Message);
                onComplete?.Invoke(new CloudLoadResult(CloudLoadStatus.Failed));
            }
        }

        public void Delete(Action<bool> onComplete)
        {
            UgsCore.Run(async () => await CloudSaveService.Instance.Data.Player.DeleteAsync(Key), onComplete);
        }
    }

    public sealed class UgsLeaderboardService : ILeaderboardService
    {
        private readonly IAuthService _auth;

        public UgsLeaderboardService(IAuthService auth) { _auth = auth; }

        public void SubmitScore(string boardId, long score, Action<bool> onComplete)
        {
            UgsCore.Run(async () => await LeaderboardsService.Instance.AddPlayerScoreAsync(boardId, score), onComplete);
        }

        public async void GetTop(string boardId, int count, Action<IReadOnlyList<MergeLegion.Services.LeaderboardEntry>> onComplete)
        {
            var list = new List<MergeLegion.Services.LeaderboardEntry>();
            try
            {
                await UgsCore.EnsureInitialized();
                var page = await LeaderboardsService.Instance.GetScoresAsync(boardId, new GetScoresOptions { Limit = count });
                foreach (var r in page.Results)
                    list.Add(new MergeLegion.Services.LeaderboardEntry { PlayerId = r.PlayerId, DisplayName = r.PlayerName, Score = (long)r.Score, Rank = r.Rank + 1 });
            }
            catch (Exception e) { Debug.LogWarning("[UGS] leaderboard failed: " + e.Message); }
            onComplete?.Invoke(list);
        }

        public async void GetPlayerEntry(string boardId, Action<MergeLegion.Services.LeaderboardEntry> onComplete)
        {
            try
            {
                await UgsCore.EnsureInitialized();
                var r = await LeaderboardsService.Instance.GetPlayerScoreAsync(boardId);
                onComplete?.Invoke(new MergeLegion.Services.LeaderboardEntry { PlayerId = r.PlayerId, DisplayName = r.PlayerName, Score = (long)r.Score, Rank = r.Rank + 1 });
            }
            catch (Exception) { onComplete?.Invoke(null); }
        }
    }

    /// <summary>Arena via Cloud Code scripts (CloudCode/*.js). Falls back to generated bots so the arena is never empty.</summary>
    public sealed class UgsArenaBackend : IArenaBackend
    {
        private readonly MockArenaBackend _bots;

        public UgsArenaBackend(GameDatabase db, ArenaConfig cfg) { _bots = new MockArenaBackend(db, cfg); }

        public void Upload(ArenaSnapshot snapshot, Action<bool> onDone)
        {
            UgsCore.Run(async () =>
                await CloudCodeService.Instance.CallEndpointAsync<bool>("ArenaUpload",
                    new Dictionary<string, object> { { "snapshot", JsonUtility.ToJson(snapshot) } }), onDone);
        }

        public async void FindOpponents(ArenaSnapshot me, int count, Action<List<ArenaSnapshot>> onDone)
        {
            var result = new List<ArenaSnapshot>();
            try
            {
                await UgsCore.EnsureInitialized();
                string json = await CloudCodeService.Instance.CallEndpointAsync<string>("ArenaFindOpponents",
                    new Dictionary<string, object> { { "trophies", me.trophies }, { "power", me.power }, { "playerId", me.playerId }, { "count", count } });
                var wrapper = JsonUtility.FromJson<OpponentList>("{\"items\":" + json + "}");
                if (wrapper?.items != null) result.AddRange(wrapper.items);
            }
            catch (Exception e) { Debug.LogWarning("[UGS] arena search failed: " + e.Message); }

            if (result.Count >= count) { onDone?.Invoke(result); return; }
            _bots.FindOpponents(me, count - result.Count, bots =>
            {
                result.AddRange(bots);
                result.Sort((a, b) => a.power.CompareTo(b.power));
                onDone?.Invoke(result);
            });
        }

        [Serializable]
        private sealed class OpponentList { public List<ArenaSnapshot> items; }
    }
}
#endif
