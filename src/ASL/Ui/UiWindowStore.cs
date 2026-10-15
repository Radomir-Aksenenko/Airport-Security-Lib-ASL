using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ASL.Api;
using BepInEx;

namespace ASL
{
    /// <summary>
    /// Remembers where the player dragged each window: <c>BepInEx/config/ASL.Windows.json</c>, keyed
    /// <c>modId/windowId</c> -> anchor + offsets. Written on drag end only (never per frame).
    /// </summary>
    internal static class UiWindowStore
    {
        public sealed class Entry
        {
            public string Anchor { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
        }

        private static Dictionary<string, Entry> _map;

        private static string FilePath => Path.Combine(Paths.ConfigPath, "ASL.Windows.json");

        private static void Load()
        {
            if (_map != null) return;
            _map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            try
            {
                var p = FilePath;
                if (!File.Exists(p)) return;
                var loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(p));
                if (loaded != null) foreach (var kv in loaded) if (kv.Value != null) _map[kv.Key] = kv.Value;
            }
            catch (Exception ex) { AslPlugin.Logger?.LogWarning($"[ui] could not read ASL.Windows.json: {ex.Message}"); }
        }

        /// <summary>Saved offsets for a window, if any and if they were saved for the same anchor.</summary>
        public static bool TryGet(string key, UiAnchor anchor, out float x, out float y)
        {
            Load();
            x = 0f; y = 0f;
            if (!_map.TryGetValue(key, out var e) || e.Anchor != anchor.ToString()) return false;
            x = e.X; y = e.Y;
            return true;
        }

        public static void Set(string key, UiAnchor anchor, float x, float y)
        {
            Load();
            _map[key] = new Entry { Anchor = anchor.ToString(), X = x, Y = y };
            try
            {
                var p = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                var tmp = p + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
            }
            catch (Exception ex) { AslPlugin.Logger?.LogWarning($"[ui] could not save ASL.Windows.json: {ex.Message}"); }
        }
    }
}
