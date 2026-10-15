using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASL.Api;
using ASL.Core;
using BepInEx;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslStorage"/>: JSON/text files in <c>BepInEx/config/ASL/data/&lt;modId&gt;/</c>.
    /// The constructor touches nothing; the folder is created on first use. Writes are atomic (temp file,
    /// then replace) so a crash mid-save cannot leave a half-written file. I/O and serialization failures on
    /// save are logged, never thrown at the mod (a save error must not crash the game); bad names throw.
    /// </summary>
    internal sealed class ModStorage : IAslStorage
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() },   // enums as names (still reads numbers)
        };

        private readonly string _modId;
        private readonly ManualLogSource _log;
        private string _dir;
        private bool _dirReady;
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ModStorage(string modId, ManualLogSource log)
        {
            _modId = modId;
            _log = log;
        }

        public string DataDirectory
        {
            get
            {
                if (_dir == null) _dir = Path.Combine(Paths.ConfigPath, "ASL", "data", StorageNames.ToSafeFileName(_modId));
                if (!_dirReady)
                {
                    try { Directory.CreateDirectory(_dir); _dirReady = true; }
                    catch (Exception ex) { WarnOnce("dir", $"cannot create data folder '{_dir}': {ex.Message}"); }
                }
                return _dir;
            }
        }

        public void Save<T>(string name, T value)
        {
            var path = FileFor(name, ".json");
            try
            {
                WriteAtomic(path, JsonSerializer.Serialize(value, Json));
            }
            catch (Exception ex) { _log.LogError($"[{_modId}] Storage.Save('{name}') failed: {ex.Message}"); }
        }

        public T Load<T>(string name, T fallback = default)
        {
            var path = FileFor(name, ".json");
            string text;
            try
            {
                if (!File.Exists(path)) return fallback;
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                WarnOnce("read:" + name, $"Storage.Load('{name}') could not read the file ({ex.Message}); using the fallback.");
                return fallback;
            }

            try
            {
                var v = JsonSerializer.Deserialize<T>(text, Json);
                // A literal "null" (or an empty file) deserializes to null/default for reference types: treat as missing.
                if (v == null && fallback != null) return fallback;
                return v;
            }
            catch (Exception ex)
            {
                WarnOnce("corrupt:" + name, $"Storage file '{Path.GetFileName(path)}' is corrupt ({ex.Message}); using the fallback. A copy was kept as .bad.");
                try { File.Copy(path, path + ".bad", true); } catch { /* best effort */ }
                return fallback;
            }
        }

        public bool Exists(string name) => File.Exists(FileFor(name, ".json"));

        public void Delete(string name)
        {
            try
            {
                var path = FileFor(name, ".json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { _log.LogError($"[{_modId}] Storage.Delete('{name}') failed: {ex.Message}"); }
        }

        public void SaveText(string name, string text)
        {
            var path = FileFor(name, ".txt");
            try { WriteAtomic(path, text ?? ""); }
            catch (Exception ex) { _log.LogError($"[{_modId}] Storage.SaveText('{name}') failed: {ex.Message}"); }
        }

        public string LoadText(string name, string fallback = null)
        {
            var path = FileFor(name, ".txt");
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : fallback; }
            catch (Exception ex)
            {
                WarnOnce("readtxt:" + name, $"Storage.LoadText('{name}') could not read the file ({ex.Message}); using the fallback.");
                return fallback;
            }
        }

        // Sanitize (throws ArgumentException for an empty/invalid name) and place inside the data folder.
        private string FileFor(string name, string ext) => Path.Combine(DataDirectory, StorageNames.ToSafeFileName(name) + ext);

        // Write to <path>.tmp, then swap into place. File.Replace is atomic on NTFS; Move covers the first write.
        private static void WriteAtomic(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        private void WarnOnce(string id, string message)
        {
            if (_warned.Add(id)) _log.LogWarning($"[{_modId}] {message}");
        }
    }
}
