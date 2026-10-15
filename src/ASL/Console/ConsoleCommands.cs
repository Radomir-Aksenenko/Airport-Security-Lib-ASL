using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ASL.Api;
using ASL.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ASL
{
    /// <summary>ASL's built-in console commands. Registered once at boot under the owner id "asl" (bare names only).</summary>
    internal static class ConsoleCommands
    {
        private static CommandRegistry _reg;

        public static void RegisterAll(CommandRegistry reg)
        {
            _reg = reg;
            reg.RegisterBuiltin("help", "help [command] - list commands, or describe one", func: Help);
            reg.RegisterBuiltin("clear", "clear the console", _ => reg.ClearBuffer());
            reg.RegisterBuiltin("echo", "echo <text> - print text", func: a => string.Join(" ", a));
            reg.RegisterBuiltin("asl", "ASL version and counts", func: _ => AslSummary());
            reg.RegisterBuiltin("mods", "list every mod: id, version, kind, state", func: _ => ModsTable());
            reg.RegisterBuiltin("mod", "mod <id> - details of one mod", func: ModDetails);
            reg.RegisterBuiltin("reload", "reload <id> - hot reload a script mod", func: Reload);
            reg.RegisterBuiltin("enable", "enable <id> - enable a mod (applies after restart)", func: a => SetEnabled(a, true));
            reg.RegisterBuiltin("disable", "disable <id> - disable a mod (applies after restart)", func: a => SetEnabled(a, false));
            reg.RegisterBuiltin("scene", "name of the active scene", func: _ => "Active scene: " + SceneManager.GetActiveScene().name);
            reg.RegisterBuiltin("fps", "frames per second and frame time", func: _ => $"{Profiler.Fps:0.0} fps ({Profiler.FrameTimeMs:0.00} ms/frame)");
            reg.RegisterBuiltin("perf", "per-mod Update/timer cost over the last second", func: _ => Perf());
            reg.RegisterBuiltin("players", "list connected players", func: _ => Players());
            reg.RegisterBuiltin("net", "network role: host / client / connected", func: _ => Net());
            reg.RegisterBuiltin("config", "config <modId> [section.key [value]] - list, read or set a mod setting", func: Config);
            reg.RegisterBuiltin("toast", "toast <text> - show a toast", func: a => { DevConsole.Ui.Toast(string.Join(" ", a)); return null; });
            reg.RegisterBuiltin("announce", "announce <text> - show the game's announcement banner", func: a => { DevConsole.Ui.Announce(string.Join(" ", a)); return null; });
            reg.RegisterBuiltin("ui.demo", "open the UI toolkit demo window", func: _ => { UiDemo.Open(DevConsole.Ui); return null; });
            reg.RegisterBuiltin("windows", "list open toolkit windows", func: _ => Windows());
            reg.RegisterBuiltin("timescale", "timescale [value] - show or set Time.timeScale", func: TimeScale);
            reg.RegisterBuiltin("log.mirror", "log.mirror on|off - show BepInEx log lines here", func: LogMirror);
            reg.RegisterBuiltin("history", "list previously typed lines", func: _ => HistoryList());
        }

        // ---- help ---------------------------------------------------------------------------------

        private static string Help(string[] a)
        {
            if (a.Length > 0)
            {
                var c = _reg.Find(a[0]);
                if (c == null) return Err($"No command '{a[0]}'.");
                var sb = new StringBuilder();
                sb.Append(CommandRegistry.DisplayName(c)).Append(" - ").Append(c.Description);
                if (c.Owner != CommandRegistry.BuiltinOwner) sb.Append("\n  from mod '").Append(c.Owner).Append("'");
                if (c.Qualified != null) sb.Append("\n  also: ").Append(c.Qualified);
                return sb.ToString();
            }
            var all = _reg.Unique();
            var o = new StringBuilder("Commands (help <command> for details):");
            int w = 0;
            foreach (var c in all) w = Math.Max(w, CommandRegistry.DisplayName(c).Length);
            foreach (var c in all)
                o.Append('\n').Append("  ").Append(CommandRegistry.DisplayName(c).PadRight(w)).Append("  ").Append(c.Description);
            return o.ToString();
        }

        // ---- mods ---------------------------------------------------------------------------------

        private static ModInfo FindMod(string id, out string error)
        {
            error = null;
            var reg = AslPlugin.Registry;
            var m = reg?.Find(id);
            if (m != null) return m;
            // forgiving: case-insensitive id, or a unique suffix / short id
            ModInfo hit = null;
            int n = 0;
            if (reg != null)
                foreach (var x in reg.Mods)
                    if (string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) || (x.Id != null && x.Id.EndsWith("." + id, StringComparison.OrdinalIgnoreCase)))
                    { hit = x; n++; }
            if (n == 1) return hit;
            error = n > 1 ? $"'{id}' is ambiguous; use the full mod id (see mods)." : $"No mod '{id}' (see mods).";
            return null;
        }

        private static string ModsTable()
        {
            var mods = AslPlugin.Registry?.Mods;
            if (mods == null || mods.Count == 0) return "No mods.";
            int wi = 2, wv = 7, wk = 4;
            foreach (var m in mods) { wi = Math.Max(wi, (m.Id ?? "").Length); wv = Math.Max(wv, (m.Version ?? "").Length); wk = Math.Max(wk, (m.Kind ?? "").Length); }
            var sb = new StringBuilder();
            sb.Append("id".PadRight(wi)).Append("  ").Append("version".PadRight(wv)).Append("  ").Append("kind".PadRight(wk)).Append("  state");
            foreach (var m in mods)
            {
                string state = m.State.ToString();
                if (m.State == ModState.Loaded) state = ConsoleText.Color("7ee787", state);
                else if (m.State == ModState.Failed || m.State == ModState.Incompatible || m.State == ModState.MissingDependency) state = ConsoleText.Color(CommandRegistry.ErrorColor, state);
                else state = ConsoleText.Color(CommandRegistry.DimColor, state);
                sb.Append('\n').Append((m.Id ?? "").PadRight(wi)).Append("  ").Append((m.Version ?? "").PadRight(wv)).Append("  ").Append((m.Kind ?? "").PadRight(wk)).Append("  ").Append(state);
            }
            return sb.ToString();
        }

        private static string ModDetails(string[] a)
        {
            if (a.Length < 1) return "Usage: mod <id>";
            var m = FindMod(a[0], out var err);
            if (m == null) return Err(err);
            var sb = new StringBuilder();
            sb.Append(m.Name).Append("  (").Append(m.Id).Append(")");
            sb.Append("\n  version : ").Append(m.Version);
            sb.Append("\n  kind    : ").Append(m.Kind);
            sb.Append("\n  state   : ").Append(m.State);
            if (!string.IsNullOrEmpty(m.Error) && m.State != ModState.Loaded) sb.Append("\n  ").Append(ConsoleText.Color(CommandRegistry.ErrorColor, "error   : " + ConsoleText.Literal(m.Error)));
            if (!string.IsNullOrEmpty(m.Author)) sb.Append("\n  author  : ").Append(ConsoleText.Literal(m.Author));
            if (!string.IsNullOrEmpty(m.Description)) sb.Append("\n  about   : ").Append(ConsoleText.Literal(m.Description));
            sb.Append("\n  deps    : ").Append(m.Dependencies != null && m.Dependencies.Count > 0 ? string.Join(", ", m.Dependencies) : "(none)");
            if (m.Tags != null && m.Tags.Count > 0) sb.Append("\n  tags    : ").Append(string.Join(", ", m.Tags));
            sb.Append("\n  folder  : ").Append(ConsoleText.Literal(m.Directory ?? ""));
            sb.Append("\n  enabled : ").Append(ModStates.IsEnabled(m.Id) ? "yes" : "no");
            if (ModStates.RestartRequired(m.Id)) sb.Append(" (will be ").Append(ModStates.IsEnabledNextLaunch(m.Id) ? "enabled" : "disabled").Append(" after restart)");
            var loaded = AslPlugin.Loader?.FindLoaded(m.Id);
            if (loaded != null) sb.Append("\n  reload  : ").Append(AslPlugin.Loader.CanReload(m.Id) ? "yes (script mod)" : "no");
            return sb.ToString();
        }

        private static string Reload(string[] a)
        {
            if (a.Length < 1) return "Usage: reload <id>";
            var m = FindMod(a[0], out var err);
            if (m == null) return Err(err);
            var loader = AslPlugin.Loader;
            if (loader == null) return Err("Loader is not ready.");
            var (ok, message) = loader.Reload(m.Id);
            return ok ? "Reloaded '" + m.Id + "'." + (string.IsNullOrEmpty(message) ? "" : " " + message) : Err("Reload failed: " + message);
        }

        private static string SetEnabled(string[] a, bool enabled)
        {
            if (a.Length < 1) return "Usage: " + (enabled ? "enable" : "disable") + " <id>";
            var m = FindMod(a[0], out var err);
            if (m == null) return Err(err);
            if (!ModStates.SetEnabled(m.Id, enabled)) return Err("Could not save the mod state file.");
            return $"'{m.Id}' will be {(enabled ? "enabled" : "disabled")} after the next game restart.";
        }

        private static string AslSummary()
        {
            int loaded = 0, failed = 0, disabled = 0, other = 0, total = 0;
            var mods = AslPlugin.Registry?.Mods;
            if (mods != null)
                foreach (var m in mods)
                {
                    total++;
                    switch (m.State)
                    {
                        case ModState.Loaded: loaded++; break;
                        case ModState.Failed: failed++; break;
                        case ModState.Disabled: disabled++; break;
                        default: other++; break;
                    }
                }
            int windows = UiRuntime.OpenWindows.Count + UiRuntime.OpenHuds.Count;
            return $"{AslInfo.Name} v{AslInfo.Version}\n  mods: {total} ({loaded} loaded, {failed} failed, {disabled} disabled, {other} blocked)\n  commands: {_reg.Unique().Count}   open windows: {windows}";
        }

        // ---- perf / net ---------------------------------------------------------------------------

        private static string Perf()
        {
            var list = new List<ProfilerEntry>();
            Profiler.Snapshot(list);
            var sb = new StringBuilder();
            sb.Append($"{Profiler.Fps:0.0} fps, {Profiler.FrameTimeMs:0.00} ms/frame (last {Profiler.LastFrameMs:0.00} ms)");
            if (list.Count == 0) return sb.Append("\n  no code mods").ToString();
            int w = 3;
            foreach (var e in list) w = Math.Max(w, (e.ModId ?? "").Length);
            sb.Append("\n").Append("mod".PadRight(w)).Append("  update    late      timers    active");
            foreach (var e in list)
            {
                sb.Append('\n').Append((e.ModId ?? "").PadRight(w)).Append("  ")
                  .Append((e.UpdateMs.ToString("0.000") + "ms").PadRight(9)).Append(' ')
                  .Append((e.LateUpdateMs.ToString("0.000") + "ms").PadRight(9)).Append(' ')
                  .Append((e.TimerMs.ToString("0.000") + "ms").PadRight(9)).Append(' ')
                  .Append(e.ActiveTimers);
                if (e.Disabled) sb.Append("  ").Append(ConsoleText.Color(CommandRegistry.ErrorColor, "(updates disabled after errors)"));
            }
            return sb.ToString();
        }

        private static string Players()
        {
            var net = AslPlugin.Net;
            if (net == null) return Err("Networking is not ready.");
            var list = net.Players;
            if (list == null || list.Count == 0) return "No players.";
            var sb = new StringBuilder(list.Count + " player(s):");
            foreach (var p in list)
                sb.Append("\n  ").Append(ConsoleText.Literal(p.Name ?? "?")).Append("  conn=").Append(p.ConnectionId).Append("  netId=").Append(p.NetId).Append(p.IsLocal ? "  (you)" : "");
            return sb.ToString();
        }

        private static string Net()
        {
            var n = AslPlugin.Net;
            if (n == null) return Err("Networking is not ready.");
            string role = n.IsHost ? "host" : n.IsServer ? "dedicated server" : n.IsClient ? "client" : "offline";
            return $"role: {role}   server: {n.IsServer}   client: {n.IsClient}   connected: {n.IsConnectedClient}   connections: {n.ConnectionCount}";
        }

        // ---- config -------------------------------------------------------------------------------

        private static string Config(string[] a)
        {
            if (a.Length == 0) return "Usage: config <modId> [section.key [value]]";
            var m = FindMod(a[0], out var err);
            if (m == null) return Err(err);
            var cfg = AslPlugin.Loader?.FindLoaded(m.Id)?.Services?.Config as ModConfig;
            if (cfg == null) return $"'{m.Id}' is not loaded or has no settings.";
            var entries = cfg.Entries;
            if (entries.Count == 0) return $"'{m.Id}' has no settings.";

            if (a.Length == 1)
            {
                var sb = new StringBuilder("Settings of " + m.Id + ":");
                foreach (var e in entries)
                    sb.Append("\n  ").Append(e.Section).Append('.').Append(e.Key).Append(" = ").Append(ConsoleText.Literal(Show(e.BoxedValue)))
                      .Append(ConsoleText.Color(CommandRegistry.DimColor, "  [" + TypeName(e) + (e.HasRange ? " " + e.Min + ".." + e.Max : "") + ", default " + ConsoleText.Literal(Show(e.DefaultBoxed)) + "]"));
                return sb.ToString();
            }

            ConfigEntryInfo entry = null;
            foreach (var e in entries)
                if (string.Equals(e.Section + "." + e.Key, a[1], StringComparison.OrdinalIgnoreCase)) { entry = e; break; }
            if (entry == null) return Err($"No setting '{a[1]}' in '{m.Id}'. Run: config {m.Id}");

            if (a.Length == 2)
                return $"{entry.Section}.{entry.Key} = {ConsoleText.Literal(Show(entry.BoxedValue))}" + (string.IsNullOrEmpty(entry.Description) ? "" : "\n  " + ConsoleText.Literal(entry.Description));

            string text = string.Join(" ", a, 2, a.Length - 2);
            if (!TryParse(entry, text, out var value, out var why)) return Err(why);
            entry.BoxedValue = value;      // BepInEx clamps ranges and saves
            return $"{entry.Section}.{entry.Key} = {ConsoleText.Literal(Show(entry.BoxedValue))}";
        }

        private static string TypeName(ConfigEntryInfo e) => e.IsEnum ? "enum " + string.Join("|", e.EnumNames ?? Array.Empty<string>()) : e.ValueType.Name.ToLowerInvariant();

        private static string Show(object v) => v switch
        {
            null => "(null)",
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(v, CultureInfo.InvariantCulture),
        };

        private static bool TryParse(ConfigEntryInfo e, string s, out object value, out string error)
        {
            value = null; error = null;
            var t = e.ValueType;
            try
            {
                if (t == typeof(string)) { value = s; return true; }
                if (t == typeof(bool))
                {
                    switch (s.Trim().ToLowerInvariant())
                    {
                        case "true": case "on": case "yes": case "1": value = true; return true;
                        case "false": case "off": case "no": case "0": value = false; return true;
                    }
                    error = "Expected true or false.";
                    return false;
                }
                if (t.IsEnum)
                {
                    if (Enum.TryParse(t, s.Trim(), true, out var en) && Enum.IsDefined(t, en)) { value = en; return true; }
                    error = "Expected one of: " + string.Join(", ", Enum.GetNames(t));
                    return false;
                }
                if (t == typeof(int)) { if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; } error = "Expected a whole number."; return false; }
                if (t == typeof(float)) { if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) { value = f; return true; } error = "Expected a number."; return false; }
                if (t == typeof(double)) { if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { value = d; return true; } error = "Expected a number."; return false; }
                value = Convert.ChangeType(s, t, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Cannot set a {t.Name} from '{s}': {ex.Message}";
                return false;
            }
        }

        // ---- windows / timescale / log / history ------------------------------------------------------

        private static string Windows()
        {
            var sb = new StringBuilder();
            int n = 0;
            var wins = UiRuntime.OpenWindows;
            for (int i = wins.Count - 1; i >= 0; i--)    // top first
            {
                var w = wins[i];
                sb.Append(n++ > 0 ? "\n" : "").Append("  ").Append(w.Key).Append("  \"").Append(ConsoleText.Literal(w.Title ?? "")).Append('"').Append(w.IsDialog ? "  (dialog)" : "");
            }
            var huds = UiRuntime.OpenHuds;
            for (int i = 0; i < huds.Count; i++)
                sb.Append(n++ > 0 ? "\n" : "").Append("  ").Append(huds[i].Key).Append("  (hud)");
            return n == 0 ? "No open windows." : n + " open window(s), top first:\n" + sb;
        }

        private static string TimeScale(string[] a)
        {
            if (a.Length == 0) return "timeScale = " + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture);
            if (!float.TryParse(a[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || float.IsNaN(v) || float.IsInfinity(v)) return Err("Expected a number, e.g. timescale 0.5");
            v = Mathf.Clamp(v, 0f, 20f);
            Time.timeScale = v;
            string msg = "timeScale = " + v.ToString("0.###", CultureInfo.InvariantCulture);
            var net = AslPlugin.Net;
            if (net != null && net.IsOnline)
                msg += "\n" + ConsoleText.Color(CommandRegistry.WarnColor, "Warning: in multiplayer this only changes time on THIS machine; other players are not affected and may desync.");
            return msg;
        }

        private static string LogMirror(string[] a)
        {
            if (a.Length == 0) return "log.mirror is " + (DevConsole.Mirroring ? "on" : "off") + ". Usage: log.mirror on|off";
            switch (a[0].ToLowerInvariant())
            {
                case "on": case "true": case "1": DevConsole.SetMirror(true); return "BepInEx log lines are now shown here.";
                case "off": case "false": case "0": DevConsole.SetMirror(false); return "Log mirroring off.";
            }
            return "Usage: log.mirror on|off";
        }

        private static string HistoryList()
        {
            var h = DevConsole.CommandHistory.Items;
            if (h.Count == 0) return "History is empty.";
            var sb = new StringBuilder();
            for (int i = 0; i < h.Count; i++) sb.Append(i > 0 ? "\n" : "").Append(' ').Append((i + 1).ToString().PadLeft(3)).Append("  ").Append(ConsoleText.Literal(h[i]));
            return sb.ToString();
        }

        private static string Err(string s) => ConsoleText.Color(CommandRegistry.ErrorColor, s);
    }
}
