using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ASL.Api;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace ASL
{
    /// <summary>A registered keybind (see <see cref="IAslKeybind"/>). State is read live from
    /// <see cref="Input"/>, so there is no per-frame caching to get out of sync.</summary>
    internal sealed class AslKeybind : IAslKeybind
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string ModName { get; }
        public KeyCode DefaultKey { get; }
        public KeyCode Key { get; internal set; }
        public bool HasConflict { get; internal set; }
        public event Action Pressed;

        public AslKeybind(string id, string displayName, string modName, KeyCode defaultKey, KeyCode current)
        {
            Id = id; DisplayName = displayName; ModName = modName; DefaultKey = defaultKey; Key = current;
        }

        public bool WasPressed  => Key != KeyCode.None && Safe(() => Input.GetKeyDown(Key));
        public bool IsHeld      => Key != KeyCode.None && Safe(() => Input.GetKey(Key));
        public bool WasReleased => Key != KeyCode.None && Safe(() => Input.GetKeyUp(Key));

        internal void RaisePressed() { try { Pressed?.Invoke(); } catch { } }
        private static bool Safe(Func<bool> f) { try { return f(); } catch { return false; } }
    }

    /// <summary>
    /// Owns every mod keybind: live press dispatch, the rebind capture flow (driven from the menu),
    /// conflict detection (mod-vs-mod is hard-blocked; keys the game uses are flagged), and persistence
    /// of user rebinds to <c>BepInEx/config/ASL.Keybinds.cfg</c>. One instance, created at boot.
    /// </summary>
    internal sealed class KeybindManager
    {
        private readonly ManualLogSource _log;
        private readonly List<AslKeybind> _binds = new List<AslKeybind>();
        private readonly Dictionary<string, KeyCode> _saved = new Dictionary<string, KeyCode>(StringComparer.Ordinal);
        private readonly HashSet<KeyCode> _reserved = new HashSet<KeyCode>();
        private readonly KeyCode[] _candidates;
        private readonly string _configPath;

        private AslKeybind _rebinding;
        private Action _onRebindDone;

        public KeybindManager(ManualLogSource log, IAslEvents events)
        {
            _log = log;
            _configPath = Path.Combine(Paths.ConfigPath, "ASL.Keybinds.cfg");
            SeedReserved();
            _candidates = BuildCandidates();
            LoadConfig();
            if (events != null) events.Update += Poll;
        }

        public IReadOnlyList<AslKeybind> Binds => _binds;
        public bool IsRebinding => _rebinding != null;

        // Get-or-create by namespaced id; applies the player's saved rebind if there is one.
        public AslKeybind GetOrCreate(string modId, string modName, string id, string displayName, KeyCode defaultKey)
        {
            string key = (modId ?? "?") + "/" + (id ?? "?");
            foreach (var b in _binds) if (b.Id == key) return b;

            KeyCode current = _saved.TryGetValue(key, out var savedKey) ? savedKey : defaultKey;
            var bind = new AslKeybind(key, displayName ?? id, modName ?? modId, defaultKey, current);
            _binds.Add(bind);
            RecomputeConflicts();
            return bind;
        }

        // ---- rebind flow (called by NativeMenu) ----

        public void BeginRebind(AslKeybind bind, Action onDone)
        {
            if (bind == null) return;
            _rebinding = bind;
            _onRebindDone = onDone;
        }

        public bool IsRebindTarget(AslKeybind bind) => _rebinding != null && ReferenceEquals(_rebinding, bind);

        private void Poll()
        {
            if (_rebinding != null) { CaptureRebind(); return; }

            // Fire Pressed events (WasPressed polling works without this, but events are handy).
            for (int i = 0; i < _binds.Count; i++)
            {
                var b = _binds[i];
                if (b.Key == KeyCode.None) continue;
                try { if (Input.GetKeyDown(b.Key)) b.RaisePressed(); } catch { }
            }
        }

        private void CaptureRebind()
        {
            bool any;
            try { any = Input.anyKeyDown; } catch { any = false; }
            if (!any) return;

            try { if (Input.GetKeyDown(KeyCode.Escape)) { FinishRebind(); return; } } catch { }

            for (int i = 0; i < _candidates.Length; i++)
            {
                KeyCode k = _candidates[i];
                bool down;
                try { down = Input.GetKeyDown(k); } catch { down = false; }
                if (!down) continue;
                Assign(_rebinding, k);
                FinishRebind();
                return;
            }
        }

        private void FinishRebind()
        {
            var cb = _onRebindDone;
            _rebinding = null; _onRebindDone = null;
            try { cb?.Invoke(); } catch { }
        }

        private void Assign(AslKeybind bind, KeyCode newKey)
        {
            if (newKey == bind.Key) return;

            // Hard-block a clash with another mod's key.
            foreach (var other in _binds)
            {
                if (ReferenceEquals(other, bind)) continue;
                if (other.Key == newKey)
                {
                    _log.LogWarning($"[keys] '{bind.ModName}/{bind.DisplayName}' -> {newKey} rejected: already used by '{other.ModName}/{other.DisplayName}'.");
                    return;   // keep the old binding
                }
            }

            bind.Key = newKey;
            if (_reserved.Contains(newKey))
                _log.LogWarning($"[keys] '{bind.ModName}/{bind.DisplayName}' bound to {newKey}, which the game also uses — it may double-trigger.");
            RecomputeConflicts();
            SaveConfig();
        }

        private void RecomputeConflicts()
        {
            for (int i = 0; i < _binds.Count; i++)
            {
                var b = _binds[i];
                bool conflict = b.Key != KeyCode.None && _reserved.Contains(b.Key);
                if (!conflict && b.Key != KeyCode.None)
                    for (int j = 0; j < _binds.Count; j++)
                        if (j != i && _binds[j].Key == b.Key) { conflict = true; break; }
                b.HasConflict = conflict;
            }
        }

        // ---- reserved (game) keys: a conservative set the game is known to use. Best-effort; the menu
        //      flags these and rebinds onto them warn rather than hard-fail. ----
        private void SeedReserved()
        {
            KeyCode[] r =
            {
                KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.Space, KeyCode.LeftShift, KeyCode.LeftControl,
                KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Escape, KeyCode.Tab,
                // Known game actions (confirmed in-game): F = first/third-person camera, G = surrender.
                KeyCode.F, KeyCode.G, KeyCode.E, KeyCode.R, KeyCode.Q,
            };
            foreach (var k in r) _reserved.Add(k);
        }

        // Keyboard keys we let the player rebind onto (mouse/left-click excluded so clicking the menu
        // row doesn't capture it).
        private static KeyCode[] BuildCandidates()
        {
            var list = new List<KeyCode>();
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++) list.Add(k);
            for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) list.Add(k);
            for (KeyCode k = KeyCode.Keypad0; k <= KeyCode.Keypad9; k++) list.Add(k);
            for (KeyCode k = KeyCode.F1; k <= KeyCode.F12; k++) list.Add(k);
            KeyCode[] extras =
            {
                KeyCode.Space, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl,
                KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.Tab, KeyCode.Return, KeyCode.Backspace, KeyCode.Delete,
                KeyCode.Insert, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.Minus, KeyCode.Equals, KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Semicolon,
                KeyCode.Quote, KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Backslash, KeyCode.BackQuote,
                KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4,
            };
            list.AddRange(extras);
            return list.ToArray();
        }

        // ---- persistence ----

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                foreach (var raw in File.ReadAllLines(_configPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string id = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (Enum.TryParse<KeyCode>(val, true, out var kc)) _saved[id] = kc;
                }
            }
            catch (Exception ex) { _log.LogWarning($"[keys] load config failed: {ex.Message}"); }
        }

        private void SaveConfig()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ASL mod keybinds. <namespaced id> = <KeyCode>. Edit in-game (F8) or here.");
                foreach (var b in _binds) sb.AppendLine(b.Id + " = " + b.Key.ToString());
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                File.WriteAllText(_configPath, sb.ToString());
            }
            catch (Exception ex) { _log.LogWarning($"[keys] save config failed: {ex.Message}"); }
        }
    }

    /// <summary>Per-mod <see cref="IAslInput"/>. Registers keys into the shared <see cref="KeybindManager"/>
    /// and drops a rebind row onto the mod's own menu section so its controls appear automatically.</summary>
    internal sealed class ModInput : IAslInput
    {
        private readonly string _modId;
        private readonly string _modName;
        private readonly MenuManager.Section _section;
        private readonly KeybindManager _keys;
        private readonly ManualLogSource _log;

        public ModInput(string modId, string modName, MenuManager.Section section, KeybindManager keys, ManualLogSource log)
        {
            _modId = modId; _modName = modName; _section = section; _keys = keys; _log = log;
        }

        public IAslKeybind RegisterKey(string id, string displayName, KeyCode defaultKey)
        {
            if (string.IsNullOrEmpty(id)) { _log.LogError($"[{_modName}] Input.RegisterKey: id is null/empty."); return null; }
            var bind = _keys.GetOrCreate(_modId, _modName, id, displayName, defaultKey);

            // Show it on the mod's menu page (once per bind).
            bool present = false;
            foreach (var c in _section.Controls)
                if (c is KeybindControl kc && kc.Bind != null && kc.Bind.Id == bind.Id) { present = true; break; }
            if (!present) _section.Controls.Add(new KeybindControl { Label = displayName ?? id, Bind = bind });

            return bind;
        }

        public bool GetKeyDown(KeyCode key) { try { return Input.GetKeyDown(key); } catch { return false; } }
        public bool GetKey(KeyCode key)     { try { return Input.GetKey(key); } catch { return false; } }
        public bool GetKeyUp(KeyCode key)   { try { return Input.GetKeyUp(key); } catch { return false; } }
    }
}
