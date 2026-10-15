using System;
using ASL.Api;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Self-demo of the UI toolkit (wired to the <c>ui.demo</c> console command by the console work): one tabbed
    /// window showing every widget, every shape x size x variant, layout containers, theme switching, toasts,
    /// dialogs and a HUD. Built only from the public <see cref="IAslUi"/> API (plus the procedural sprites for
    /// the image demo), so it doubles as a worked example.
    /// </summary>
    internal static class UiDemo
    {
        private const string WindowId = "ui_demo";
        private const string HudId = "ui_demo_hud";
        private static readonly string[] ThemeNames = { "Default", "Dark", "Light", "Game", "HighContrast" };

        private static UiTheme ThemeByName(string n)
        {
            switch (n)
            {
                case "Dark": return UiTheme.Dark;
                case "Light": return UiTheme.Light;
                case "Game": return UiTheme.Game;
                case "HighContrast": return UiTheme.HighContrast;
                default: return UiTheme.Default;
            }
        }

        /// <summary>Builds (or rebuilds) the demo window and opens it.</summary>
        public static void Open(IAslUi ui) => Open(ui, 0, 0);

        private static void Open(IAslUi ui, int themeIndex, int tab)
        {
            var win = ui.CreateWindow(WindowId, "ASL UI toolkit demo", new UiWindowOptions { Width = 900f, Anchor = UiAnchor.Center });

            // ---- header: theme switcher -------------------------------------------------------------
            var top = win.AddRow(12f);
            top.AddLabel("Theme").SetSize(90f, 0f);
            var themeDrop = top.AddDropdown(null, ThemeNames, themeIndex, i =>
            {
                ui.Theme = ThemeByName(ThemeNames[i]);
                Open(ui, i, ((IUiTabs)win.FindById("tabs")).SelectedIndex);   // new windows use the new theme: rebuild
            });
            themeDrop.Tooltip = "Windows take their theme when created, so the demo rebuilds itself.";
            win.AddSeparator();

            var tabs = win.AddTabs();
            tabs.Id = "tabs";
            BuildWidgets(ui, tabs.AddTab("Widgets"));
            BuildButtons(tabs.AddTab("Buttons"));
            BuildLayout(tabs.AddTab("Layout"));
            BuildFeedback(ui, tabs.AddTab("Feedback"));
            tabs.SelectedIndex = tab;

            win.Open();
        }

        // ---------------------------------------------------------------------------------------------

        private static void BuildWidgets(IAslUi ui, IUiContainer p)
        {
            p.AddHeader("Text");
            p.AddLabel("Title style", UiTextStyle.Title);
            p.AddLabel("Header style", UiTextStyle.Header);
            p.AddLabel("Body style: the quick brown fox jumps over the lazy dog.");
            p.AddLabel("Small style", UiTextStyle.Small);
            p.AddLabel("Muted style", UiTextStyle.Muted);
            p.AddLabel("Mono style  0123456789", UiTextStyle.Mono);
            p.AddLiveLabel(() => "Live label (0.25 s refresh): t = " + Time.unscaledTime.ToString("0.00"), 0.25f);
            p.AddSeparator();

            p.AddHeader("Toggles");
            var tr = p.AddRow(24f);
            tr.AddToggle("Switch", true, v => ui.Toast("Switch: " + v), UiToggleStyle.Switch).Tooltip = "Switch style";
            tr.AddToggle("Checkbox", false, v => ui.Toast("Checkbox: " + v), UiToggleStyle.Checkbox);
            tr.AddToggle("Button style", false, v => ui.Toast("Button toggle: " + v), UiToggleStyle.Button);
            p.AddSeparator();

            p.AddHeader("Slider, input, dropdown, progress");
            p.AddSlider("Continuous", 0f, 1f, 0.5f, v => { }, 0f, "0.00");
            p.AddSlider("Stepped (0.5)", 0f, 5f, 2f, v => { }, 0.5f, "0.0");
            p.AddSlider("Percent", 0f, 1f, 0.25f, v => { }, 0.05f, "0%");
            p.AddInput("Type something and press Enter...", "", s => ui.Toast("Submitted: " + s, UiToastKind.Success), null);
            p.AddDropdown("Dropdown", new[] { "Alpha", "Beta", "Gamma", "Delta" }, 1, i => ui.Toast("Picked #" + i));
            p.AddDropdown("Long list", MakeNumbers(20), 3, null);
            var prog = p.AddProgressBar(0.35f, "progress");
            p.AddLiveLabel(() =>
            {
                float v = (Time.unscaledTime * 0.15f) % 1f;
                prog.Value = v;
                prog.Text = (v * 100f).ToString("0") + "%";
                return "Progress bar is driven by a live label";
            }, 0.1f);
            p.AddImage(UiShapes.Circle(), 64f, 64f);
        }

        private static string[] MakeNumbers(int n)
        {
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = "Option " + (i + 1);
            return a;
        }

        private static void BuildButtons(IUiContainer p)
        {
            var shapes = (UiShape[])Enum.GetValues(typeof(UiShape));
            var variants = (UiVariant[])Enum.GetValues(typeof(UiVariant));
            var sizes = (UiSize[])Enum.GetValues(typeof(UiSize));

            foreach (var shape in shapes)
            {
                var g = p.AddGroup(shape + " shape");
                bool compact = shape == UiShape.Circle || shape == UiShape.Square;

                // every variant (Medium)
                var grid = g.AddGrid(compact ? 7 : 4, compact ? 64f : 190f, compact ? 64f : 46f, 8f);
                foreach (var v in variants)
                {
                    var st = new UiButtonStyle { Shape = shape, Variant = v, Size = UiSize.Medium };
                    string text = compact ? v.ToString().Substring(0, 1) : v.ToString();
                    var v2 = v;
                    grid.AddButton(text, null, st).Tooltip = shape + " / " + v2;
                }

                // every size (Primary)
                var row = g.AddRow(10f);
                foreach (var s in sizes)
                {
                    var st = new UiButtonStyle { Shape = shape, Variant = UiVariant.Primary, Size = s };
                    row.AddButton(compact ? s.ToString().Substring(0, 1) : s.ToString(), null, st);
                }
            }

            var extra = p.AddGroup("Icon & custom");
            var r = extra.AddRow(10f);
            r.AddIconButton(UiShapes.Cross(), null, 48f, UiShape.Circle);
            r.AddIconButton(UiShapes.Chevron(), null, 48f, UiShape.Square);
            r.AddButton("Custom colours", null, new UiButtonStyle
            {
                Shape = UiShape.Pill, Background = new Color(0.55f, 0.2f, 0.8f, 1f), TextColor = Color.white, Width = 220f,
            });
            r.AddButton("Disabled", null).Interactable = false;
        }

        private static void BuildLayout(IUiContainer p)
        {
            var cols = p.AddRow(12f);
            var left = cols.AddGroup("Column in a group");
            left.AddLabel("Rows, columns, grids,");
            left.AddLabel("groups, scrolls and tabs");
            left.AddLabel("nest freely.");
            left.AddSpacer(10f);
            left.AddButton("Button", null);
            var right = cols.AddGroup("Grid 3 x 2");
            var grid = right.AddGrid(3, 100f, 40f, 6f);
            for (int i = 1; i <= 6; i++) grid.AddButton("Cell " + i, null, UiButtonStyle.Secondary);

            p.AddSpacer(8f);
            p.AddHeader("Scroll view (height 160)");
            var sc = p.AddScroll(160f);
            for (int i = 1; i <= 24; i++) sc.AddLabel("Scrollable line " + i + "  (mouse wheel or drag)");

            p.AddSpacer(8f);
            p.AddHeader("Nested tabs");
            var t = p.AddTabs();
            t.AddTab("One").AddLabel("First nested tab.");
            var two = t.AddTab("Two");
            two.AddLabel("Second nested tab with a toggle:");
            two.AddToggle("Enabled", true, null);
            t.AddTab("Three").AddButton("A button on tab three", null);

            p.AddSpacer(8f);
            var row = p.AddRow(10f);
            row.AddLabel("Flexible spacer ->");
            row.AddSpacer(0f).SetFlexible(1f);
            row.AddButton("Right aligned", null, UiButtonStyle.Success);
        }

        private static void BuildFeedback(IAslUi ui, IUiContainer p)
        {
            p.AddHeader("Toasts");
            var tr = p.AddRow(10f);
            tr.AddButton("Info", () => ui.Toast("Information toast", UiToastKind.Info), UiButtonStyle.Primary);
            tr.AddButton("Success", () => ui.Toast("It worked!", UiToastKind.Success), UiButtonStyle.Success);
            tr.AddButton("Warning", () => ui.Toast("Careful now", UiToastKind.Warning), UiButtonStyle.Warning);
            tr.AddButton("Error", () => ui.Toast("Something broke", UiToastKind.Error, 5f), UiButtonStyle.Danger);
            tr.AddButton("Spam x7", () => { for (int i = 1; i <= 7; i++) ui.Toast("Toast #" + i); }, UiButtonStyle.Secondary);
            p.AddSeparator();

            p.AddHeader("Dialogs (Enter = confirm, Escape = cancel)");
            var dr = p.AddRow(10f);
            dr.AddButton("Confirm", () => ui.Confirm("Delete everything?", "This is only a demo, nothing is deleted.",
                () => ui.Toast("Yes!", UiToastKind.Success), () => ui.Toast("No.", UiToastKind.Warning)));
            dr.AddButton("Prompt", () => ui.Prompt("Your name", "Type a name:", "Player",
                s => ui.Toast("Hello, " + s), () => ui.Toast("Cancelled")));
            dr.AddButton("Alert", () => ui.Alert("Heads up", "Something happened.", () => ui.Toast("Closed alert")));
            p.AddSeparator();

            p.AddHeader("HUD (click-through overlay)");
            var hr = p.AddRow(10f);
            hr.AddButton("Show HUD", () =>
            {
                var hud = ui.GetWindow(HudId);
                if (hud == null)
                {
                    hud = ui.CreateHud(HudId, UiAnchor.TopLeft, new UiWindowOptions { Width = 280f, OffsetX = 20f, OffsetY = -20f });
                    hud.AddLabel("ASL HUD", UiTextStyle.Header);
                    hud.AddLiveLabel(() => "FPS: " + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("0"), 0.5f);
                }
                else hud.Open();
            });
            hr.AddButton("Hide HUD", () => ui.GetWindow(HudId)?.Close(), UiButtonStyle.Secondary);
            p.AddSeparator();

            p.AddHeader("Windows");
            p.AddButton("Open a modal window", () =>
            {
                var m = ui.CreateWindow("ui_demo_modal", "Modal window", new UiWindowOptions { Width = 420f, Modal = true });
                m.AddLabel("A modal window dims everything behind it.");
                m.AddButton("Close", () => m.Close(), UiButtonStyle.Primary);
                m.Open();
            });
        }
    }
}
