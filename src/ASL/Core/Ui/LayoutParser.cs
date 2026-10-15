using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using ASL.Api;

namespace ASL.Core
{
    /// <summary>The kind of a node in a JSON UI layout (see docs/ui.md).</summary>
    public enum UiNodeType
    {
        Window, Header, Label, Button, Toggle, Slider, Input, Dropdown, Progress, Image,
        Spacer, Separator, Row, Column, Grid, Scroll, Group, Tabs, Tab,
    }

    /// <summary>
    /// One parsed element of a layout. Only the fields the JSON actually contained are set (everything else is
    /// null / default), so the builder can fall back to the toolkit's own defaults. Plain data: no Unity types.
    /// </summary>
    public sealed class UiNode
    {
        public UiNodeType Type;
        /// <summary>Where the node sits in the file (for diagnostics), e.g. <c>children[2].children[0]</c>.</summary>
        public string Path = "";

        public string Id, Text, Label, Title, Placeholder, Tooltip, File, Format, Theme, ToggleKey;
        public string OnClick, OnChange, OnSubmit, Bind;

        public UiTextStyle? Style;
        public UiToggleStyle? ToggleStyle;
        public UiVariant? Variant;
        public UiShape? Shape;
        public UiSize? Size;
        public UiAnchor? Anchor;

        public float? Width, Height, Min, Max, Step, Spacing, CellWidth, CellHeight, OffsetX, OffsetY;
        public int? Columns;

        /// <summary>The <c>value</c> field, by JSON type: a bool (toggle), a number (slider, progress, dropdown index) or a string (input, dropdown option).</summary>
        public bool? BoolValue;
        public double? NumberValue;
        public string StringValue;

        public string[] Options;
        public bool? Interactable, Visible, Draggable, Closable, Modal, Open;

        /// <summary>Child nodes (containers). For a <see cref="UiNodeType.Tabs"/> node these are <see cref="UiNodeType.Tab"/> nodes.</summary>
        public readonly List<UiNode> Children = new List<UiNode>();
    }

    /// <summary>Result of <see cref="LayoutParser.Parse"/>.</summary>
    public sealed class LayoutParseResult
    {
        /// <summary>The window node (null when <see cref="Error"/> is set).</summary>
        public UiNode Root;
        /// <summary>Non-fatal problems: unknown types/fields, bad values. The affected node/field was skipped.</summary>
        public readonly List<string> Warnings = new List<string>();
        /// <summary>Set when the JSON could not be read at all.</summary>
        public string Error;
        public bool Ok => Error == null && Root != null;
    }

    /// <summary>
    /// Pure parser for declarative UI layouts (<c>ui/*.json</c>): JSON text to a <see cref="UiNode"/> tree plus a
    /// list of warnings. Comments and trailing commas are allowed, property names and enum values are
    /// case-insensitive. Bad input never throws: an unreadable document yields <see cref="LayoutParseResult.Error"/>;
    /// anything else (unknown type or field, wrong value type, bad enum name) is reported as a warning and skipped.
    /// </summary>
    public static class LayoutParser
    {
        private const int MaxDepth = 32;

        private static readonly string[] Common = { "type", "id", "tooltip", "interactable", "visible", "width", "height" };

        private static readonly Dictionary<UiNodeType, string[]> Allowed = new Dictionary<UiNodeType, string[]>
        {
            { UiNodeType.Window,    new[] { "title", "anchor", "offsetx", "offsety", "theme", "togglekey", "draggable", "closable", "modal", "open", "children" } },
            { UiNodeType.Header,    new[] { "text" } },
            { UiNodeType.Label,     new[] { "text", "style" } },
            { UiNodeType.Button,    new[] { "text", "variant", "shape", "size", "onclick" } },
            { UiNodeType.Toggle,    new[] { "label", "text", "value", "style", "onchange", "bind" } },
            { UiNodeType.Slider,    new[] { "label", "text", "min", "max", "value", "step", "format", "onchange", "bind" } },
            { UiNodeType.Input,     new[] { "placeholder", "value", "onsubmit", "onchange", "bind" } },
            { UiNodeType.Dropdown,  new[] { "label", "text", "options", "value", "onchange", "bind" } },
            { UiNodeType.Progress,  new[] { "value", "text" } },
            { UiNodeType.Image,     new[] { "file" } },
            { UiNodeType.Spacer,    new string[0] },
            { UiNodeType.Separator, new string[0] },
            { UiNodeType.Row,       new[] { "spacing", "children" } },
            { UiNodeType.Column,    new[] { "spacing", "children" } },
            { UiNodeType.Grid,      new[] { "columns", "cellwidth", "cellheight", "spacing", "children" } },
            { UiNodeType.Scroll,    new[] { "children" } },
            { UiNodeType.Group,     new[] { "title", "children" } },
            { UiNodeType.Tabs,      new[] { "tabs" } },
            { UiNodeType.Tab,       new[] { "title", "children" } },
        };

        /// <summary>Parses a layout document. Never throws.</summary>
        public static LayoutParseResult Parse(string json)
        {
            var res = new LayoutParseResult();
            if (string.IsNullOrWhiteSpace(json)) { res.Error = "the layout file is empty"; return res; }
            try
            {
                var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 64 };
                using var doc = JsonDocument.Parse(json, opts);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) { res.Error = "the layout must be a JSON object"; return res; }
                var node = ParseNode(root, "", 0, res.Warnings, true);
                if (node == null) { res.Error = "the layout root could not be read"; return res; }
                res.Root = node;
            }
            catch (JsonException ex) { res.Error = "invalid JSON: " + ex.Message; res.Root = null; }
            catch (Exception ex) { res.Error = "could not read the layout: " + ex.Message; res.Root = null; }
            return res;
        }

        // ---- nodes ---------------------------------------------------------------------------

        private static UiNode ParseNode(JsonElement el, string path, int depth, List<string> w, bool isRoot)
        {
            if (depth > MaxDepth) { Warn(w, path, "nesting is deeper than " + MaxDepth + " levels; skipped"); return null; }

            UiNodeType type = UiNodeType.Window;
            if (!isRoot)
            {
                if (!TryGetProp(el, "type", out var tv) || tv.ValueKind != JsonValueKind.String) { Warn(w, path, "missing \"type\"; skipped"); return null; }
                if (!TryNodeType(tv.GetString(), out type)) { Warn(w, path, $"unknown type '{tv.GetString()}'; skipped"); return null; }
                if (type == UiNodeType.Window) { Warn(w, path, "\"window\" is only valid at the top level; skipped"); return null; }
                if (type == UiNodeType.Tab) { Warn(w, path, "\"tab\" is only valid inside 'tabs'; skipped"); return null; }
            }
            else if (TryGetProp(el, "type", out var rt) && rt.ValueKind == JsonValueKind.String
                     && !string.Equals(rt.GetString(), "window", StringComparison.OrdinalIgnoreCase))
            {
                Warn(w, path, $"top-level type '{rt.GetString()}' ignored (the top level is always a window)");
            }

            return ParseNodeOfType(el, type, path, depth, w);
        }

        private static UiNode ParseNodeOfType(JsonElement el, UiNodeType type, string path, int depth, List<string> w)
        {
            var n = new UiNode { Type = type, Path = path };
            string[] allowed = Allowed[type];

            foreach (var p in el.EnumerateObject())
            {
                string name = p.Name;
                string lname = name.ToLowerInvariant();
                var v = p.Value;

                bool isCommon = Array.IndexOf(Common, lname) >= 0;
                bool isTypeField = Array.IndexOf(allowed, lname) >= 0;
                if (!isCommon && !isTypeField)
                {
                    Warn(w, path, IsKnownField(lname) ? $"field '{name}' has no effect on '{TypeName(type)}'; ignored" : $"unknown field '{name}'; ignored");
                    continue;
                }

                switch (lname)
                {
                    case "type": break;
                    case "id": n.Id = Str(v, name, path, w); break;
                    case "tooltip": n.Tooltip = Str(v, name, path, w); break;
                    case "interactable": n.Interactable = Bool(v, name, path, w); break;
                    case "visible": n.Visible = Bool(v, name, path, w); break;
                    case "width": n.Width = Num(v, name, path, w); break;
                    case "height": n.Height = Num(v, name, path, w); break;

                    case "title": n.Title = Str(v, name, path, w); break;
                    case "anchor": n.Anchor = Enum<UiAnchor>(v, name, path, w); break;
                    case "offsetx": n.OffsetX = Num(v, name, path, w); break;
                    case "offsety": n.OffsetY = Num(v, name, path, w); break;
                    case "theme": n.Theme = Str(v, name, path, w); break;
                    case "togglekey": n.ToggleKey = Str(v, name, path, w); break;
                    case "draggable": n.Draggable = Bool(v, name, path, w); break;
                    case "closable": n.Closable = Bool(v, name, path, w); break;
                    case "modal": n.Modal = Bool(v, name, path, w); break;
                    case "open": n.Open = Bool(v, name, path, w); break;

                    case "text":
                        if (type == UiNodeType.Toggle || type == UiNodeType.Slider || type == UiNodeType.Dropdown) n.Label = Str(v, name, path, w) ?? n.Label;
                        else n.Text = Str(v, name, path, w);
                        break;
                    case "label": n.Label = Str(v, name, path, w); break;
                    case "placeholder": n.Placeholder = Str(v, name, path, w); break;
                    case "file": n.File = Str(v, name, path, w); break;
                    case "format": n.Format = Str(v, name, path, w); break;

                    case "style":
                        if (type == UiNodeType.Toggle) n.ToggleStyle = Enum<UiToggleStyle>(v, name, path, w);
                        else n.Style = Enum<UiTextStyle>(v, name, path, w);
                        break;
                    case "variant": n.Variant = Enum<UiVariant>(v, name, path, w); break;
                    case "shape": n.Shape = Enum<UiShape>(v, name, path, w); break;
                    case "size": n.Size = Enum<UiSize>(v, name, path, w); break;

                    case "onclick": n.OnClick = Str(v, name, path, w); break;
                    case "onchange": n.OnChange = Str(v, name, path, w); break;
                    case "onsubmit": n.OnSubmit = Str(v, name, path, w); break;
                    case "bind":
                        n.Bind = Str(v, name, path, w);
                        if (n.Bind != null && !LayoutBind.TryParseConfig(n.Bind, out _))
                        {
                            Warn(w, path, $"bind '{n.Bind}' is not supported (use \"config:Section.Key\"); ignored");
                            n.Bind = null;
                        }
                        break;

                    case "min": n.Min = Num(v, name, path, w); break;
                    case "max": n.Max = Num(v, name, path, w); break;
                    case "step": n.Step = Num(v, name, path, w); break;
                    case "spacing": n.Spacing = Num(v, name, path, w); break;
                    case "cellwidth": n.CellWidth = Num(v, name, path, w); break;
                    case "cellheight": n.CellHeight = Num(v, name, path, w); break;
                    case "columns":
                    {
                        var d = Num(v, name, path, w);
                        if (d.HasValue) n.Columns = (int)Math.Round(d.Value);
                        break;
                    }

                    case "options": n.Options = StrArray(v, name, path, w); break;
                    case "value": ReadValue(n, v, path, w); break;

                    case "children": ReadChildren(n, v, path, depth, w); break;
                    case "tabs": ReadTabs(n, v, path, depth, w); break;
                }
            }

            if (type == UiNodeType.Window && n.Id != null && n.Id.Trim().Length == 0) n.Id = null;
            if (type == UiNodeType.Tab && n.Title == null) n.Title = "Tab";
            return n;
        }

        private static void ReadValue(UiNode n, JsonElement v, string path, List<string> w)
        {
            switch (n.Type)
            {
                case UiNodeType.Toggle:
                    if (v.ValueKind == JsonValueKind.True) n.BoolValue = true;
                    else if (v.ValueKind == JsonValueKind.False) n.BoolValue = false;
                    else Warn(w, path, "toggle 'value' must be true or false; ignored");
                    break;
                case UiNodeType.Slider:
                case UiNodeType.Progress:
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) n.NumberValue = d;
                    else Warn(w, path, $"{TypeName(n.Type)} 'value' must be a number; ignored");
                    break;
                case UiNodeType.Input:
                    if (v.ValueKind == JsonValueKind.String) n.StringValue = v.GetString();
                    else Warn(w, path, "input 'value' must be a string; ignored");
                    break;
                case UiNodeType.Dropdown:
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var idx)) n.NumberValue = idx;
                    else if (v.ValueKind == JsonValueKind.String) n.StringValue = v.GetString();
                    else Warn(w, path, "dropdown 'value' must be an option index or an option text; ignored");
                    break;
                default:
                    Warn(w, path, $"field 'value' has no effect on '{TypeName(n.Type)}'; ignored");
                    break;
            }
        }

        private static void ReadChildren(UiNode n, JsonElement v, string path, int depth, List<string> w)
        {
            if (v.ValueKind != JsonValueKind.Array) { Warn(w, path, "'children' must be an array; ignored"); return; }
            int i = 0;
            foreach (var c in v.EnumerateArray())
            {
                string cp = Join(path, "children[" + i + "]");
                i++;
                if (c.ValueKind != JsonValueKind.Object) { Warn(w, cp, "a child must be an object; skipped"); continue; }
                var child = ParseNode(c, cp, depth + 1, w, false);
                if (child == null) continue;
                n.Children.Add(child);
            }
        }

        private static void ReadTabs(UiNode n, JsonElement v, string path, int depth, List<string> w)
        {
            if (v.ValueKind != JsonValueKind.Array) { Warn(w, path, "'tabs' must be an array; ignored"); return; }
            if (depth + 1 > MaxDepth) { Warn(w, path, "nesting is too deep; tabs skipped"); return; }
            int i = 0;
            foreach (var t in v.EnumerateArray())
            {
                string tp = Join(path, "tabs[" + i + "]");
                i++;
                if (t.ValueKind != JsonValueKind.Object) { Warn(w, tp, "a tab must be an object; skipped"); continue; }
                n.Children.Add(ParseNodeOfType(t, UiNodeType.Tab, tp, depth + 1, w));
            }
        }

        // ---- field readers ---------------------------------------------------------------------

        private static string Str(JsonElement v, string name, string path, List<string> w)
        {
            if (v.ValueKind == JsonValueKind.String) return v.GetString();
            if (v.ValueKind == JsonValueKind.Null) return null;
            Warn(w, path, $"field '{name}' must be a string; ignored");
            return null;
        }

        private static float? Num(JsonElement v, string name, string path, List<string> w)
        {
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && !double.IsNaN(d) && !double.IsInfinity(d)) return (float)d;
            if (v.ValueKind != JsonValueKind.Null) Warn(w, path, $"field '{name}' must be a number; ignored");
            return null;
        }

        private static bool? Bool(JsonElement v, string name, string path, List<string> w)
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            if (v.ValueKind != JsonValueKind.Null) Warn(w, path, $"field '{name}' must be true or false; ignored");
            return null;
        }

        private static T? Enum<T>(JsonElement v, string name, string path, List<string> w) where T : struct
        {
            if (v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (!string.IsNullOrEmpty(s) && char.IsLetter(s[0]) && System.Enum.TryParse<T>(s.Trim(), true, out var r)) return r;
                Warn(w, path, $"field '{name}': '{s}' is not a valid {typeof(T).Name.Replace("Ui", "")} (use one of: {string.Join(", ", System.Enum.GetNames(typeof(T)))}); ignored");
                return null;
            }
            if (v.ValueKind != JsonValueKind.Null) Warn(w, path, $"field '{name}' must be a string; ignored");
            return null;
        }

        private static string[] StrArray(JsonElement v, string name, string path, List<string> w)
        {
            if (v.ValueKind != JsonValueKind.Array) { Warn(w, path, $"field '{name}' must be an array of strings; ignored"); return null; }
            var list = new List<string>();
            foreach (var e in v.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.String) list.Add(e.GetString());
                else if (e.ValueKind == JsonValueKind.Number) list.Add(e.GetRawText());
                else Warn(w, path, $"field '{name}' contains a non-string entry; skipped");
            }
            return list.ToArray();
        }

        // ---- helpers ---------------------------------------------------------------------------

        // Case-insensitive property lookup (JsonElement.TryGetProperty is case-sensitive).
        private static bool TryGetProp(JsonElement obj, string name, out JsonElement value)
        {
            foreach (var p in obj.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
            value = default;
            return false;
        }

        private static bool IsKnownField(string lname)
        {
            if (Array.IndexOf(Common, lname) >= 0) return true;
            foreach (var kv in Allowed) if (Array.IndexOf(kv.Value, lname) >= 0) return true;
            return false;
        }

        private static bool TryNodeType(string s, out UiNodeType type)
        {
            type = UiNodeType.Label;
            if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0])) return false;
            return System.Enum.TryParse(s.Trim(), true, out type);
        }

        private static string TypeName(UiNodeType t) => t.ToString().ToLowerInvariant();

        private static string Join(string path, string part) => path.Length == 0 ? part : path + "." + part;

        private static void Warn(List<string> w, string path, string msg) => w.Add(path.Length == 0 ? msg : path + ": " + msg);
    }

    /// <summary>What an <c>onClick</c> / <c>onChange</c> / <c>onSubmit</c> string means.</summary>
    public enum LayoutActionKind { None, Close, Open, Toggle, Toast, Announce, Command, Named }

    /// <summary>A parsed action string: one of the built-ins (<c>close</c>, <c>open:id</c>, ...) or a named, code-registered action.</summary>
    public readonly struct LayoutAction
    {
        public readonly LayoutActionKind Kind;
        /// <summary>The text after the colon (window id, toast text, console line); for <see cref="LayoutActionKind.Named"/> the action name.</summary>
        public readonly string Arg;

        public LayoutAction(LayoutActionKind kind, string arg) { Kind = kind; Arg = arg; }

        /// <summary>Classifies an action string. Null/empty is <see cref="LayoutActionKind.None"/>; anything that is not a built-in is <see cref="LayoutActionKind.Named"/>.</summary>
        public static LayoutAction Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new LayoutAction(LayoutActionKind.None, null);
            s = s.Trim();
            if (string.Equals(s, "close", StringComparison.OrdinalIgnoreCase)) return new LayoutAction(LayoutActionKind.Close, null);
            if (TryPrefix(s, "open:", LayoutActionKind.Open, out var a)) return a;
            if (TryPrefix(s, "toggle:", LayoutActionKind.Toggle, out a)) return a;
            if (TryPrefix(s, "toast:", LayoutActionKind.Toast, out a)) return a;
            if (TryPrefix(s, "announce:", LayoutActionKind.Announce, out a)) return a;
            if (TryPrefix(s, "command:", LayoutActionKind.Command, out a)) return a;
            return new LayoutAction(LayoutActionKind.Named, s);
        }

        private static bool TryPrefix(string s, string prefix, LayoutActionKind kind, out LayoutAction a)
        {
            a = default;
            if (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string arg = s.Substring(prefix.Length).Trim();
            if (arg.Length == 0) return false;   // "open:" alone is just an (unregistered) named action
            a = new LayoutAction(kind, arg);
            return true;
        }
    }

    /// <summary>Helpers for the <c>"bind": "config:Section.Key"</c> field.</summary>
    public static class LayoutBind
    {
        /// <summary>
        /// True if <paramref name="bind"/> is <c>config:Section.Key</c>. <paramref name="candidates"/> lists every way to split
        /// the text after the prefix at a dot (sections or keys may contain dots), first dot first.
        /// </summary>
        public static bool TryParseConfig(string bind, out List<KeyValuePair<string, string>> candidates)
        {
            candidates = null;
            if (string.IsNullOrWhiteSpace(bind)) return false;
            bind = bind.Trim();
            if (!bind.StartsWith("config:", StringComparison.OrdinalIgnoreCase)) return false;
            string rest = bind.Substring("config:".Length).Trim();
            var list = new List<KeyValuePair<string, string>>();
            for (int i = 1; i < rest.Length - 1; i++)
                if (rest[i] == '.') list.Add(new KeyValuePair<string, string>(rest.Substring(0, i), rest.Substring(i + 1)));
            if (list.Count == 0) return false;
            candidates = list;
            return true;
        }

        /// <summary>Formats a number the way actions receive it (invariant culture).</summary>
        public static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
