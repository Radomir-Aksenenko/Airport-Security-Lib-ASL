using System;
using System.Collections.Generic;
using ASL.Api;

namespace ASL
{
    /// <summary>The toolkit half of the per-mod <see cref="IAslUi"/>: windows, HUDs, toasts and dialogs.</summary>
    internal sealed partial class ModUi
    {
        private UiOwner _owner;
        private readonly Dictionary<string, UiWindow> _windows = new Dictionary<string, UiWindow>(StringComparer.Ordinal);

        /// <summary>Mod identity used for log lines and callback guards.</summary>
        internal UiOwner Owner => _owner ??= new UiOwner(_modId, _modName, _log);

        public bool IsTextInputFocused => UiRuntime.IsTextInputFocused;

        public IUiWindow CreateWindow(string id, string title, UiWindowOptions options = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("window id is required", nameof(id));
            var opt = options != null ? options.Clone() : new UiWindowOptions();
            return Register(id, title, opt, hud: false);
        }

        public IUiWindow CreateHud(string id, UiAnchor anchor, UiWindowOptions options = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("HUD id is required", nameof(id));
            // A HUD is a non-interactive overlay: no title bar, no cursor capture, no input blocking, click-through.
            var opt = new UiWindowOptions
            {
                Width = 300f, Anchor = anchor,
                Draggable = false, Closable = false, Modal = false, CaptureCursor = false, BlockGameInput = false,
                ShowTitleBar = false, Scrollable = false, RememberPosition = false, CloseOnEscape = false,
            };
            if (options != null)
            {
                opt.Width = options.Width; opt.Height = options.Height;
                opt.OffsetX = options.OffsetX; opt.OffsetY = options.OffsetY;
                opt.Theme = options.Theme; opt.Padding = options.Padding; opt.SortOrder = options.SortOrder;
            }
            var w = Register(id, string.Empty, opt, hud: true);
            w.Open();
            return w;
        }

        private UiWindow Register(string id, string title, UiWindowOptions opt, bool hud)
        {
            if (_windows.TryGetValue(id, out var old)) UiRuntime.DestroyWindow(old);
            var theme = opt.Theme ?? Theme ?? UiTheme.Default;
            var w = UiWindow.Create(Owner, _modId + "/" + id, id, title, opt, theme, hud, false);
            _windows[id] = w;
            return w;
        }

        public IUiWindow GetWindow(string id)
        {
            if (id != null && _windows.TryGetValue(id, out var w) && !w.Dead) return w;
            return null;
        }

        public void Toast(string text, UiToastKind kind = UiToastKind.Info, float seconds = 3f) =>
            UiToasts.Show(Owner, Theme ?? UiTheme.Default, text, kind, seconds);

        public void Confirm(string title, string message, Action onYes, Action onNo = null, string yesText = "Yes", string noText = "No") =>
            Guard("Ui.Confirm", () => UiDialogs.Confirm(Owner, Theme ?? UiTheme.Default, title, message, onYes, onNo, yesText, noText));

        public void Prompt(string title, string message, string initial, Action<string> onSubmit, Action onCancel = null) =>
            Guard("Ui.Prompt", () => UiDialogs.Prompt(Owner, Theme ?? UiTheme.Default, title, message, initial, onSubmit, onCancel));

        public void Alert(string title, string message, Action onClose = null) =>
            Guard("Ui.Alert", () => UiDialogs.Alert(Owner, Theme ?? UiTheme.Default, title, message, onClose));

        private void Guard(string what, Action a)
        {
            try { a(); } catch (Exception ex) { Owner.Report(what, ex); }
        }

        /// <summary>Destroys every window/HUD this mod created (mod unload / reload).</summary>
        internal void ReleaseAll()
        {
            var list = new List<UiWindow>(_windows.Values);
            _windows.Clear();
            foreach (var w in list) UiRuntime.DestroyWindow(w);
        }
    }
}
