using System;
using System.Collections.Generic;
using ASL.Api;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// The F8 "Mod Manager" page (built with the toolkit): a summary line, then one box per mod with its name,
    /// version, author, kind, a coloured state badge, the error (if any), description and dependencies, plus
    /// Enable / Disable (persisted in <see cref="ModStates"/>, applies next launch) and, for script mods, Reload.
    /// All state is read once when the page opens and refreshed in place after an action, so it costs nothing per frame.
    /// </summary>
    internal static class UiModManagerPage
    {
        public static void Build(IUiContainer page)
        {
            var reg = AslPlugin.Registry;
            var theme = UiTheme.Game;
            var owner = (page as UiElement)?.Host.Owner;

            var mods = new List<ModInfo>();
            if (reg != null)
                foreach (var m in reg.Mods)
                    if (m != null && m.Id != AslInfo.Guid) mods.Add(m);   // ASL itself is not a mod

            var summary = page.AddLabel(Summary(mods), UiTextStyle.Body);
            var status = page.AddLabel(string.Empty, UiTextStyle.Body);
            status.Color = theme.Warning;
            status.Visible = false;

            if (mods.Count == 0)
            {
                page.AddLabel("(no mods installed - drop a mod folder into the game's mods/ directory)", UiTextStyle.Muted);
                return;
            }

            void Notify(string text, UiToastKind kind)
            {
                status.Text = text;
                status.Color = kind == UiToastKind.Error ? theme.Danger : kind == UiToastKind.Success ? theme.Success : theme.Warning;
                status.Visible = true;
                // The F8 menu sits above the toast layer, so the page also shows the message itself; the toast is for when it is closed.
                if (owner != null) UiToasts.Show(owner, theme, text, kind, 4f);
            }

            foreach (var mod in mods)
            {
                try { AddMod(page, mod, theme, owner, summary, mods, Notify); }
                catch (Exception ex) { owner?.Report("mod manager row for " + mod.Id, ex); }
            }
        }

        private static void AddMod(IUiContainer page, ModInfo m, UiTheme theme, UiOwner owner, IUiText summary,
                                   List<ModInfo> all, Action<string, UiToastKind> notify)
        {
            string id = m.Id;
            string name = string.IsNullOrWhiteSpace(m.Name) ? id : m.Name;
            var box = page.AddGroup();

            string head = name + "   v" + (m.Version ?? "?");
            if (!string.IsNullOrWhiteSpace(m.Author)) head += "   by " + m.Author;
            box.AddLabel(head, UiTextStyle.Body);

            var info = box.AddRow(14f);
            info.AddLabel((m.Kind ?? "?") + " mod", UiTextStyle.Muted);
            var badge = info.AddLabel(string.Empty, UiTextStyle.Body);

            var error = box.AddLabel(string.Empty, UiTextStyle.Body);
            error.Color = theme.Danger;

            if (!string.IsNullOrWhiteSpace(m.Description)) box.AddLabel(m.Description, UiTextStyle.Muted);
            if (m.Dependencies != null && m.Dependencies.Count > 0)
                box.AddLabel("Requires: " + string.Join(", ", m.Dependencies), UiTextStyle.Muted);

            var restart = box.AddLabel(string.Empty, UiTextStyle.Body);
            restart.Color = theme.Warning;

            var buttons = box.AddRow(10f);
            var toggle = buttons.AddButton(string.Empty, null, new UiButtonStyle { Variant = UiVariant.Secondary, Size = UiSize.Small, Width = 170f });
            IUiButton reload = null;
            bool canReload = string.Equals(m.Kind, "script", StringComparison.Ordinal) && AslPlugin.Loader != null && AslPlugin.Loader.CanReload(id);
            if (canReload)
                reload = buttons.AddButton("Reload", null, new UiButtonStyle { Variant = UiVariant.Secondary, Size = UiSize.Small, Width = 170f });

            void Refresh()
            {
                badge.Text = "[" + StateText(m.State) + "]";
                badge.Color = StateColor(theme, m.State);

                bool hasError = !string.IsNullOrWhiteSpace(m.Error) && m.State != ModState.Loaded;
                error.Text = hasError ? m.Error : string.Empty;
                error.Visible = hasError;

                bool next = ModStates.IsEnabledNextLaunch(id);
                toggle.Text = next ? "Disable" : "Enable";
                bool pending = ModStates.RestartRequired(id);
                restart.Text = pending ? (next ? "Will be enabled after restarting the game." : "Will be disabled after restarting the game.") : string.Empty;
                restart.Visible = pending;
                summary.Text = Summary(all);
            }

            toggle.Clicked += () =>
            {
                bool target = !ModStates.IsEnabledNextLaunch(id);
                bool saved = ModStates.SetEnabled(id, target);
                Refresh();
                if (!saved) notify(name + ": could not save the setting (see the BepInEx log).", UiToastKind.Error);
                else notify(name + (target ? " enabled" : " disabled") + " - restart required to apply.", UiToastKind.Warning);
            };

            if (reload != null)
            {
                reload.Clicked += () =>
                {
                    var r = AslPlugin.Loader.Reload(id);
                    Refresh();
                    notify(r.ok ? "Reloaded " + name + "." : "Reload of " + name + " failed: " + r.message, r.ok ? UiToastKind.Success : UiToastKind.Error);
                };
            }

            Refresh();
        }

        // ---- text / colours --------------------------------------------------------------------

        private static string Summary(List<ModInfo> mods)
        {
            int loaded = 0, failed = 0, disabled = 0, missing = 0, incompatible = 0;
            foreach (var m in mods)
            {
                switch (m.State)
                {
                    case ModState.Loaded: loaded++; break;
                    case ModState.Failed: failed++; break;
                    case ModState.Disabled: disabled++; break;
                    case ModState.MissingDependency: missing++; break;
                    case ModState.Incompatible: incompatible++; break;
                }
            }
            string s = loaded + " loaded, " + failed + " failed, " + disabled + " disabled";
            if (missing > 0) s += ", " + missing + " missing dependencies";
            if (incompatible > 0) s += ", " + incompatible + " incompatible";
            return s;
        }

        private static string StateText(ModState s)
        {
            switch (s)
            {
                case ModState.Loaded: return "Loaded";
                case ModState.Failed: return "Failed";
                case ModState.Disabled: return "Disabled";
                case ModState.MissingDependency: return "Missing dependency";
                case ModState.Incompatible: return "Incompatible";
                default: return s.ToString();
            }
        }

        private static Color StateColor(UiTheme t, ModState s)
        {
            switch (s)
            {
                case ModState.Loaded: return t.Success;
                case ModState.Failed: return t.Danger;
                case ModState.Disabled: return t.TextMuted;
                default: return t.Warning;
            }
        }
    }
}
