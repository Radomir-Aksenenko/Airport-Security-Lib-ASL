using System;
using ASL.Api;
using UnityEngine;

namespace UiShowcase
{
    /// <summary>
    /// The main showcase window: a tab strip with one tab per widget family. Each tab is built by its own small
    /// method so you can read them independently.
    /// </summary>
    internal sealed class ShowcaseWindow
    {
        private readonly IModContext _ctx;
        private readonly IAslKeybind _toggleKey;
        private readonly IConfigEntry<ThemePreset> _theme;
        private readonly Action _onOpened;
        private readonly Func<int> _openCount;

        private IUiWindow _win;
        private IUiProgress _progress;
        private IAslTimer _progressTimer;
        private float _progressValue;

        public ShowcaseWindow(IModContext ctx, IAslKeybind toggleKey, IConfigEntry<ThemePreset> theme,
                              Action onOpened, Func<int> openCount)
        {
            _ctx = ctx;
            _toggleKey = toggleKey;
            _theme = theme;
            _onOpened = onOpened;
            _openCount = openCount;
        }

        public IUiWindow Window => _win;

        public void Open() => _win?.Open();

        /// <summary>Rebuild the window (new theme / language) and re-show it if it was showing.</summary>
        public void Rebuild()
        {
            bool wasOpen = _win != null && _win.IsOpen;
            Build();
            if (wasOpen) _win.Open();
        }

        public void Build()
        {
            _progressTimer?.Cancel();   // the old window's progress bar is about to be destroyed

            // Window ids are namespaced per mod. Creating "main" again replaces the previous window.
            // The theme is read from ctx.Ui.Theme (set by UiShowcaseMod) when the window is created.
            _win = _ctx.Ui.CreateWindow("main", _ctx.Loc.Get("window.title"), new UiWindowOptions
            {
                Width = 780,
                Anchor = UiAnchor.Center,
                Draggable = true,
                CloseOnEscape = true,
            });

            // F7 (rebindable in Settings) toggles the window. Ignored while the player types in a text field.
            _win.BindToggleKey(_toggleKey);

            // Opened/Closed events let you react to visibility: here we count opens and refresh the counter.
            _win.Opened += () => _onOpened();

            _win.AddLiveLabel(() => _ctx.Loc.Get("window.opened", _openCount()), 1f);

            var tabs = _win.AddTabs();
            BuildButtonsTab(tabs.AddTab("Buttons"));
            BuildInputsTab(tabs.AddTab("Inputs"));
            BuildLayoutTab(tabs.AddTab("Layout"));
            BuildDialogsTab(tabs.AddTab("Dialogs"));
            BuildThemesTab(tabs.AddTab("Themes"));
        }

        // ---- Tab 1: Buttons ------------------------------------------------------------------------------------

        private void BuildButtonsTab(IUiContainer tab)
        {
            // Every UiShape x UiSize. A grid lays children out in fixed-size cells; 6 shapes per row.
            tab.AddHeader("Shape x Size");
            tab.AddLabel("Columns: Rect, Rounded, Pill, Circle, Square, Native. Rows: Small to XLarge.", UiTextStyle.Muted);
            var shapes = (UiShape[])Enum.GetValues(typeof(UiShape));
            var sizes = (UiSize[])Enum.GetValues(typeof(UiSize));
            var grid = tab.AddGrid(shapes.Length, 112f, 84f);
            foreach (var size in sizes)
                foreach (var shape in shapes)
                {
                    var s = shape; var z = size;
                    // UiButtonStyle is plain data. Presets (Primary, Pill(...), ...) return fresh copies; With(...) clones.
                    var style = UiButtonStyle.Primary.With(shape: s, size: z);
                    grid.AddButton(shape.ToString(), () => _ctx.Ui.Toast($"{s} / {z}", UiToastKind.Info, 1.5f), style);
                }

            // Every colour role. The colours come from the active theme.
            tab.AddSeparator();
            tab.AddHeader("Variants");
            var variants = (UiVariant[])Enum.GetValues(typeof(UiVariant));
            var vgrid = tab.AddGrid(4, 170f, 52f);
            foreach (var v in variants)
            {
                var variant = v;
                vgrid.AddButton(variant.ToString(), () => _ctx.Ui.Toast(variant + " clicked", UiToastKind.Info, 1.5f),
                    new UiButtonStyle { Variant = variant });
            }

            // Pill and circle presets, and icon buttons.
            tab.AddSeparator();
            tab.AddHeader("Pill, circle and icon");
            var row = tab.AddRow();
            row.AddButton("Pill", () => { }, UiButtonStyle.Pill(UiVariant.Success));
            row.AddButton("Pill", () => { }, UiButtonStyle.Pill(UiVariant.Warning).With(size: UiSize.Large));
            row.AddButton("OK", () => { }, UiButtonStyle.Circle(56f, UiVariant.Danger));

            // Icon sprites come from ctx.Assets (PNG files in the mod folder). This sample ships no binary
            // files, so the icon is optional: drop an "icon.png" next to the DLL to see icon buttons.
            Sprite icon = TryLoadIcon();
            if (icon != null)
            {
                row.AddIconButton(icon, () => _ctx.Ui.Toast("Icon button", UiToastKind.Info), 56f, UiShape.Circle);
                row.AddIconButton(icon, () => _ctx.Ui.Toast("Square icon", UiToastKind.Info), 56f, UiShape.Square);
                var withText = row.AddButton("With icon", () => { }, UiButtonStyle.Secondary);
                withText.SetIcon(icon);   // SetIcon puts the sprite on an existing button
            }
            else
            {
                tab.AddLabel("(No icon.png in the mod folder: icon buttons skipped.)", UiTextStyle.Muted);
            }

            // Buttons are objects: keep the handle to change them later.
            tab.AddSeparator();
            var counter = 0;
            var clicker = tab.AddButton("Clicked 0 times", () => { }, UiButtonStyle.Outline);
            clicker.Clicked += () => clicker.Text = $"Clicked {++counter} times";   // extra listener via the event
            clicker.Tooltip = "Hover for ~0.5 s to see a tooltip.";
        }

        private Sprite TryLoadIcon()
        {
            try
            {
                // GetPath resolves a path inside the mod folder; check it exists before decoding.
                if (!System.IO.File.Exists(_ctx.Assets.GetPath("icon.png"))) return null;
                return _ctx.Assets.LoadSprite("icon.png");   // may still be null if the PNG is unreadable
            }
            catch (Exception ex)
            {
                _ctx.Log.Warning("Could not load icon.png: " + ex.Message);
                return null;
            }
        }

        // ---- Tab 2: Inputs -------------------------------------------------------------------------------------

        private void BuildInputsTab(IUiContainer tab)
        {
            tab.AddHeader("Toggles");
            // The same bool, three looks.
            tab.AddToggle("Switch (default)", true, v => _ctx.Log.Info("switch: " + v), UiToggleStyle.Switch);
            tab.AddToggle("Checkbox", false, v => _ctx.Log.Info("checkbox: " + v), UiToggleStyle.Checkbox);
            tab.AddToggle("Button", false, v => _ctx.Log.Info("button toggle: " + v), UiToggleStyle.Button);

            tab.AddSeparator();
            tab.AddHeader("Slider");
            // step snaps the value; format is a .NET float format for the readout.
            tab.AddSlider("Volume (step 5)", 0f, 100f, 50f, v => _ctx.Log.Info("volume: " + v), 5f, "0");
            tab.AddSlider("Continuous", 0f, 1f, 0.25f, v => { }, 0f, "0.00");

            tab.AddSeparator();
            tab.AddHeader("Text input");
            var echo = tab.AddLabel("Type something and press Enter.", UiTextStyle.Muted);
            var input = tab.AddInput("Your name...", "", text => echo.Text = "Submitted: " + text);
            input.CharacterLimit = 24;

            tab.AddSeparator();
            tab.AddHeader("Dropdown");
            tab.AddDropdown("Favourite colour", new[] { "Red", "Green", "Blue" }, 1,
                i => _ctx.Ui.Toast("Picked index " + i, UiToastKind.Info, 1.5f));

            tab.AddSeparator();
            tab.AddHeader("Progress bar");
            _progress = tab.AddProgressBar(0f, "0%");
            _progressValue = 0f;
            // ctx.Timers.Every runs a callback at an interval (unscaled time by default). 10 updates a second is
            // plenty for a bar and costs nothing per frame. We cancel it when the window is rebuilt.
            _progressTimer = _ctx.Timers.Every(0.1f, () =>
            {
                if (_win == null || !_win.IsOpen) return;   // nothing to animate while hidden
                _progressValue += 0.02f;
                if (_progressValue > 1f) _progressValue = 0f;
                _progress.Value = _progressValue;
                _progress.Text = $"{_progressValue * 100f:0}%";
            });
        }

        // ---- Tab 3: Layout -------------------------------------------------------------------------------------

        private void BuildLayoutTab(IUiContainer tab)
        {
            tab.AddHeader("Row and column");
            var row = tab.AddRow();                       // children side by side
            for (int i = 1; i <= 3; i++) row.AddButton("Row " + i, () => { }, UiButtonStyle.Secondary);
            var col = tab.AddColumn();                    // children stacked
            col.AddLabel("Column item A");
            col.AddLabel("Column item B");

            tab.AddSpacer(8f);                            // empty vertical space

            tab.AddHeader("Grid");
            var grid = tab.AddGrid(4, 150f, 44f);
            for (int i = 1; i <= 8; i++) grid.AddButton("Cell " + i, () => { }, UiButtonStyle.Outline);

            tab.AddSeparator();                           // thin divider line

            tab.AddHeader("Group");
            var group = tab.AddGroup("A boxed group");    // a panel with an optional title
            group.AddLabel("Groups hold any widgets.");
            group.AddToggle("Inside the group", true, v => { });

            tab.AddHeader("Scroll");
            var scroll = tab.AddScroll(120f);             // fixed height; mouse wheel scrolls
            for (int i = 1; i <= 12; i++) scroll.AddLabel("Scrollable line " + i);

            tab.AddHeader("Live label");
            // Re-evaluated while visible. Use it for anything that changes: clocks, counters, status.
            tab.AddLiveLabel(() => "Time: " + DateTime.Now.ToString("HH:mm:ss"), 0.25f);
        }

        // ---- Tab 4: Dialogs ------------------------------------------------------------------------------------

        private void BuildDialogsTab(IUiContainer tab)
        {
            tab.AddHeader("Toasts");
            tab.AddLabel("Short notifications stacked in the corner.", UiTextStyle.Muted);
            var row = tab.AddRow();
            row.AddButton("Info", () => _ctx.Ui.Toast("This is information", UiToastKind.Info), UiButtonStyle.Primary);
            row.AddButton("Success", () => _ctx.Ui.Toast("It worked!", UiToastKind.Success), UiButtonStyle.Success);
            row.AddButton("Warning", () => _ctx.Ui.Toast("Careful now", UiToastKind.Warning, 5f), UiButtonStyle.Warning);
            row.AddButton("Error", () => _ctx.Ui.Toast("Something failed", UiToastKind.Error), UiButtonStyle.Danger);

            tab.AddSeparator();
            tab.AddHeader("Modal dialogs");
            var dlg = tab.AddRow();

            // Confirm: exactly one of the callbacks runs. Enter = yes, Escape = no.
            dlg.AddButton("Confirm", () => _ctx.Ui.Confirm("Question", _ctx.Loc.Get("dialog.question"),
                onYes: () => _ctx.Ui.Toast(_ctx.Loc.Get("dialog.thanks"), UiToastKind.Success),
                onNo: () => _ctx.Ui.Toast("Aw.", UiToastKind.Info),
                yesText: "Love it", noText: "Meh"));

            // Prompt: a text field. onSubmit gets the text, onCancel is optional.
            dlg.AddButton("Prompt", () => _ctx.Ui.Prompt("Name", "What should we call you?", "Traveller",
                name => _ctx.Ui.Toast("Hello, " + name + "!", UiToastKind.Success),
                () => _ctx.Ui.Toast("Cancelled", UiToastKind.Warning)));

            // Alert: a message with an OK button.
            dlg.AddButton("Alert", () => _ctx.Ui.Alert("Heads up", "This is an alert. Press OK.",
                () => _ctx.Log.Info("alert closed")));
        }

        // ---- Tab 5: Themes -------------------------------------------------------------------------------------

        private void BuildThemesTab(IUiContainer tab)
        {
            tab.AddHeader("Themes");
            tab.AddLabel("Switching sets ctx.Ui.Theme and rebuilds this window with it.", UiTextStyle.Muted);
            tab.AddLiveLabel(() => "Current: " + _theme.Value, 0.5f);

            var row = tab.AddRow();
            foreach (ThemePreset preset in Enum.GetValues(typeof(ThemePreset)))
            {
                var p = preset;
                // The config entry's Changed handler (in UiShowcaseMod) applies the theme and reopens the window.
                // We defer one frame: rebuilding destroys this very button, so not from inside its own click.
                row.AddButton(p.ToString(), () => _ctx.Timers.NextFrame(() => _theme.Value = p),
                    UiButtonStyle.Pill(p == _theme.Value ? UiVariant.Success : UiVariant.Secondary));
            }

            tab.AddSeparator();
            tab.AddHeader("Custom theme");
            tab.AddLabel("Themes are plain data: start from a preset and change colours.", UiTextStyle.Muted);
            tab.AddButton("Pink accent (temporary)", () => _ctx.Timers.NextFrame(() =>
            {
                var t = UiTheme.Dark;
                t.Primary = new Color(0.9f, 0.3f, 0.5f);
                t.Accent = new Color(1f, 0.6f, 0.75f);
                _ctx.Ui.Theme = t;
                Rebuild();
            }), UiButtonStyle.Primary);
        }
    }
}
