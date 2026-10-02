using System;
using System.Collections.Generic;
using System.IO;

namespace MergeLegion.Save
{
    public interface ISaveStorage
    {
        /// <summary>Returns stored texts, newest first (main file, then backup).</summary>
        IReadOnlyList<string> ReadAll();
        void Write(string text);
        void Delete();
    }

    /// <summary>Atomic file storage: writes a temp file, rotates the previous save to .bak, then swaps in the new one.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _path;
        private readonly string _backup;
        private readonly string _temp;

        public FileSaveStorage(string path)
        {
            _path = path;
            _backup = path + ".bak";
            _temp = path + ".tmp";
        }

        public IReadOnlyList<string> ReadAll()
        {
            var list = new List<string>(2);
            TryRead(_path, list);
            TryRead(_backup, list);
            return list;
        }

        public void Write(string text)
        {
            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(_temp, text);
            if (File.Exists(_path)) File.Copy(_path, _backup, true);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(_temp, _path);
        }

        public void Delete()
        {
            foreach (var p in new[] { _path, _backup, _temp })
                if (File.Exists(p)) File.Delete(p);
        }

        private static void TryRead(string path, List<string> into)
        {
            try
            {
                if (File.Exists(path)) into.Add(File.ReadAllText(path));
            }
            catch (IOException) { /* unreadable file is treated as missing */ }
        }
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private string _main;
        private string _backup;

        public IReadOnlyList<string> ReadAll()
        {
            var list = new List<string>(2);
            if (_main != null) list.Add(_main);
            if (_backup != null) list.Add(_backup);
            return list;
        }

        public void Write(string text)
        {
            _backup = _main;
            _main = text;
        }

        public void Delete()
        {
            _main = null;
            _backup = null;
        }

        /// <summary>Test hook: overwrite the main slot without rotating the backup.</summary>
        public void CorruptMain(string garbage) => _main = garbage;
    }
}
