using System;
using System.IO;
using System.Linq;
using ASL.Api;
using ASL.Core;
using Xunit;

namespace ASL.Tests
{
    public class LayoutParserTests
    {
        private static LayoutParseResult Ok(string json)
        {
            var r = LayoutParser.Parse(json);
            Assert.True(r.Ok, r.Error);
            return r;
        }

        [Fact]
        public void Template_main_json_parses_without_warnings()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            string path = null;
            while (dir != null)
            {
                var p = Path.Combine(dir.FullName, "templates", "content", "asl-ui", "ui", "main.json");
                if (File.Exists(p)) { path = p; break; }
                dir = dir.Parent;
            }
            Assert.NotNull(path);
            var r = Ok(File.ReadAllText(path));
            Assert.Empty(r.Warnings);
            Assert.Equal("main", r.Root.Id);
            Assert.Equal("F6", r.Root.ToggleKey);
            Assert.Equal(UiAnchor.Center, r.Root.Anchor);
            Assert.Equal("Dark", r.Root.Theme);
            Assert.Equal(600f, r.Root.Width);
            Assert.Equal(UiNodeType.Row, r.Root.Children[2].Type);
            Assert.Equal("toast:Hi!", r.Root.Children[2].Children[0].OnClick);
        }

        [Fact]
        public void Every_sample_and_template_layout_parses_without_warnings()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "samples"))) root = root.Parent;
            Assert.NotNull(root);

            var files = new System.Collections.Generic.List<string>();
            foreach (var top in new[] { "samples", "templates" })
            {
                var dir = Path.Combine(root.FullName, top);
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
                {
                    var parent = Path.GetFileName(Path.GetDirectoryName(f));
                    if (string.Equals(parent, "ui", StringComparison.OrdinalIgnoreCase)) files.Add(f);
                }
            }
            Assert.NotEmpty(files);

            foreach (var f in files)
            {
                var r = LayoutParser.Parse(File.ReadAllText(f));
                Assert.True(r.Ok, f + ": " + r.Error);
                Assert.True(r.Warnings.Count == 0, f + ": " + string.Join("; ", r.Warnings));
            }
        }

        [Fact]
        public void Parses_every_node_type()
        {
            const string json = @"{
              ""id"": ""w"", ""title"": ""T"", ""width"": 500, ""height"": 300, ""anchor"": ""TopRight"", ""offsetX"": 10, ""offsetY"": -5,
              ""theme"": ""Light"", ""toggleKey"": ""F7"", ""draggable"": false, ""closable"": false, ""modal"": true, ""open"": true,
              ""children"": [
                { ""type"": ""header"", ""text"": ""H"" },
                { ""type"": ""label"", ""text"": ""L"", ""style"": ""Muted"", ""tooltip"": ""tip"", ""id"": ""lbl"" },
                { ""type"": ""button"", ""text"": ""B"", ""variant"": ""Success"", ""shape"": ""Pill"", ""size"": ""Large"", ""onClick"": ""act"", ""width"": 120, ""height"": 40, ""interactable"": false },
                { ""type"": ""toggle"", ""label"": ""Tg"", ""value"": true, ""onChange"": ""t"", ""bind"": ""config:General.God"", ""style"": ""Checkbox"" },
                { ""type"": ""slider"", ""label"": ""S"", ""min"": 1, ""max"": 5, ""value"": 2, ""step"": 0.5, ""format"": ""0.0"", ""bind"": ""config:General.Speed"" },
                { ""type"": ""input"", ""placeholder"": ""P"", ""value"": ""init"", ""onSubmit"": ""say"", ""onChange"": ""typing"" },
                { ""type"": ""dropdown"", ""label"": ""D"", ""options"": [""A"",""B""], ""value"": 1 },
                { ""type"": ""progress"", ""value"": 0.3, ""text"": ""30%"" },
                { ""type"": ""image"", ""file"": ""icon.png"", ""width"": 64, ""height"": 64 },
                { ""type"": ""spacer"", ""height"": 20 },
                { ""type"": ""separator"" },
                { ""type"": ""grid"", ""columns"": 3, ""cellWidth"": 120, ""cellHeight"": 48, ""spacing"": 4, ""children"": [ { ""type"": ""label"", ""text"": ""g"" } ] },
                { ""type"": ""scroll"", ""height"": 300, ""children"": [ { ""type"": ""label"", ""text"": ""s"" } ] },
                { ""type"": ""group"", ""title"": ""Box"", ""children"": [] },
                { ""type"": ""column"", ""spacing"": 2, ""children"": [] },
                { ""type"": ""tabs"", ""tabs"": [ { ""title"": ""One"", ""children"": [ { ""type"": ""label"", ""text"": ""t1"" } ] }, { ""title"": ""Two"", ""children"": [] } ] }
              ] }";
            var r = Ok(json);
            Assert.Empty(r.Warnings);

            var w = r.Root;
            Assert.Equal(UiNodeType.Window, w.Type);
            Assert.Equal("w", w.Id); Assert.Equal("T", w.Title);
            Assert.Equal(500f, w.Width); Assert.Equal(300f, w.Height);
            Assert.Equal(UiAnchor.TopRight, w.Anchor);
            Assert.Equal(10f, w.OffsetX); Assert.Equal(-5f, w.OffsetY);
            Assert.Equal("Light", w.Theme); Assert.Equal("F7", w.ToggleKey);
            Assert.False(w.Draggable); Assert.False(w.Closable); Assert.True(w.Modal); Assert.True(w.Open);
            Assert.Equal(16, w.Children.Count);

            var c = w.Children;
            Assert.Equal(UiNodeType.Header, c[0].Type); Assert.Equal("H", c[0].Text);
            Assert.Equal(UiTextStyle.Muted, c[1].Style); Assert.Equal("tip", c[1].Tooltip); Assert.Equal("lbl", c[1].Id);

            Assert.Equal(UiVariant.Success, c[2].Variant); Assert.Equal(UiShape.Pill, c[2].Shape); Assert.Equal(UiSize.Large, c[2].Size);
            Assert.Equal("act", c[2].OnClick); Assert.Equal(120f, c[2].Width); Assert.Equal(40f, c[2].Height); Assert.False(c[2].Interactable);

            Assert.Equal("Tg", c[3].Label); Assert.True(c[3].BoolValue); Assert.Equal("t", c[3].OnChange);
            Assert.Equal("config:General.God", c[3].Bind); Assert.Equal(UiToggleStyle.Checkbox, c[3].ToggleStyle);

            Assert.Equal(1f, c[4].Min); Assert.Equal(5f, c[4].Max); Assert.Equal(2.0, c[4].NumberValue); Assert.Equal(0.5f, c[4].Step);
            Assert.Equal("0.0", c[4].Format); Assert.Equal("config:General.Speed", c[4].Bind);

            Assert.Equal("P", c[5].Placeholder); Assert.Equal("init", c[5].StringValue); Assert.Equal("say", c[5].OnSubmit); Assert.Equal("typing", c[5].OnChange);

            Assert.Equal(new[] { "A", "B" }, c[6].Options); Assert.Equal(1.0, c[6].NumberValue);
            Assert.Equal(0.3, c[7].NumberValue); Assert.Equal("30%", c[7].Text);
            Assert.Equal("icon.png", c[8].File); Assert.Equal(64f, c[8].Width);
            Assert.Equal(UiNodeType.Spacer, c[9].Type); Assert.Equal(20f, c[9].Height);
            Assert.Equal(UiNodeType.Separator, c[10].Type);

            Assert.Equal(3, c[11].Columns); Assert.Equal(120f, c[11].CellWidth); Assert.Equal(48f, c[11].CellHeight); Assert.Equal(4f, c[11].Spacing);
            Assert.Single(c[11].Children);
            Assert.Equal(300f, c[12].Height); Assert.Single(c[12].Children);
            Assert.Equal("Box", c[13].Title);
            Assert.Equal(UiNodeType.Column, c[14].Type); Assert.Equal(2f, c[14].Spacing);

            var tabs = c[15];
            Assert.Equal(UiNodeType.Tabs, tabs.Type);
            Assert.Equal(2, tabs.Children.Count);
            Assert.All(tabs.Children, t => Assert.Equal(UiNodeType.Tab, t.Type));
            Assert.Equal("One", tabs.Children[0].Title);
            Assert.Single(tabs.Children[0].Children);
        }

        [Fact]
        public void Nesting_and_paths()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""row"", ""children"": [ { ""type"": ""column"", ""children"": [ { ""type"": ""label"", ""text"": ""deep"" } ] } ] } ] }");
            var deep = r.Root.Children[0].Children[0].Children[0];
            Assert.Equal("deep", deep.Text);
            Assert.Equal("children[0].children[0].children[0]", deep.Path);
        }

        [Fact]
        public void Comments_trailing_commas_and_case_insensitivity_are_accepted()
        {
            var r = Ok(@"{
              // a comment
              ""ID"": ""x"",
              /* block */
              ""Children"": [ { ""Type"": ""BUTTON"", ""Text"": ""ok"", ""Variant"": ""danger"", }, ],
            }");
            Assert.Empty(r.Warnings);
            Assert.Equal("x", r.Root.Id);
            Assert.Equal(UiVariant.Danger, r.Root.Children[0].Variant);
        }

        [Fact]
        public void Unknown_type_is_skipped_with_a_warning()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""banana"" }, { ""type"": ""label"", ""text"": ""kept"" } ] }");
            Assert.Single(r.Root.Children);
            Assert.Equal("kept", r.Root.Children[0].Text);
            Assert.Contains(r.Warnings, w => w.Contains("banana") && w.Contains("children[0]"));
        }

        [Fact]
        public void Missing_type_and_non_object_children_warn()
        {
            var r = Ok(@"{ ""children"": [ { ""text"": ""x"" }, 5, ""s"" ] }");
            Assert.Empty(r.Root.Children);
            Assert.Equal(3, r.Warnings.Count);
        }

        [Fact]
        public void Unknown_field_warns_but_node_survives()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""label"", ""text"": ""t"", ""colour"": ""red"" } ], ""frobnicate"": 1 }");
            Assert.Single(r.Root.Children);
            Assert.Contains(r.Warnings, w => w.Contains("colour"));
            Assert.Contains(r.Warnings, w => w.Contains("frobnicate"));
        }

        [Fact]
        public void Field_that_does_not_apply_to_the_type_warns()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""label"", ""text"": ""t"", ""onClick"": ""x"" } ] }");
            Assert.Null(r.Root.Children[0].OnClick);
            Assert.Contains(r.Warnings, w => w.Contains("onClick") && w.Contains("no effect"));
        }

        [Fact]
        public void Bad_values_warn_and_are_ignored()
        {
            var r = Ok(@"{ ""width"": ""wide"", ""anchor"": ""Nowhere"", ""modal"": ""yes"", ""children"": [
                { ""type"": ""button"", ""text"": 5, ""variant"": ""Purple"", ""size"": 3 },
                { ""type"": ""toggle"", ""label"": ""x"", ""value"": 1 },
                { ""type"": ""slider"", ""min"": ""a"", ""value"": ""b"" },
                { ""type"": ""dropdown"", ""options"": ""A,B"" },
                { ""type"": ""grid"", ""columns"": ""three"" },
                { ""type"": ""label"", ""text"": ""x"", ""children"": {} } ] }");
            Assert.Null(r.Root.Width);
            Assert.Null(r.Root.Anchor);
            Assert.Null(r.Root.Modal);
            Assert.Null(r.Root.Children[0].Text);
            Assert.Null(r.Root.Children[0].Variant);
            Assert.Null(r.Root.Children[1].BoolValue);
            Assert.Null(r.Root.Children[2].NumberValue);
            Assert.Null(r.Root.Children[3].Options);
            Assert.Null(r.Root.Children[4].Columns);
            Assert.True(r.Warnings.Count >= 12, string.Join("\n", r.Warnings));
        }

        [Fact]
        public void Numeric_enum_strings_are_rejected()
        {
            var r = Ok(@"{ ""anchor"": ""3"" }");
            Assert.Null(r.Root.Anchor);
            Assert.Single(r.Warnings);
        }

        [Fact]
        public void Unsupported_bind_is_dropped()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""toggle"", ""label"": ""x"", ""bind"": ""storage:foo"" } ] }");
            Assert.Null(r.Root.Children[0].Bind);
            Assert.Contains(r.Warnings, w => w.Contains("bind"));
        }

        [Fact]
        public void Tab_outside_tabs_and_window_inside_children_are_rejected()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""tab"" }, { ""type"": ""window"" } ] }");
            Assert.Empty(r.Root.Children);
            Assert.Equal(2, r.Warnings.Count);
        }

        [Fact]
        public void Text_is_an_alias_of_label_for_value_widgets()
        {
            var r = Ok(@"{ ""children"": [ { ""type"": ""toggle"", ""text"": ""T"" }, { ""type"": ""slider"", ""text"": ""S"" } ] }");
            Assert.Equal("T", r.Root.Children[0].Label);
            Assert.Equal("S", r.Root.Children[1].Label);
        }

        [Fact]
        public void Excessive_nesting_is_cut_off()
        {
            string json = "{ \"children\": [" + string.Concat(Enumerable.Repeat("{ \"type\": \"row\", \"children\": [", 40)) + string.Concat(Enumerable.Repeat("] }", 40)) + "] }";
            var r = LayoutParser.Parse(json);
            // Either the JSON depth limit or ours kicks in; it must not throw and must not return a 40-deep tree.
            if (r.Ok) Assert.NotEmpty(r.Warnings);
            else Assert.NotNull(r.Error);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{ not json")]
        [InlineData("[1,2,3]")]
        [InlineData("\"hello\"")]
        [InlineData("null")]
        public void Unreadable_documents_return_an_error_and_never_throw(string json)
        {
            var r = LayoutParser.Parse(json);
            Assert.False(r.Ok);
            Assert.False(string.IsNullOrEmpty(r.Error));
            Assert.Null(r.Root);
        }

        [Fact]
        public void Null_input_returns_an_error()
        {
            var r = LayoutParser.Parse(null);
            Assert.False(r.Ok);
        }

        [Fact]
        public void Top_level_type_other_than_window_warns()
        {
            var r = Ok(@"{ ""type"": ""row"" }");
            Assert.Equal(UiNodeType.Window, r.Root.Type);
            Assert.Single(r.Warnings);
        }

        [Theory]
        [InlineData("close", LayoutActionKind.Close, null)]
        [InlineData("CLOSE", LayoutActionKind.Close, null)]
        [InlineData("open:settings", LayoutActionKind.Open, "settings")]
        [InlineData("toggle:main", LayoutActionKind.Toggle, "main")]
        [InlineData("toast:Hello there: friend", LayoutActionKind.Toast, "Hello there: friend")]
        [InlineData("announce:Hi", LayoutActionKind.Announce, "Hi")]
        [InlineData("command:give coffee 3", LayoutActionKind.Command, "give coffee 3")]
        [InlineData("myAction", LayoutActionKind.Named, "myAction")]
        [InlineData("open:", LayoutActionKind.Named, "open:")]
        [InlineData("", LayoutActionKind.None, null)]
        [InlineData(null, LayoutActionKind.None, null)]
        public void Action_strings(string input, LayoutActionKind kind, string arg)
        {
            var a = LayoutAction.Parse(input);
            Assert.Equal(kind, a.Kind);
            Assert.Equal(arg, a.Arg);
        }

        [Fact]
        public void Config_bind_candidates()
        {
            Assert.True(LayoutBind.TryParseConfig("config:General.Speed", out var c));
            Assert.Single(c);
            Assert.Equal("General", c[0].Key);
            Assert.Equal("Speed", c[0].Value);

            Assert.True(LayoutBind.TryParseConfig("config:A.B.C", out c));
            Assert.Equal(2, c.Count);
            Assert.Equal("A", c[0].Key); Assert.Equal("B.C", c[0].Value);
            Assert.Equal("A.B", c[1].Key); Assert.Equal("C", c[1].Value);

            Assert.False(LayoutBind.TryParseConfig("config:NoDot", out _));
            Assert.False(LayoutBind.TryParseConfig("config:.x", out _));
            Assert.False(LayoutBind.TryParseConfig("other:A.B", out _));
            Assert.False(LayoutBind.TryParseConfig(null, out _));
        }

        [Fact]
        public void Number_format_is_invariant()
        {
            var old = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.Equal("1.5", LayoutBind.Format(1.5));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        }
    }
}
