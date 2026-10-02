using System;
using MergeLegion.Core;

namespace MergeLegion.Save
{
    public enum LoadOutcome { Loaded, NewGame, RecoveredFromBackup, Tampered, FutureVersion }

    /// <summary>Owns the in-memory <see cref="SaveData"/> and persists it through an <see cref="ISaveStorage"/>.</summary>
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;
        private readonly ITimeService _time;
        private readonly SaveCodec _codec;
        private bool _dirty;

        public SaveData Data { get; private set; }
        public bool IsDirty => _dirty;

        public SaveService(ISaveStorage storage, ITimeService time, SaveCodec codec = null)
        {
            _storage = storage;
            _time = time;
            _codec = codec ?? new SaveCodec();
            Data = NewData();
        }

        public LoadOutcome Load()
        {
            var texts = _storage.ReadAll();
            bool first = true;
            for (int i = 0; i < texts.Count; i++, first = false)
            {
                var result = _codec.Decode(texts[i]);
                if (result.Status == DecodeStatus.FutureVersion) return LoadOutcome.FutureVersion;
                if (!result.HasData) continue;

                Data = result.Data;
                if (result.Status == DecodeStatus.Tampered) return LoadOutcome.Tampered;
                return first ? LoadOutcome.Loaded : LoadOutcome.RecoveredFromBackup;
            }

            Data = NewData();
            _dirty = true;
            return LoadOutcome.NewGame;
        }

        public void MarkDirty() => _dirty = true;

        /// <summary>Saves only when something changed.</summary>
        public void Flush()
        {
            if (_dirty) Save();
        }

        public void Save()
        {
            Data.lastSavedUtcTicks = _time.UtcNow.Ticks;
            _storage.Write(_codec.Encode(Data));
            _dirty = false;
            EventBus.Publish(new SaveCompletedEvent());
        }

        /// <summary>Replaces local state, e.g. after the player confirms a cloud save.</summary>
        public void Replace(SaveData data)
        {
            Data = data;
            Save();
        }

        public string ExportJson() => _codec.Encode(Data);

        public DecodeResult Import(string text) => _codec.Decode(text);

        public void DeleteAll()
        {
            _storage.Delete();
            Data = NewData();
            _dirty = false;
        }

        private SaveData NewData()
        {
            var d = new SaveData();
            long now = _time.UtcNow.Ticks;
            d.createdUtcTicks = now;
            d.lastSavedUtcTicks = now;
            return d;
        }
    }
}
