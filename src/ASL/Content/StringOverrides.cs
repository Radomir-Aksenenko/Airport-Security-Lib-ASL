using System;
using System.Collections.Generic;
using BepInEx.Logging;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// EXPERIMENTAL content-mod text overrides (manifest <c>content.strings</c>): any TextMeshPro label whose
    /// text is exactly a key is rewritten to the value. Applied on scene change and on a throttled poll by
    /// <see cref="ContentRegistry"/>; text the game re-sets later (e.g. on language change, or labels
    /// rebuilt every few seconds) can revert until the next pass. Replaced texts are never re-replaced, so
    /// overlapping keys/values cannot chain or ping-pong.
    /// </summary>
    internal sealed class StringOverrides
    {
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _values = new HashSet<string>(StringComparer.Ordinal);
        private bool _failed;

        public StringOverrides(ManualLogSource log) => _log = log;

        public bool HasWork => _map.Count > 0 && !_failed;

        public void Register(string modId, IDictionary<string, string> strings)
        {
            int added = 0;
            foreach (var kv in strings)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null || kv.Key == kv.Value) continue;
                if (_map.ContainsKey(kv.Key)) { _log.LogWarning($"[content] string override for \"{kv.Key}\" from '{modId}' ignored: already set by another mod."); continue; }
                _map[kv.Key] = kv.Value;
                _values.Add(kv.Value);
                added++;
            }
            if (added > 0)
                _log.LogInfo($"[content] '{modId}' queued {added} string override(s). EXPERIMENTAL: exact-match TextMeshPro text only; the game may re-set a label and undo it.");
        }

        public void Apply()
        {
            if (!HasWork) return;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
                int applied = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null) continue;
                    var s = t.text;
                    if (s == null || s.Length == 0) continue;
                    if (_values.Contains(s)) continue;   // already one of our replacements
                    if (_map.TryGetValue(s, out var repl)) { t.text = repl; applied++; }
                }
                if (applied > 0) _log.LogInfo($"[content] strings: rewrote {applied} text label(s) (experimental).");
            }
            catch (Exception ex)
            {
                _failed = true;   // don't retry (and log) every poll
                _log.LogWarning($"[content] string overrides disabled after an error: {ex.Message}");
            }
        }
    }
}
