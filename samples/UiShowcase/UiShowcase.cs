using System;
using ASL.Api;
using UnityEngine;

namespace UiShowcase
{
    /// <summary>The theme presets offered in the config. An enum config entry becomes a dropdown in the F8 menu.</summary>
    public enum ThemePreset { Default, Dark, Light, Game, HighContrast }

    /// <summary>
    /// UI Showcase: a guided tour of the ASL 2.0 UI toolkit, written to be read top to bottom.
    ///
    /// The mod is split in a few small files:
    ///   UiShowcase.cs     (this file) wires everything up in OnLoad: config, storage, keybind, commands, menu.
    ///   ShowcaseWindow.cs the big tabbed window (Buttons / Inputs / Layout / Dialogs / Themes).
    ///   ShowcaseHud.cs    the small always-on HUD in the top-right corner.
    ///
    /// Keys: F7 toggles the window, F8 is ASL's own mod menu (this mod adds a page and a Settings section there).
    /// Console: "showcase" opens the window, "showcase.toast hello" pops a toast.
    /// </summary>
    public sealed class UiShowcaseMod : AslMod
    {
        private IModContext _ctx;
        private ShowcaseWindow _window;
        private ShowcaseHud _hud;

        // Typed, persisted settings (BepInEx .cfg). Each one shows up automatically on this mod's Settings page
        // in the F8 menu because we leave ctx.Config.ShowInMenu = true.
        private IConfigEntry<bool> _showHud;
        private IConfigEntry<float> _hudScale;
        private IConfigEntry<ThemePreset> _theme;

        private int _openCount;

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;

            // ---- Config -------------------------------------------------------------------------------------
            ctx.Config.ShowInMenu = true;   // true is the default; written out to show the switch exists
            _showHud = ctx.Config.Bind("HUD", "ShowHud", true, "Show the small FPS/time overlay in the top-right corner.");
            _hudScale = ctx.Config.BindRange("HUD", "HudScale", 1f, 0.5f, 2f, "Text size multiplier of the overlay.");
            _theme = ctx.Config.Bind("Look", "Theme", ThemePreset.Dark, "Theme preset used by the showcase windows.");

            // Entries raise Changed when the player edits them in the F8 Settings page (or the .cfg file is reloaded).
            _showHud.Changed += _ => _hud?.Apply();
            _hudScale.Changed += _ => _hud?.Apply();
            _theme.Changed += _ => ApplyTheme();

            // ---- Storage ------------------------------------------------------------------------------------
            // Per-mod JSON save data that survives mod updates. Load<T> returns the fallback when nothing is saved yet.
            _openCount = ctx.Storage.Load("stats", new Stats()).Opens;

            // ---- Theme + UI ---------------------------------------------------------------------------------
            // ctx.Ui.Theme is the default for every window this mod creates afterwards.
            ctx.Ui.Theme = ToTheme(_theme.Value);

            // The toggle key: RegisterKey gives a rebindable, conflict-checked keybind (also listed in Settings).
            var key = ctx.Input.RegisterKey("window", "Toggle UI Showcase", KeyCode.F7);

            _hud = new ShowcaseHud(ctx, _showHud, _hudScale);
            _window = new ShowcaseWindow(ctx, key, _theme, OnWindowOpened, () => _openCount);

            _hud.Build();
            _window.Build();

            // Windows and HUDs are rebuilt when the language changes so titles/labels pick up the new strings.
            ctx.Loc.LanguageChanged += () => { _hud.Build(); _window.Rebuild(); };

            RegisterCommands(ctx);
            RegisterMenu(ctx, key);

            ctx.Log.Info($"UI Showcase loaded (language '{ctx.Loc.Language}'). Press F7.");
        }

        public override void OnUnload()
        {
            // Timers/windows/commands are cleaned up by ASL when the mod unloads; saving is the one thing we do by hand.
            SaveStats();
        }

        // ---------------------------------------------------------------------------------------------------

        /// <summary>Called by the window each time it opens: count it, save it, tell other mods.</summary>
        private void OnWindowOpened()
        {
            _openCount++;
            SaveStats();

            // The cross-mod message bus: any other mod can ctx.Mods.Subscribe("uishowcase.opened", payload => ...).
            _ctx.Mods.Publish("uishowcase.opened", _openCount);
        }

        private void SaveStats() => _ctx.Storage.Save("stats", new Stats { Opens = _openCount });

        /// <summary>Plain data class: Storage serializes any JSON-friendly type.</summary>
        public sealed class Stats { public int Opens { get; set; } }

        private void ApplyTheme()
        {
            _ctx.Ui.Theme = ToTheme(_theme.Value);
            // Themes are applied when a window is created, so rebuild (and re-show if it was open).
            _window.Rebuild();
            _hud.Build();
        }

        internal static UiTheme ToTheme(ThemePreset preset)
        {
            // Every preset getter returns a fresh copy, so tweaking one is safe.
            switch (preset)
            {
                case ThemePreset.Dark: return UiTheme.Dark;
                case ThemePreset.Light: return UiTheme.Light;
                case ThemePreset.Game: return UiTheme.Game;
                case ThemePreset.HighContrast: return UiTheme.HighContrast;
                default: return UiTheme.Default;
            }
        }

        // ---- Commands -----------------------------------------------------------------------------------------

        private void RegisterCommands(IModContext ctx)
        {
            // Commands run from the dev console. Dots are fine in names; they are just a naming convention.
            ctx.Commands.Register("showcase", "Open the UI Showcase window", args => _window.Open());

            // The Func<string[], string> overload returns text that the console prints.
            ctx.Commands.Register("showcase.toast", "showcase.toast <text>: pop a toast", (Func<string[], string>)(args =>
            {
                if (args == null || args.Length == 0) return "usage: showcase.toast <text>";
                ctx.Ui.Toast(string.Join(" ", args), UiToastKind.Info);
                return "toast shown";
            }));
        }

        // ---- F8 menu ------------------------------------------------------------------------------------------

        private void RegisterMenu(IModContext ctx, IAslKeybind key)
        {
            // A plain button in this mod's section of the F8 menu. We go through _window.Open() (not a window
            // reference) because the showcase window is rebuilt when the theme changes.
            ctx.Menu.AddButton(ctx.Loc.Get("menu.open"), () => _window.Open());

            // AddWindowButton opens a specific window object. This tiny "About" window is created once and never
            // rebuilt, so holding the reference is safe.
            var about = ctx.Ui.CreateWindow("about", ctx.Info.Name, new UiWindowOptions { Width = 420 });
            about.AddLabel($"{ctx.Info.Name} v{ctx.Info.Version} by {ctx.Info.Author}");
            about.AddLabel(ctx.Info.Description, UiTextStyle.Muted);
            about.AddButton("Close", () => about.Close(), UiButtonStyle.Secondary);
            ctx.Menu.AddWindowButton("About", about);

            ctx.Menu.AddSeparator("More");

            // A whole page of widgets. The callback receives a container: the same Add* calls as on a window.
            ctx.Menu.AddPage(ctx.Loc.Get("menu.page"), page =>
            {
                page.AddHeader("Quick page");
                page.AddLabel("Built with ctx.Menu.AddPage. Same widgets, same API.", UiTextStyle.Muted);
                page.AddKeybind(key);
                page.AddToggle("Show HUD", _showHud.Value, v => _showHud.Value = v, UiToggleStyle.Checkbox);
                page.AddButton("Say hi", () => ctx.Ui.Toast("Hi from the menu page!", UiToastKind.Success),
                    UiButtonStyle.Pill(UiVariant.Success));
            });
        }
    }
}
