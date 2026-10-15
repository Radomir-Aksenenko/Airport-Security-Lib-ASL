using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslUi"/> (handed out as <c>ctx.Ui</c>). <see cref="Announce"/> delegates to the
    /// shared <see cref="UiManager"/> banner helper; the toolkit members are owned by the UI agent
    /// and are placeholders for now.
    /// </summary>
    internal sealed class ModUi : IAslUi
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

        public bool IsTextInputFocused => false;   // TODO(UI agent): true while an ASL text field has focus

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

        public IUiWindow CreateWindow(string id, string title, UiWindowOptions options = null) => throw NotYet("Ui.CreateWindow");
        public IUiWindow CreateHud(string id, UiAnchor anchor, UiWindowOptions options = null) => throw NotYet("Ui.CreateHud");
        public IUiWindow GetWindow(string id) => null;
        public void Toast(string text, UiToastKind kind = UiToastKind.Info, float seconds = 3f) => _log.LogInfo($"[toast:{kind}] {text}");
        public void Confirm(string title, string message, Action onYes, Action onNo = null, string yesText = "Yes", string noText = "No") => throw NotYet("Ui.Confirm");
        public void Prompt(string title, string message, string initial, Action<string> onSubmit, Action onCancel = null) => throw NotYet("Ui.Prompt");
        public void Alert(string title, string message, Action onClose = null) => throw NotYet("Ui.Alert");
        public IUiWindow LoadLayout(string relativeJsonPath) => throw NotYet("Ui.LoadLayout");

        private static NotImplementedException NotYet(string what) => new NotImplementedException($"ASL 2.0: {what} not implemented yet");
    }
}
