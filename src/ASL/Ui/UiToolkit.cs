using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslUi"/> (handed out as <c>ctx.Ui</c>). <see cref="Announce"/> delegates to the
    /// shared <see cref="UiManager"/> banner helper. The window / HUD / toast / dialog members live in
    /// <c>ModUi.Toolkit.cs</c> (same class, partial); <c>LoadLayout</c> (declarative JSON layouts) lives in <c>ModUi.Layout.cs</c>.
    /// </summary>
    internal sealed partial class ModUi : IAslUi
    {
        private readonly string _modId;
        private readonly string _modName;
        private readonly string _modDirectory;
        private readonly UiManager _shared;
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Action<string>> _actions = new(StringComparer.Ordinal);

        public ModUi(string modId, string modName, string modDirectory, UiManager shared, ManualLogSource log)
        {
            _modId = modId;
            _modName = modName;
            _modDirectory = modDirectory;
            _shared = shared;
            _log = log;
        }

        public void Announce(string text, float seconds = 2.5f) => _shared.Announce(text, seconds);

        public UiTheme Theme { get; set; } = UiTheme.Default;

        public void RegisterAction(string name, Action action)
        {
            if (string.IsNullOrEmpty(name) || action == null) return;
            _actions[name] = _ => action();
        }

        public void RegisterAction(string name, Action<string> action)
        {
            if (string.IsNullOrEmpty(name) || action == null) return;
            _actions[name] = action;
        }

    }
}
