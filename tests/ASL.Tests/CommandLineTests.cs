using System.Collections.Generic;
using ASL.Core;
using Xunit;

namespace ASL.Tests
{
    public class CommandLineTests
    {
        private static string[] Tok(string s) => CommandLine.Tokenize(s).ToArray();

        [Fact]
        public void Empty_and_whitespace_give_no_tokens()
        {
            Assert.Empty(Tok(""));
            Assert.Empty(Tok(null));
            Assert.Empty(Tok("   \t "));
            Assert.Empty(CommandLine.Parse("  ;  ; "));
        }

        [Fact]
        public void Splits_on_multiple_spaces()
        {
            Assert.Equal(new[] { "give", "apple", "3" }, Tok("  give   apple \t 3  "));
        }

        [Fact]
        public void Double_and_single_quotes_group()
        {
            Assert.Equal(new[] { "say", "hello world", "x y" }, Tok("say \"hello world\" 'x y'"));
            Assert.Equal(new[] { "say", "it's", "a \"b\"" }, Tok("say \"it's\" 'a \"b\"'"));
        }

        [Fact]
        public void Backslash_escapes_inside_quotes()
        {
            Assert.Equal(new[] { "echo", "a \"quoted\" word" }, Tok("echo \"a \\\"quoted\\\" word\""));
            Assert.Equal(new[] { "echo", "back\\slash" }, Tok("echo \"back\\\\slash\""));
            Assert.Equal(new[] { "echo", "l1\nl2" }, Tok("echo \"l1\\nl2\""));
        }

        [Fact]
        public void Backslash_outside_quotes_is_literal_and_unknown_escape_kept()
        {
            Assert.Equal(new[] { "load", "C:\\mods\\x" }, Tok("load C:\\mods\\x"));
            Assert.Equal(new[] { "load", "C:\\mods" }, Tok("load \"C:\\mods\""));
        }

        [Fact]
        public void Empty_quotes_make_an_empty_argument_and_adjacent_parts_join()
        {
            Assert.Equal(new[] { "set", "", "v" }, Tok("set \"\" v"));
            Assert.Equal(new[] { "ab cd" }, Tok("a\"b c\"d"));
        }

        [Fact]
        public void Unterminated_quote_runs_to_end()
        {
            Assert.Equal(new[] { "say", "hello world" }, Tok("say \"hello world"));
        }

        [Fact]
        public void Semicolons_split_commands_outside_quotes_only()
        {
            var p = CommandLine.Parse("echo a; echo \"b;c\" ;; fps");
            Assert.Equal(3, p.Count);
            Assert.Equal("echo", p[0].Name);
            Assert.Equal(new[] { "a" }, p[0].Args);
            Assert.Equal(new[] { "b;c" }, p[1].Args);
            Assert.Equal("fps", p[2].Name);
            Assert.Empty(p[2].Args);
        }

        [Fact]
        public void Escaped_quote_does_not_end_the_string_for_splitting()
        {
            var parts = CommandLine.SplitCommands("echo \"a\\\";b\"; fps");
            Assert.Equal(2, parts.Count);
            Assert.Equal("fps", parts[1]);
        }

        [Fact]
        public void Parse_name_with_dot_and_no_args()
        {
            var p = CommandLine.Parse("ui.demo");
            Assert.Single(p);
            Assert.Equal("ui.demo", p[0].Name);
        }
    }

    public class AutocompleteTests
    {
        private static readonly string[] Names = { "help", "history", "hello.world", "clear", "config", "Echo" };

        [Fact]
        public void Candidates_are_case_insensitive_sorted_distinct()
        {
            Assert.Equal(new[] { "hello.world", "help", "history" }, Autocomplete.Candidates(Names, "H"));
            Assert.Equal(new[] { "Echo" }, Autocomplete.Candidates(Names, "ec"));
            Assert.Empty(Autocomplete.Candidates(Names, "zzz"));
            Assert.Single(Autocomplete.Candidates(new[] { "a", "A" }, ""));
        }

        [Fact]
        public void CommonPrefix_works()
        {
            Assert.Equal("hel", Autocomplete.CommonPrefix(new List<string> { "hello.world", "help" }));
            Assert.Equal("", Autocomplete.CommonPrefix(new List<string> { "a", "b" }));
            Assert.Equal("", Autocomplete.CommonPrefix(new List<string>()));
            Assert.Equal("only", Autocomplete.CommonPrefix(new List<string> { "only" }));
        }

        [Fact]
        public void Complete_single_match_adds_space()
        {
            Assert.Equal("clear ", Autocomplete.Complete(Names, "cl", out var c));
            Assert.Single(c);
        }

        [Fact]
        public void Complete_extends_to_common_prefix_and_reports_candidates()
        {
            string r = Autocomplete.Complete(Names, "he", out var c);
            Assert.Equal("hel", r);
            Assert.Equal(2, c.Count);
            // no further progress: input unchanged but candidates still reported
            Assert.Equal("hel", Autocomplete.Complete(Names, "hel", out c));
            Assert.Equal(2, c.Count);
        }

        [Fact]
        public void Complete_only_the_name_of_the_last_segment()
        {
            Assert.Equal("echo hi; clear ", Autocomplete.Complete(Names, "echo hi; cl", out _));
            Assert.Equal("echo cl", Autocomplete.Complete(Names, "echo cl", out var c));   // past the name
            Assert.Empty(c);
            Assert.Equal("", Autocomplete.Complete(Names, "", out _));
        }
    }

    public class RingBufferTests
    {
        [Fact]
        public void Drops_oldest_when_full()
        {
            var b = new RingBuffer<int>(3);
            for (int i = 1; i <= 5; i++) b.Add(i);
            Assert.Equal(3, b.Count);
            Assert.Equal(3, b[0]);
            Assert.Equal(5, b[2]);
        }

        [Fact]
        public void Version_changes_and_clear_resets()
        {
            var b = new RingBuffer<string>(2);
            int v = b.Version;
            b.Add("a");
            Assert.NotEqual(v, b.Version);
            v = b.Version;
            b.Clear();
            Assert.NotEqual(v, b.Version);
            Assert.Equal(0, b.Count);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => b[0]);
            b.Add("x");
            Assert.Equal("x", b[0]);
        }

        [Fact]
        public void Capacity_must_be_positive()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
        }
    }

    public class CommandHistoryTests
    {
        [Fact]
        public void Up_down_navigation_with_draft()
        {
            var h = new CommandHistory(50);
            h.Add("one"); h.Add("two"); h.Add("two"); h.Add("  "); h.Add("three");
            Assert.Equal(3, h.Items.Count);
            Assert.Equal("three", h.Previous("dra"));
            Assert.Equal("two", h.Previous("three"));
            Assert.Equal("one", h.Previous("two"));
            Assert.Equal("one", h.Previous("one"));      // clamped at oldest
            Assert.Equal("two", h.Next("one"));
            Assert.Equal("three", h.Next("two"));
            Assert.Equal("dra", h.Next("three"));        // back to the draft
            Assert.Equal("dra", h.Next("dra"));
        }

        [Fact]
        public void Bounded()
        {
            var h = new CommandHistory(3);
            for (int i = 0; i < 10; i++) h.Add("c" + i);
            Assert.Equal(3, h.Items.Count);
            Assert.Equal("c7", h.Items[0]);
        }

        [Fact]
        public void Empty_history_returns_current()
        {
            Assert.Equal("x", new CommandHistory().Previous("x"));
        }
    }

    public class ConsoleTextTests
    {
        [Fact]
        public void Strip_removes_tags_but_keeps_plain_angle_brackets()
        {
            Assert.Equal("err boom", ConsoleText.Strip("<color=#ff0000>err</color> <b>boom</b>"));
            Assert.Equal("a < b and c > d", ConsoleText.Strip("a < b and c > d"));
            Assert.Equal("", ConsoleText.Strip(null));
        }

        [Fact]
        public void Literal_wraps_in_noparse()
        {
            Assert.Equal("<noparse><b></noparse>", ConsoleText.Literal("<b>"));
            // a literal closing tag inside the text must not end the noparse block early
            Assert.Equal("<noparse><\\/noparse>x</noparse>", ConsoleText.Literal("</noparse>x"));
        }
    }
}
