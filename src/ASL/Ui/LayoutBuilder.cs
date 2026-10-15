using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ASL.Api;
using ASL.Core;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Turns a parsed <see cref="UiNode"/> tree (see <see cref="LayoutParser"/>) into toolkit calls on a mod's
    /// <see cref="ModUi"/>: one window plus its widgets, with action strings wired (built-ins or named
    /// actions) and <c>"bind": "config:Section.Key"</c> two-way bound to the mod's config. Problems are logged as
    /// warnings and the offending widget is skipped; the build never throws to the caller.
    /// </summary>
    internal sealed class LayoutBuilder
    {
        private readonly ModUi _ui;
        private readonly UiOwner _owner;
        private readonly string _file;
        private UiWindow _win;

        private LayoutBuilder(ModUi ui, string file)
        {
            _ui = ui;
            _owner = ui.Owner;
            _file = file;
        }

        /// <summary>Builds the window described by <paramref name="root"/> (a <see cref="UiNodeType.Window"/> node). Returns null on failure.</summary>
        public static IUiWindow Build(ModUi ui, UiNode root, string file)
        {
            if (ui == null || root == null) return null;
            return new LayoutBuilder(ui, file).BuildWindow(root);
        }

        private void Warn(UiNode n, string msg)
        {
            string where = string.IsNullOrEmpty(n.Path) ? _file : _file + ": " + n.Path;
            _ui.LogWarning($"[{_ui.ModNameValue}] {where}: {msg}");
        }

        // ---- window ------------------------------------------------------------------------------

        private IUiWindow BuildWindow(UiNode root)
        {
            string id = !string.IsNullOrWhiteSpace(root.Id) ? root.Id.Trim() : Path.GetFileNameWithoutExtension(_file);
            if (string.IsNullOrEmpty(id)) id = "layout";

            var opt = new UiWindowOptions();
            if (root.Width.HasValue && root.Width.Value > 0f) opt.Width = root.Width.Value;
            if (root.Height.HasValue && root.Height.Value > 0f) opt.Height = root.Height.Value;
            if (root.Anchor.HasValue) opt.Anchor = root.Anchor.Value;
            if (root.OffsetX.HasValue) opt.OffsetX = root.OffsetX.Value;
            if (root.OffsetY.HasValue) opt.OffsetY = root.OffsetY.Value;
            if (root.Draggable.HasValue) opt.Draggable = root.Draggable.Value;
            if (root.Closable.HasValue) opt.Closable = root.Closable.Value;
            if (root.Modal.HasValue) opt.Modal = root.Modal.Value;
            if (!string.IsNullOrWhiteSpace(root.Theme))
            {
                var theme = ThemeByName(root.Theme);
                if (theme != null) opt.Theme = theme;
                else Warn(root, $"unknown theme '{root.Theme}' (use Default, Dark, Light, Game or HighContrast); using the mod's theme");
            }

            string title = root.Title ?? id;
            var win = _ui.CreateWindow(id, title, opt) as UiWindow;
            if (win == null) return null;
            _win = win;

            BuildChildren(win, root.Children);

            if (!string.IsNullOrWhiteSpace(root.ToggleKey))
            {
                if (TryParseKey(root.ToggleKey, out var key))
                {
                    var bind = _ui.RegisterToggleKey(id, title, key);
                    if (bind != null) win.BindToggleKey(bind);
                }
                else Warn(root, $"toggleKey '{root.ToggleKey}' is not a Unity KeyCode name (e.g. F6, BackQuote, Alpha1); no key bound");
            }

            if (root.Open == true) win.Open();
            return win;
        }

        private static UiTheme ThemeByName(string name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "default": return UiTheme.Default;
                case "dark": return UiTheme.Dark;
                case "light": return UiTheme.Light;
                case "game": return UiTheme.Game;
                case "highcontrast": return UiTheme.HighContrast;
                default: return null;
            }
        }

        private static bool TryParseKey(string s, out KeyCode key)
        {
            key = KeyCode.None;
            s = s.Trim();
            if (s.Length == 0 || !char.IsLetter(s[0])) return false;
            return Enum.TryParse(s, true, out key) && key != KeyCode.None;
        }

        // ---- children ----------------------------------------------------------------------------

        private void BuildChildren(IUiContainer parent, List<UiNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                try
                {
                    var el = BuildNode(parent, n);
                    if (el != null) ApplyCommon(el, n);
                }
                catch (Exception ex) { Warn(n, $"could not build '{n.Type}': {ex.Message}"); }
            }
        }

        private void ApplyCommon(IUiElement el, UiNode n)
        {
            if (n.Id != null) el.Id = n.Id;
            if (!string.IsNullOrEmpty(n.Tooltip)) el.Tooltip = n.Tooltip;
            if (n.Interactable.HasValue) el.Interactable = n.Interactable.Value;
            if (n.Visible.HasValue) el.Visible = n.Visible.Value;
            // Images take width/height as their own sizing; spacers use height as their size.
            if (n.Type != UiNodeType.Image && n.Type != UiNodeType.Spacer && (n.Width.HasValue || n.Height.HasValue))
                el.SetSize(n.Width ?? 0f, n.Height ?? 0f);
        }

        private IUiElement BuildNode(IUiContainer c, UiNode n)
        {
            switch (n.Type)
            {
                case UiNodeType.Header: return c.AddHeader(n.Text ?? string.Empty);
                case UiNodeType.Label: return c.AddLabel(n.Text ?? string.Empty, n.Style ?? UiTextStyle.Body);
                case UiNodeType.Button: return BuildButton(c, n);
                case UiNodeType.Toggle: return BuildToggle(c, n);
                case UiNodeType.Slider: return BuildSlider(c, n);
                case UiNodeType.Input: return BuildInput(c, n);
                case UiNodeType.Dropdown: return BuildDropdown(c, n);
                case UiNodeType.Progress: return c.AddProgressBar(Mathf.Clamp01((float)(n.NumberValue ?? 0.0)), n.Text);
                case UiNodeType.Image: return BuildImage(c, n);
                case UiNodeType.Spacer: return c.AddSpacer(n.Height ?? 12f);
                case UiNodeType.Separator: return c.AddSeparator();

                case UiNodeType.Row: { var r = c.AddRow(n.Spacing ?? -1f); BuildChildren(r, n.Children); return r; }
                case UiNodeType.Column: { var r = c.AddColumn(n.Spacing ?? -1f); BuildChildren(r, n.Children); return r; }
                case UiNodeType.Grid:
                {
                    var g = c.AddGrid(Math.Max(1, n.Columns ?? 2), n.CellWidth ?? 120f, n.CellHeight ?? 40f, n.Spacing ?? -1f);
                    BuildChildren(g, n.Children);
                    return g;
                }
                case UiNodeType.Scroll: { var s = c.AddScroll(n.Height ?? 200f); BuildChildren(s, n.Children); return s; }
                case UiNodeType.Group: { var g = c.AddGroup(n.Title); BuildChildren(g, n.Children); return g; }
                case UiNodeType.Tabs:
                {
                    var tabs = c.AddTabs();
                    foreach (var t in n.Children)
                    {
                        if (t.Type != UiNodeType.Tab) continue;
                        var page = tabs.AddTab(t.Title ?? "Tab");
                        BuildChildren(page, t.Children);
                    }
                    return tabs;
                }
                default:
                    Warn(n, $"'{n.Type}' cannot be used here; skipped");
                    return null;
            }
        }

        // ---- widgets -----------------------------------------------------------------------------

        private IUiElement BuildButton(IUiContainer c, UiNode n)
        {
            UiButtonStyle style = null;
            if (n.Variant.HasValue || n.Shape.HasValue || n.Size.HasValue)
            {
                style = new UiButtonStyle();
                if (n.Variant.HasValue) style.Variant = n.Variant.Value;
                if (n.Shape.HasValue) style.Shape = n.Shape.Value;
                if (n.Size.HasValue) style.Size = n.Size.Value;
            }
            var act = Compile(n.OnClick);
            return c.AddButton(n.Text ?? string.Empty, () => Run(act, string.Empty), style);
        }

        private IUiElement BuildToggle(IUiContainer c, UiNode n)
        {
            var entry = FindEntry(n, e => e.ValueType == typeof(bool), "a bool");
            bool initial = entry != null ? (bool)entry.BoxedValue : (n.BoolValue ?? false);
            var act = Compile(n.OnChange);
            bool busy = false;   // true while WE write the entry: its change event must not bounce back into the control
            IUiToggle t = null;
            t = c.AddToggle(n.Label ?? string.Empty, initial, v =>
            {
                if (entry != null) { busy = true; try { entry.BoxedValue = v; } finally { busy = false; } }
                Run(act, v ? "true" : "false");
            }, n.ToggleStyle ?? UiToggleStyle.Switch);

            if (entry != null)
                Follow(entry, t, () => { if (!busy) t.Value = (bool)entry.BoxedValue; });
            return t;
        }

        private IUiElement BuildSlider(IUiContainer c, UiNode n)
        {
            var entry = FindEntry(n, e => IsNumeric(e.ValueType), "a number (int, float or double)");
            bool isInt = entry != null && entry.ValueType == typeof(int);

            float min = n.Min ?? (entry != null && entry.HasRange ? ToFloat(entry.Min) : 0f);
            float max = n.Max ?? (entry != null && entry.HasRange ? ToFloat(entry.Max) : 1f);
            if (max <= min)
            {
                Warn(n, $"slider max ({max}) must be greater than min ({min}); using {min}..{min + 1f}");
                max = min + 1f;
            }
            float value = entry != null ? ToFloat(entry.BoxedValue) : (float)(n.NumberValue ?? min);
            float step = n.Step ?? (isInt ? 1f : 0f);
            string format = n.Format ?? (isInt ? "0" : "0.##");

            var act = Compile(n.OnChange);
            bool busy = false;
            IUiSlider s = null;
            s = c.AddSlider(n.Label ?? string.Empty, min, max, Mathf.Clamp(value, min, max), v =>
            {
                if (entry != null)
                {
                    busy = true;
                    try { entry.BoxedValue = isInt ? (object)(int)Math.Round(v) : v; } finally { busy = false; }
                }
                Run(act, LayoutBind.Format(v));
            }, step, format);

            if (entry != null)
                Follow(entry, s, () => { if (!busy) s.Value = ToFloat(entry.BoxedValue); });
            return s;
        }

        private IUiElement BuildInput(IUiContainer c, UiNode n)
        {
            var entry = FindEntry(n, e => true, "any value");
            string initial = entry != null ? ToText(entry.BoxedValue) : (n.StringValue ?? string.Empty);
            var onSubmit = Compile(n.OnSubmit);
            var onChange = Compile(n.OnChange);
            bool busy = false;   // true while we write the entry or the text: no echo
            IUiInput inp = null;
            inp = c.AddInput(n.Placeholder ?? string.Empty, initial,
                s =>
                {
                    if (entry != null)
                    {
                        // The text must convert to the entry's type (an int entry rejects "abc"); show the stored value again if not.
                        busy = true;
                        try { entry.BoxedValue = s; }
                        catch { inp.Text = ToText(entry.BoxedValue); return; }
                        finally { busy = false; }
                    }
                    Run(onSubmit, s ?? string.Empty);
                },
                s => { if (!busy) Run(onChange, s ?? string.Empty); });

            if (entry != null)
                Follow(entry, inp, () => { if (!busy && !inp.IsFocused) inp.Text = ToText(entry.BoxedValue); });
            return inp;
        }

        private IUiElement BuildDropdown(IUiContainer c, UiNode n)
        {
            var entry = FindEntry(n, e => e.IsEnum || e.ValueType == typeof(int) || e.ValueType == typeof(string), "an enum, int or string");
            string[] options = n.Options ?? (entry != null && entry.IsEnum ? entry.EnumNames : Array.Empty<string>());

            int index = 0;
            if (entry != null) index = IndexFor(entry, options);
            else if (n.NumberValue.HasValue) index = (int)Math.Round(n.NumberValue.Value);
            else if (n.StringValue != null) { int i = Array.IndexOf(options, n.StringValue); if (i >= 0) index = i; }
            if (options.Length > 0) index = Math.Max(0, Math.Min(options.Length - 1, index)); else index = 0;

            var act = Compile(n.OnChange);
            bool busy = false;
            IUiDropdown d = null;
            d = c.AddDropdown(n.Label, options, index, i =>
            {
                if (i < 0 || i >= options.Length) return;
                if (entry != null)
                {
                    busy = true;
                    try { entry.BoxedValue = entry.ValueType == typeof(int) ? (object)i : options[i]; } finally { busy = false; }
                }
                Run(act, options[i]);
            });

            if (entry != null)
                Follow(entry, d, () => { if (!busy) d.Index = IndexFor(entry, options); });
            return d;
        }

        private static int IndexFor(ConfigEntryInfo e, string[] options)
        {
            if (e.ValueType == typeof(int)) return (int)e.BoxedValue;
            int i = Array.IndexOf(options, Convert.ToString(e.BoxedValue, CultureInfo.InvariantCulture));
            return i < 0 ? 0 : i;
        }

        private IUiElement BuildImage(IUiContainer c, UiNode n)
        {
            if (string.IsNullOrWhiteSpace(n.File)) { Warn(n, "image has no \"file\"; skipped"); return null; }
            Sprite sprite = null;
            try { sprite = _ui.AssetsSource?.LoadSprite(n.File); }
            catch (Exception ex) { Warn(n, $"image '{n.File}' could not be read: {ex.Message}"); return null; }
            if (sprite == null) { Warn(n, $"image '{n.File}' could not be loaded (missing or not a PNG); skipped"); return null; }
            return c.AddImage(sprite, n.Width ?? 0f, n.Height ?? 0f);
        }

        // ---- config binding ----------------------------------------------------------------------

        // The config entry a widget is bound to, or null (with a warning) when the bind is missing / unusable.
        private ConfigEntryInfo FindEntry(UiNode n, Func<ConfigEntryInfo, bool> accepts, string expected)
        {
            if (n.Bind == null) return null;
            if (!LayoutBind.TryParseConfig(n.Bind, out var candidates)) return null;   // the parser already warned
            var cfg = _ui.ConfigSource;
            if (cfg == null)
            {
                Warn(n, $"bind '{n.Bind}': this mod has no config; the control is not bound");
                return null;
            }

            ConfigEntryInfo found = null;
            foreach (var cand in candidates)
            {
                foreach (var e in cfg.Entries)
                    if (string.Equals(e.Section, cand.Key, StringComparison.Ordinal) && string.Equals(e.Key, cand.Value, StringComparison.Ordinal)) { found = e; break; }
                if (found != null) break;
            }
            if (found == null)
            {
                Warn(n, $"bind '{n.Bind}': no such config entry (bind it with ctx.Config.Bind in OnLoad first); the control is not bound");
                return null;
            }
            if (!accepts(found))
            {
                Warn(n, $"bind '{n.Bind}': the entry is a {found.ValueType.Name} but this widget needs {expected}; the control is not bound");
                return null;
            }
            return found;
        }

        // Keeps a control in sync when the config entry changes from elsewhere (file reload, code, F8 Settings page).
        private void Follow(ConfigEntryInfo entry, IUiElement el, Action refresh)
        {
            Action handler = null;
            handler = () =>
            {
                try
                {
                    if (_ui.IsReleased || (el is UiElement ue && ue.Dead)) { entry.ValueChanged -= handler; return; }
                    refresh();
                }
                catch (Exception ex) { _owner.Report("layout config binding", ex); }
            };
            entry.ValueChanged += handler;
            _ui.TrackUndo(() => entry.ValueChanged -= handler);
        }

        private static bool IsNumeric(Type t) => t == typeof(int) || t == typeof(float) || t == typeof(double);

        private static float ToFloat(object o)
        {
            try { return Convert.ToSingle(o, CultureInfo.InvariantCulture); } catch { return 0f; }
        }

        private static string ToText(object o) => Convert.ToString(o, CultureInfo.InvariantCulture) ?? string.Empty;

        // ---- actions -----------------------------------------------------------------------------

        private static LayoutAction Compile(string spec) => LayoutAction.Parse(spec);

        // Runs an action. Called from toolkit callbacks, which already catch and log exceptions per mod.
        private void Run(LayoutAction a, string value)
        {
            switch (a.Kind)
            {
                case LayoutActionKind.None: break;
                case LayoutActionKind.Close: _win?.Close(); break;
                case LayoutActionKind.Open: WithWindow(a.Arg, w => w.Open()); break;
                case LayoutActionKind.Toggle: WithWindow(a.Arg, w => w.Toggle()); break;
                case LayoutActionKind.Toast: _ui.Toast(a.Arg); break;
                case LayoutActionKind.Announce: _ui.Announce(a.Arg); break;
                case LayoutActionKind.Command: AslPlugin.CommandHub?.Execute(a.Arg); break;
                case LayoutActionKind.Named: _ui.InvokeNamed(a.Arg, value); break;
            }
        }

        private void WithWindow(string id, Action<IUiWindow> act)
        {
            var w = _ui.GetWindow(id);
            if (w != null) act(w);
            else _ui.WarnOnce("win:" + id, $"[{_ui.ModNameValue}] layout action refers to window '{id}', which this mod has not created.");
        }
    }
}
