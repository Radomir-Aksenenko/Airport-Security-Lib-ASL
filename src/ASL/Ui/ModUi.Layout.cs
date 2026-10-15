using System;
using System.Collections.Generic;
using System.IO;
using ASL.Api;
using ASL.Core;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// The declarative-layout half of the per-mod <see cref="IAslUi"/>: <c>LoadLayout</c> (JSON file -> parsed by
    /// the pure <see cref="LayoutParser"/> -> built by <see cref="LayoutBuilder"/>), named-action dispatch, toggle
    /// keys and the hooks that tie the layout's windows / config bindings to the mod's lifetime.
    /// </summary>
    internal sealed partial class ModUi
    {
        private ModConfig _config;
        private IAslAssets _assets;
        private bool _released;
        private readonly List<Action> _undo = new List<Action>();
        private readonly List<AslKeybind> _toggleBinds = new List<AslKeybind>();
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Connects this UI to the mod's services (config for <c>bind</c>, assets for <c>image</c>) and to its release event.</summary>
        internal void Attach(ModServices services)
        {
            if (services == null) return;
            _config = services.Config as ModConfig;
            _assets = services.Assets;
            services.Releasing += ReleaseAll;
        }

        /// <summary>For mods without services (content mods): only assets are available; the caller releases through <see cref="ReleaseAll"/>.</summary>
        internal void AttachStandalone(IAslAssets assets) { _assets = assets; }

        internal ModConfig ConfigSource => _config;
        internal IAslAssets AssetsSource => _assets;
        internal bool IsReleased => _released;
        internal string ModIdValue => _modId;
        internal string ModNameValue => _modName;

        public IUiWindow LoadLayout(string relativeJsonPath)
        {
            if (_released) return null;
            try
            {
                if (string.IsNullOrWhiteSpace(relativeJsonPath)) { _log.LogError($"[{_modName}] Ui.LoadLayout: path is empty."); return null; }
                if (string.IsNullOrEmpty(_modDirectory)) { _log.LogError($"[{_modName}] Ui.LoadLayout('{relativeJsonPath}'): this mod has no folder."); return null; }

                string full = StorageNames.SafeJoin(_modDirectory, relativeJsonPath);
                if (!File.Exists(full)) { _log.LogError($"[{_modName}] Ui.LoadLayout: file not found: {relativeJsonPath}"); return null; }

                var res = LayoutParser.Parse(File.ReadAllText(full));
                if (!res.Ok) { _log.LogError($"[{_modName}] Ui.LoadLayout('{relativeJsonPath}'): {res.Error}"); return null; }
                foreach (var w in res.Warnings) _log.LogWarning($"[{_modName}] {relativeJsonPath}: {w}");

                return LayoutBuilder.Build(this, res.Root, relativeJsonPath);
            }
            catch (Exception ex)
            {
                _log.LogError($"[{_modName}] Ui.LoadLayout('{relativeJsonPath}') failed: {ex.Message}");
                return null;
            }
        }

        // ---- services for the builder ------------------------------------------------------------

        /// <summary>Runs a registered named action; logs once per name if there is none.</summary>
        internal void InvokeNamed(string name, string value)
        {
            if (_released) return;
            if (_actions.TryGetValue(name, out var a)) { Owner.Fire(a, value ?? string.Empty, "action '" + name + "'"); return; }
            WarnOnce("action:" + name, $"[{_modName}] layout action '{name}' is not registered (ctx.Ui.RegisterAction); the click did nothing.");
        }

        internal void WarnOnce(string key, string message)
        {
            if (_warned.Add(key)) _log.LogWarning(message);
        }

        internal void LogWarning(string message) => _log.LogWarning(message);

        /// <summary>Something to undo when the mod is released (config subscriptions...).</summary>
        internal void TrackUndo(Action undo) { if (undo != null) _undo.Add(undo); }

        /// <summary>
        /// Registers (or finds) the rebindable ASL keybind that toggles a window - id <c>ui.&lt;windowId&gt;.toggle</c>, shown
        /// on the mod's F8 page like any <c>ctx.Input.RegisterKey</c> key.
        /// </summary>
        internal AslKeybind RegisterToggleKey(string windowId, string title, KeyCode key)
        {
            var keys = AslPlugin.Keybinds;
            if (keys == null) return null;
            string display = "Toggle " + (string.IsNullOrEmpty(title) ? windowId : title);
            var bind = keys.GetOrCreate(_modId, _modName, "ui." + windowId + ".toggle", display, key);

            var section = AslPlugin.Menu?.EnsureSection(_modName);
            if (section != null)
            {
                bool present = false;
                foreach (var c in section.Controls)
                    if (c is KeybindControl kc && kc.Bind != null && kc.Bind.Id == bind.Id) { present = true; break; }
                if (!present) section.Controls.Add(new KeybindControl { Label = display, Bind = bind });
            }
            if (!_toggleBinds.Contains(bind)) _toggleBinds.Add(bind);
            return bind;
        }

        /// <summary>Layout part of the release: config subscriptions and toggle-key handlers. Called from <see cref="ReleaseAll"/>.</summary>
        private void ReleaseLayoutState()
        {
            _released = true;
            for (int i = _undo.Count - 1; i >= 0; i--)
            {
                try { _undo[i](); } catch { }
            }
            _undo.Clear();
            foreach (var b in _toggleBinds) { try { b.ClearPressed(); } catch { } }
            _toggleBinds.Clear();
        }
    }
}
