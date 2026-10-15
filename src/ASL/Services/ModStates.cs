using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Persisted enabled/disabled switch per mod id: <c>BepInEx/config/ASL.ModStates.json</c>, a flat
    /// <c>{ "modId": true|false }</c> map. A mod that is not listed is enabled.
    ///
    /// The file is read ONCE at boot (<see cref="Init"/>) and that snapshot decides what loads this session
    /// (<see cref="IsEnabled"/>). <see cref="SetEnabled"/> (used by the Mod Manager page) writes the file
    /// immediately but only changes the pending value (<see cref="IsEnabledNextLaunch"/>): it takes effect
    /// next launch. Main thread only; no call here ever throws.
    /// </summary>
    internal static class ModStates
    {
        private static readonly object Gate = new();
        private static Dictionary<string, bool> _boot = new(StringComparer.Ordinal);      // what this session honours
        private static Dictionary<string, bool> _pending = new(StringComparer.Ordinal);   // what the file holds now
        private static string _path;
        private static ManualLogSource _log;
        private static bool _inited;

        /// <summary>Raised after <see cref="SetEnabled"/> persisted a change (id, new value).</summary>
        public static event Action<string, bool> Changed;

        /// <summary>The file in use (null before <see cref="Init"/>).</summary>
        public static string FilePath => _path;

        /// <summary>Read the state file (once). Safe to call repeatedly; later calls are ignored.</summary>
        public static void Init(ManualLogSource log, string pathOverride = null)
        {
            lock (Gate)
            {
                if (_inited) return;
                _inited = true;
                _log = log;
                try { _path = pathOverride ?? Path.Combine(Paths.ConfigPath, "ASL.ModStates.json"); }
                catch (Exception ex) { _log?.LogWarning($"ModStates: no config path ({ex.Message}); all mods stay enabled."); return; }

                var map = Read(_path, _log);
                _boot = new Dictionary<string, bool>(map, StringComparer.Ordinal);
                _pending = new Dictionary<string, bool>(map, StringComparer.Ordinal);
            }
        }

        /// <summary>True if the mod is enabled for THIS session (the boot snapshot). Unknown ids are enabled.</summary>
        public static bool IsEnabled(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return true;
            lock (Gate) return !_boot.TryGetValue(modId, out var v) || v;
        }

        /// <summary>True if the mod will be enabled on the NEXT launch (what the Mod Manager toggle shows).</summary>
        public static bool IsEnabledNextLaunch(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return true;
            lock (Gate) return !_pending.TryGetValue(modId, out var v) || v;
        }

        /// <summary>True if the pending value differs from what this session is running with.</summary>
        public static bool RestartRequired(string modId) => IsEnabled(modId) != IsEnabledNextLaunch(modId);

        /// <summary>Persist a new enabled state immediately. Takes effect next launch. Returns false if it could not be saved.</summary>
        public static bool SetEnabled(string modId, bool enabled)
        {
            if (string.IsNullOrEmpty(modId)) return false;
            bool saved;
            lock (Gate)
            {
                if (!_inited) Init(_log);
                if (enabled) _pending.Remove(modId);   // absent == enabled keeps the file small
                else _pending[modId] = false;
                saved = Write(_path, _pending, _log);
            }
            try { Changed?.Invoke(modId, enabled); }
            catch (Exception ex) { _log?.LogError($"ModStates.Changed handler threw: {ex.Message}"); }
            return saved;
        }

        // ---- file I/O (never throws) -----------------------------------------------------------

        private static Dictionary<string, bool> Read(string path, ManualLogSource log)
        {
            var map = new Dictionary<string, bool>(StringComparer.Ordinal);
            try
            {
                if (path == null || !File.Exists(path)) return map;
                using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.True) map[p.Name] = true;
                    else if (p.Value.ValueKind == JsonValueKind.False) map[p.Name] = false;
                }
            }
            catch (Exception ex) { log?.LogWarning($"ModStates: could not read '{path}' ({ex.Message}); treating every mod as enabled."); }
            return map;
        }

        private static bool Write(string path, Dictionary<string, bool> map, ManualLogSource log)
        {
            if (path == null) return false;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return true;
            }
            catch (Exception ex)
            {
                log?.LogError($"ModStates: could not save '{path}': {ex.Message}");
                return false;
            }
        }
    }
}
