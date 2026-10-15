using System;
using System.Collections.Generic;
using ASL.Api;

namespace ASL
{
    /// <summary>
    /// The F8 "ASL" page: framework version, live FPS / frame time, the per-mod cost from <see cref="Profiler"/>
    /// (live labels, refreshed every 0.5 s and only while the page is on screen), the UI demo, a reset for
    /// remembered window positions and pointers to the documentation.
    /// </summary>
    internal static class UiAslPage
    {
        private const float Refresh = 0.5f;
        private static ModUi _ui;

        /// <summary>ASL's own UI handle (used for the demo window); mods never see it.</summary>
        private static ModUi Ui => _ui ??= new ModUi("asl", "ASL", null, AslPlugin.UiApi, AslPlugin.Logger);

        public static void Build(IUiContainer page)
        {
            page.AddLabel("ASL - Airport Security Lib   v" + AslInfo.Version, UiTextStyle.Body);
            page.AddLiveLabel(() => "FPS " + Profiler.Fps.ToString("0.0") + "     frame " + Profiler.FrameTimeMs.ToString("0.00") + " ms", Refresh);
            page.AddSeparator();

            // ---- console keybind (rebindable here, like any mod keybind) ---------------------------
            var consoleKey = DevConsole.Bind;
            if (consoleKey != null) page.AddKeybind(consoleKey);

            // ---- per-mod cost ----------------------------------------------------------------------
            page.AddLabel("Mod cost per frame (average over the last second)", UiTextStyle.Muted);
            var ids = new List<ProfilerEntry>();
            Profiler.Snapshot(ids);
            if (ids.Count == 0) page.AddLabel("(no code mods loaded)", UiTextStyle.Muted);
            foreach (var entry in ids)
            {
                string id = entry.ModId;
                string name = AslPlugin.Registry?.Find(id)?.Name ?? id;
                var scratch = new List<ProfilerEntry>(ids.Count);   // one per label: the live label re-reads the snapshot into it
                page.AddLiveLabel(() => Line(name, id, scratch), Refresh);
            }
            page.AddSeparator();

            // ---- tools ---------------------------------------------------------------------------------
            var status = page.AddLabel(string.Empty, UiTextStyle.Muted);
            status.Visible = false;

            var row = page.AddRow(12f);
            var demo = row.AddButton("UI demo", () =>
            {
                Ui.Theme = UiTheme.Default;
                UiDemo.Open(Ui);
                AslPlugin.Menu.Visible = false;   // the demo window sits below the F8 menu layer: close the menu so it is visible
            }, new UiButtonStyle { Variant = UiVariant.Primary, Size = UiSize.Medium });
            demo.Tooltip = "Open a window that shows every widget, shape, size and variant of the UI toolkit (closes this menu).";

            var reset = row.AddButton("Reset window positions", () =>
            {
                bool ok = UiWindowStore.Reset();
                status.Text = ok ? "Remembered window positions cleared; each window uses its default place the next time it is created." : "Could not clear the saved positions (see the BepInEx log).";
                status.Visible = true;
            }, new UiButtonStyle { Variant = UiVariant.Secondary, Size = UiSize.Medium });
            reset.Tooltip = "Forget where you dragged mod windows (BepInEx/config/ASL.Windows.json).";

            page.AddSeparator();

            // ---- about -----------------------------------------------------------------------------------
            page.AddLabel("Documentation: README.md and the docs/ folder of the ASL repository", UiTextStyle.Muted);
            page.AddLabel("https://github.com/Radomir-Aksenenko/Airport-Security-Lib-ASL", UiTextStyle.Muted);
            page.AddLabel("Mods folder: mods/ next to the game. Log: BepInEx/LogOutput.log.", UiTextStyle.Muted);
        }

        // Runs only every Refresh seconds while the page is visible; allocates a string, no more.
        private static string Line(string name, string id, List<ProfilerEntry> scratch)
        {
            Profiler.Snapshot(scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                var e = scratch[i];
                if (!string.Equals(e.ModId, id, StringComparison.Ordinal)) continue;
                string s = name + ":  update " + e.UpdateMs.ToString("0.000") + " ms   late " + e.LateUpdateMs.ToString("0.000")
                           + " ms   timers " + e.TimerMs.ToString("0.000") + " ms (" + e.ActiveTimers + " active)";
                if (e.Disabled) s += "   [handlers switched off after errors]";
                return s;
            }
            return name + ":  -";
        }
    }
}
