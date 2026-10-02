using MergeLegion.Core;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Save
{
    /// <summary>Drives autosave: periodic flush of dirty state, plus flush on pause/quit.</summary>
    public sealed class SaveRunner : MonoBehaviour
    {
        private SaveService _save;
        private float _interval = 30f;
        private float _timer;

        public void Init(SaveService save, IRemoteConfigService config)
        {
            _save = save;
            _interval = Mathf.Max(5f, config.GetFloat(RemoteKeys.SaveAutosaveSeconds, 30f));
            EventBus.Subscribe<AppPauseEvent>(OnPause);
        }

        private void OnPause(AppPauseEvent e)
        {
            if (e.Paused) _save?.Flush();
        }

        private void Update()
        {
            if (_save == null) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer < _interval) return;
            _timer = 0f;
            _save.Flush();
        }

        private void OnApplicationQuit() => _save?.Flush();

        private void OnDestroy() => EventBus.Unsubscribe<AppPauseEvent>(OnPause);
    }
}
