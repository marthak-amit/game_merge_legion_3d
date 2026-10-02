using System;
using MergeLegion.Save;

namespace MergeLegion.Services.Mock
{
    /// <summary>"Cloud" backed by a local storage slot, so conflict resolution can be exercised offline.</summary>
    public sealed class MockCloudSaveService : ICloudSaveService
    {
        private readonly ISaveStorage _storage;

        public MockCloudSaveService(ISaveStorage storage) { _storage = storage; }

        public void Save(string json, Action<bool> onComplete)
        {
            _storage.Write(json);
            onComplete?.Invoke(true);
        }

        public void Load(Action<CloudLoadResult> onComplete)
        {
            var all = _storage.ReadAll();
            onComplete?.Invoke(all.Count == 0
                ? new CloudLoadResult(CloudLoadStatus.NotFound)
                : new CloudLoadResult(CloudLoadStatus.Ok, all[0]));
        }

        public void Delete(Action<bool> onComplete)
        {
            _storage.Delete();
            onComplete?.Invoke(true);
        }
    }
}
