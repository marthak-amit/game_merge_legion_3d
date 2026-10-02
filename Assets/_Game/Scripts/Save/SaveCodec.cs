using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MergeLegion.Save
{
    public enum DecodeStatus { Ok, Empty, Corrupt, Tampered, FutureVersion, MigrationFailed }

    public readonly struct DecodeResult
    {
        public readonly DecodeStatus Status;
        public readonly SaveData Data;
        public DecodeResult(DecodeStatus status, SaveData data = null) { Status = status; Data = data; }
        public bool HasData => Data != null;
    }

    /// <summary>Upgrades the raw payload JSON from <see cref="FromVersion"/> to FromVersion + 1.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        string Migrate(string payloadJson);
    }

    [Serializable]
    internal sealed class SaveEnvelope
    {
        public int version;
        public string checksum;
        public string payload;
    }

    /// <summary>
    /// Versioned, checksummed save format. The checksum is a deterrent against casual editing only;
    /// real protection requires server-authoritative state (cloud save validation).
    /// </summary>
    public sealed class SaveCodec
    {
        /// <summary>Bump when the payload shape changes and add a migration for the previous version.</summary>
        public const int CurrentVersion = 1;

        private const string Salt = "ml3d.v1.7c1f9e";

        private readonly int _currentVersion;
        private readonly Dictionary<int, ISaveMigration> _migrations = new Dictionary<int, ISaveMigration>();

        public SaveCodec(IEnumerable<ISaveMigration> migrations = null, int currentVersion = CurrentVersion)
        {
            _currentVersion = currentVersion;
            if (migrations == null) return;
            foreach (var m in migrations) _migrations[m.FromVersion] = m;
        }

        public string Encode(SaveData data)
        {
            string payload = JsonUtility.ToJson(data);
            var env = new SaveEnvelope { version = _currentVersion, payload = payload, checksum = Checksum(payload) };
            return JsonUtility.ToJson(env);
        }

        public DecodeResult Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return new DecodeResult(DecodeStatus.Empty);

            SaveEnvelope env;
            try { env = JsonUtility.FromJson<SaveEnvelope>(text); }
            catch (Exception) { return new DecodeResult(DecodeStatus.Corrupt); }
            if (env == null || string.IsNullOrEmpty(env.payload)) return new DecodeResult(DecodeStatus.Corrupt);

            if (env.version > _currentVersion) return new DecodeResult(DecodeStatus.FutureVersion);

            bool tampered = env.checksum != Checksum(env.payload);

            string payload = env.payload;
            for (int v = env.version; v < _currentVersion; v++)
            {
                if (!_migrations.TryGetValue(v, out var migration)) return new DecodeResult(DecodeStatus.MigrationFailed);
                try { payload = migration.Migrate(payload); }
                catch (Exception) { return new DecodeResult(DecodeStatus.MigrationFailed); }
            }

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(payload); }
            catch (Exception) { return new DecodeResult(DecodeStatus.Corrupt); }
            if (data == null) return new DecodeResult(DecodeStatus.Corrupt);

            if (tampered) data.tamperDetected = true;
            return new DecodeResult(tampered ? DecodeStatus.Tampered : DecodeStatus.Ok, data);
        }

        private static string Checksum(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + payload));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
