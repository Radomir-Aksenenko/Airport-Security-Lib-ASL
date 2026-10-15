using System;
using System.Collections.Generic;
using System.Text;

namespace ASL.Core
{
    /// <summary>One parsed console command: the name and its arguments (quotes already resolved).</summary>
    internal readonly struct ParsedCommand
    {
        public readonly string Name;
        public readonly string[] Args;
        public ParsedCommand(string name, string[] args) { Name = name; Args = args; }
    }

    /// <summary>
    /// Pure console tokenizer (no Unity). A line is split into commands on <c>;</c> (outside quotes) and each
    /// command into a name plus arguments on whitespace. Double and single quotes group words; inside quotes a
    /// backslash escapes <c>\ " '</c> (and <c>\n</c>, <c>\t</c>); outside quotes a backslash is literal so Windows paths
    /// work unquoted. An unterminated quote runs to the end of the line. <c>""</c> is an empty argument.
    /// </summary>
    internal static class CommandLine
    {
        /// <summary>Split a line into raw command strings on <c>;</c> outside quotes. Empty segments are dropped.</summary>
        public static List<string> SplitCommands(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line)) return result;
            int start = 0;
            char quote = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < line.Length) { i++; continue; }
                    if (c == quote) quote = '\0';
                }
                else if (c == '"' || c == '\'') quote = c;
                else if (c == ';')
                {
                    AddPart(result, line, start, i);
                    start = i + 1;
                }
            }
            AddPart(result, line, start, line.Length);
            return result;
        }

        private static void AddPart(List<string> into, string s, int from, int to)
        {
            string part = s.Substring(from, to - from).Trim();
            if (part.Length > 0) into.Add(part);
        }

        /// <summary>Split one command (no <c>;</c> handling) into tokens: name first, then arguments.</summary>
        public static List<string> Tokenize(string command)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(command)) return tokens;
            var sb = new StringBuilder();
            bool has = false;          // a token is in progress (so "" yields an empty token)
            char quote = '\0';
            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < command.Length)
                    {
                        char n = command[i + 1];
                        if (n == '\\' || n == '"' || n == '\'') { sb.Append(n); i++; continue; }
                        if (n == 'n') { sb.Append('\n'); i++; continue; }
                        if (n == 't') { sb.Append('\t'); i++; continue; }
                        sb.Append(c);       // unknown escape: keep the backslash literally
                        continue;
                    }
                    if (c == quote) { quote = '\0'; continue; }
                    sb.Append(c);
                }
                else if (c == '"' || c == '\'') { quote = c; has = true; }
                else if (char.IsWhiteSpace(c))
                {
                    if (has) { tokens.Add(sb.ToString()); sb.Clear(); has = false; }
                }
                else { sb.Append(c); has = true; }
            }
            if (has) tokens.Add(sb.ToString());
            return tokens;
        }

        /// <summary>Parse a whole line into commands (name + args). Blank commands are skipped.</summary>
        public static List<ParsedCommand> Parse(string line)
        {
            var result = new List<ParsedCommand>();
            foreach (var cmd in SplitCommands(line))
            {
                var t = Tokenize(cmd);
                if (t.Count == 0 || t[0].Length == 0) continue;
                var args = new string[t.Count - 1];
                for (int i = 1; i < t.Count; i++) args[i - 1] = t[i];
                result.Add(new ParsedCommand(t[0], args));
            }
            return result;
        }
    }

    /// <summary>Pure Tab-completion helpers for the command name being typed.</summary>
    internal static class Autocomplete
    {
        /// <summary>All names starting with <paramref name="partial"/> (case-insensitive), sorted, distinct.</summary>
        public static List<string> Candidates(IEnumerable<string> names, string partial)
        {
            partial ??= string.Empty;
            var list = new List<string>();
            if (names == null) return list;
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n) && n.StartsWith(partial, StringComparison.OrdinalIgnoreCase) && !Contains(list, n))
                    list.Add(n);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static bool Contains(List<string> list, string s)
        {
            foreach (var x in list) if (string.Equals(x, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Longest common (case-insensitive) prefix of <paramref name="items"/>, taken from the first item. "" for none.</summary>
        public static string CommonPrefix(IList<string> items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            string first = items[0];
            int len = first.Length;
            for (int i = 1; i < items.Count && len > 0; i++)
            {
                string s = items[i];
                int k = 0;
                int max = Math.Min(len, s.Length);
                while (k < max && char.ToLowerInvariant(first[k]) == char.ToLowerInvariant(s[k])) k++;
                len = k;
            }
            return first.Substring(0, len);
        }

        /// <summary>
        /// Complete the command name in the LAST <c>;</c>-segment of <paramref name="input"/> (only while the cursor is
        /// still in the name, i.e. no space after it). Returns the new input text; <paramref name="candidates"/> holds
        /// every match (print them when more than one remains after extending to the common prefix).
        /// A single match gets a trailing space.
        /// </summary>
        public static string Complete(IEnumerable<string> names, string input, out List<string> candidates)
        {
            input ??= string.Empty;
            candidates = new List<string>();
            int nameStart = LastSegmentStart(input);
            while (nameStart < input.Length && char.IsWhiteSpace(input[nameStart])) nameStart++;
            string partial = input.Substring(nameStart);
            if (partial.Length == 0) return input;
            foreach (char c in partial) if (char.IsWhiteSpace(c) || c == '"' || c == '\'') return input;   // past the name

            candidates = Candidates(names, partial);
            if (candidates.Count == 0) return input;
            string head = input.Substring(0, nameStart);
            if (candidates.Count == 1) return head + candidates[0] + " ";
            string common = CommonPrefix(candidates);
            return head + (common.Length > partial.Length ? common : partial);
        }

        private static int LastSegmentStart(string s)
        {
            int start = 0;
            char quote = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (quote != '\0')
                {
                    if (c == '\\' && i + 1 < s.Length) { i++; continue; }
                    if (c == quote) quote = '\0';
                }
                else if (c == '"' || c == '\'') quote = c;
                else if (c == ';') start = i + 1;
            }
            return start;
        }
    }
}
