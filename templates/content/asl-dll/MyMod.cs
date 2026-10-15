using ASL.Api;
using UnityEngine;

namespace MyMod
{
    /// <summary>
    /// Starter ASL 2.0 DLL mod. One small tour of the common surface: a saved setting, a rebindable key
    /// that toggles a window, a toast, a timer and a console command. Replace it with your own code.
    /// </summary>
    public sealed class MyMod : AslMod
    {
        private IConfigEntry<bool> _greetOnStart;
        private IConfigEntry<float> _bannerSeconds;
        private IUiWindow _window;
        private int _secondsLoaded;

        public override void OnLoad(IModContext ctx)
        {
            // Settings: saved to BepInEx/config/<modId>.cfg and listed on the F8 page automatically.
            _greetOnStart = ctx.Config.Bind("General", "GreetOnStart", true, "Show a toast when the mod loads.");
            _bannerSeconds = ctx.Config.BindRange("General", "BannerSeconds", 2.5f, 0.5f, 10f, "How long announcements stay on screen.");

            // Window: built once, hidden until opened, toggled by a rebindable key (change it in F8 > Controls).
            _window = BuildWindow(ctx);
            _window.BindToggleKey(ctx.Input.RegisterKey("window", "Toggle MyMod window", KeyCode.F6));

            // Timer: counts seconds, shown by the window's live label. Cancelled automatically on unload.
            ctx.Timers.Every(1f, () => _secondsLoaded++);

            // Console command: "mymod", "mymod open" or "mymod close" in the console (` key).
            ctx.Commands.Register("mymod", "mymod [open|close|status]", HandleCommand);

            if (_greetOnStart.Value)
                ctx.Ui.Toast("MyMod loaded. Press F6 to open its window.", UiToastKind.Success);

            ctx.Log.Info("MyMod loaded.");
        }

        private IUiWindow BuildWindow(IModContext ctx)
        {
            var win = ctx.Ui.CreateWindow("main", "MyMod", new UiWindowOptions { Width = 520 });

            win.AddHeader("Hello from MyMod");
            win.AddLabel("This window, its key and its settings are all wired by ASL.", UiTextStyle.Muted);
            win.AddLiveLabel(() => "Seconds since load: " + _secondsLoaded);

            var row = win.AddRow();
            row.AddButton("Say hi", () => ctx.Ui.Toast("Hi from MyMod!", UiToastKind.Success), UiButtonStyle.Pill(UiVariant.Success));
            row.AddButton("Announce", () => ctx.Ui.Announce("MyMod says hi", _bannerSeconds.Value), UiButtonStyle.Secondary);
            row.AddButton("Close", () => win.Close(), UiButtonStyle.Ghost);

            win.AddSeparator();
            win.AddToggle("Greet on load", _greetOnStart.Value, on => _greetOnStart.Value = on);
            win.AddSlider("Banner seconds", 0.5f, 10f, _bannerSeconds.Value, v => _bannerSeconds.Value = v, 0.5f, "0.0");

            return win;
        }

        private string HandleCommand(string[] args)
        {
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (sub)
            {
                case "open":
                    _window.Open();
                    return "MyMod window opened.";
                case "close":
                    _window.Close();
                    return "MyMod window closed.";
                default:
                    return $"MyMod: window {(_window.IsOpen ? "open" : "closed")}, {_secondsLoaded}s since load.";
            }
        }
    }
}
