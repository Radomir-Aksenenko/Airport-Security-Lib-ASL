using System;
using System.Collections.Generic;
using ASL.Api;
using ASL.Core;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Shared dev-console command table plus the console's output buffer. Names are case-insensitive and global.
    /// A mod's command <c>give</c> is always reachable as <c>&lt;shortId&gt;.give</c> (shortId = last segment of the
    /// mod id, lower-cased) and also as the bare <c>give</c> when no one else owns that name; on a clash the later
    /// registrant only gets the qualified name (a warning is logged). ASL's own built-ins are bare-only.
    /// <see cref="Execute"/> runs every <c>;</c>-separated command, catching handler exceptions. The window
    /// (<c>DevConsole</c>) only reads <see cref="Snapshot"/> / <see cref="Version"/>. Main-thread API (Print is also locked, so a stray
    /// background-thread print cannot corrupt the buffer).
    /// </summary>
    internal sealed class CommandRegistry
    {
        /// <summary>Console buffer size (lines).</summary>
        public const int BufferLines = 500;
        /// <summary>Owner id of ASL's built-in commands.</summary>
        public const string BuiltinOwner = "asl";
        /// <summary>Colour of error lines.</summary>
        public const string ErrorColor = "ff6b6b";
        /// <summary>Colour of warning lines.</summary>
        public const string WarnColor = "ffcc66";
        /// <summary>Colour of echoed input / dim text.</summary>
        public const string DimColor = "9aa4b2";

        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly RingBuffer<string> _lines = new(BufferLines);
        private readonly object _bufferLock = new();
        private int _depth;

        public CommandRegistry(ManualLogSource log) => _log = log;

        internal sealed class Command
        {
            public string Name;            // as registered (bare)
            public string Qualified;       // "<shortId>.<name>" (null for built-ins)
            public string Description;
            public string Owner;
            public Action<string[]> Action;
            public Func<string[], string> Func;
            public bool HasBare;           // is the bare name mapped to this command?
        }

        /// <summary>Every key (bare and qualified) to its command. Several keys can map to one <see cref="Command"/>.</summary>
        public IReadOnlyDictionary<string, Command> Commands => _commands;

        /// <summary>Last segment of a mod id, lower-cased: "com.radomir.propHunt" gives "prophunt".</summary>
        public static string ShortId(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId)) return "mod";
            string s = modId.Trim();
            int dot = s.LastIndexOfAny(new[] { '.', '/', '\\' });
            if (dot >= 0 && dot < s.Length - 1) s = s.Substring(dot + 1);
            return s.ToLowerInvariant();
        }

        /// <summary>Distinct commands sorted by display name (bare name if owned, else the qualified one).</summary>
        public List<Command> Unique()
        {
            var seen = new HashSet<Command>();
            var list = new List<Command>();
            foreach (var c in _commands.Values) if (seen.Add(c)) list.Add(c);
            list.Sort((a, b) => string.Compare(DisplayName(a), DisplayName(b), StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>The name users should type for <paramref name="c"/>: bare if it owns it, else qualified.</summary>
        public static string DisplayName(Command c) => c.HasBare || c.Qualified == null ? c.Name : c.Qualified;

        /// <summary>All typeable names (bare + qualified), for Tab completion.</summary>
        public IEnumerable<string> Names => _commands.Keys;

        public void Register(string owner, string name, string description, Action<string[]> action, Func<string[], string> func)
            => RegisterCore(owner, name, description, action, func, builtin: false);

        /// <summary>Register one of ASL's own commands (bare name only; replaces nothing a mod owns).</summary>
        public void RegisterBuiltin(string name, string description, Action<string[]> action = null, Func<string[], string> func = null)
            => RegisterCore(BuiltinOwner, name, description, action, func, builtin: true);

        private void RegisterCore(string owner, string name, string description, Action<string[]> action, Func<string[], string> func, bool builtin)
        {
            if (string.IsNullOrWhiteSpace(name) || (action == null && func == null)) return;
            name = name.Trim();
            if (name.IndexOfAny(new[] { ' ', '\t', ';', '"', '\'' }) >= 0)
            {
                _log.LogWarning($"Console command '{name}' (from '{owner}') has an invalid name (no spaces, quotes or ';'); not registered.");
                return;
            }

            // Re-registering your own command replaces it (e.g. after a hot reload that skipped release).
            Unregister(owner, name);

            var cmd = new Command { Name = name, Description = description ?? string.Empty, Owner = owner, Action = action, Func = func };

            if (!builtin)
            {
                string q = ShortId(owner) + "." + name;
                if (_commands.TryGetValue(q, out var other) && other.Owner != owner)
                    _log.LogWarning($"Console command '{q}' (from '{owner}') clashes with '{other.Owner}'; qualified name not registered.");
                else { cmd.Qualified = q; _commands[q] = cmd; }
            }

            if (_commands.TryGetValue(name, out var existing))
            {
                if (cmd.Qualified == null)
                {
                    _log.LogWarning($"Console command '{name}' (from '{owner}') clashes with '{existing.Owner}' and has no free qualified name; not registered.");
                    return;
                }
                _log.LogWarning($"Console command '{name}' (from '{owner}') is already registered by '{existing.Owner}'; use '{cmd.Qualified}'.");
            }
            else
            {
                _commands[name] = cmd;
                cmd.HasBare = true;
            }
        }

        /// <summary>Remove a command <paramref name="owner"/> registered (all its names). Other owners' commands are untouched.</summary>
        public void Unregister(string owner, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            Command target = null;
            if (_commands.TryGetValue(name.Trim(), out var c) && c.Owner == owner) target = c;
            else
            {
                // the owner may pass the qualified name, or its bare name may be owned by someone else
                foreach (var x in _commands.Values)
                    if (x.Owner == owner && (string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Qualified, name, StringComparison.OrdinalIgnoreCase))) { target = x; break; }
            }
            if (target == null) return;
            var keys = new List<string>();
            foreach (var kv in _commands) if (ReferenceEquals(kv.Value, target)) keys.Add(kv.Key);
            foreach (var k in keys) _commands.Remove(k);
        }

        /// <summary>Remove everything <paramref name="owner"/> registered.</summary>
        public void UnregisterOwner(string owner)
        {
            var keys = new List<string>();
            foreach (var kv in _commands) if (kv.Value.Owner == owner) keys.Add(kv.Key);
            foreach (var k in keys) _commands.Remove(k);
        }

        /// <summary>Find by bare or qualified name (case-insensitive).</summary>
        public Command Find(string name) => name != null && _commands.TryGetValue(name, out var c) ? c : null;

        // ---- execution --------------------------------------------------------------------------

        /// <summary>Run a line (several commands separated by <c>;</c>). Returns false if any command was unknown or threw.</summary>
        public bool Execute(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return true;
            if (_depth >= 8) { PrintError("Console recursion limit reached."); return false; }
            bool ok = true;
            _depth++;
            try
            {
                foreach (var p in CommandLine.Parse(commandLine))
                {
                    var cmd = Find(p.Name);
                    if (cmd == null)
                    {
                        PrintError($"Unknown command '{Escape(p.Name)}'. Type help.");
                        ok = false;
                        continue;
                    }
                    try
                    {
                        if (cmd.Action != null) cmd.Action(p.Args);
                        else
                        {
                            string text = cmd.Func(p.Args);
                            if (!string.IsNullOrEmpty(text)) Print(text);
                        }
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        PrintError($"'{Escape(p.Name)}' failed: {Escape(ex.Message)}");
                        try { _log.LogError($"Console command '{p.Name}' ({cmd.Owner}) threw: {ex}"); } catch { }
                    }
                }
            }
            finally { _depth--; }
            return ok;
        }

        // ---- output -----------------------------------------------------------------------------

        /// <summary>Increments whenever the buffer changes (the window re-renders on change).</summary>
        public int Version { get { lock (_bufferLock) return _lines.Version; } }

        /// <summary>Copy the buffered lines (oldest first) into <paramref name="into"/> (cleared first).</summary>
        public void Snapshot(List<string> into)
        {
            into.Clear();
            lock (_bufferLock) for (int i = 0; i < _lines.Count; i++) into.Add(_lines[i]);
        }

        /// <summary>Write text (may contain TMP rich text and newlines) to the console buffer and mirror it to the BepInEx log.</summary>
        public void Print(string text)
        {
            if (text == null) text = string.Empty;
            AddLines(text);
            try { _log.LogInfo("[console] " + ConsoleText.Strip(text)); } catch { }
        }

        /// <summary>Print a red line.</summary>
        public void PrintError(string text) => Print(ConsoleText.Color(ErrorColor, text));

        /// <summary>Print a warning-coloured line.</summary>
        public void PrintWarning(string text) => Print(ConsoleText.Color(WarnColor, text));

        /// <summary>Add text to the buffer only (echo of typed input, mirrored log lines): not written to the log.</summary>
        public void AddLines(string text)
        {
            if (text == null) return;
            lock (_bufferLock)
            {
                int start = 0;
                for (int i = 0; i <= text.Length; i++)
                {
                    if (i == text.Length || text[i] == '\n')
                    {
                        int end = i > start && text[i - 1] == '\r' ? i - 1 : i;
                        _lines.Add(text.Substring(start, end - start));
                        start = i + 1;
                    }
                }
            }
        }

        /// <summary>Empty the console buffer.</summary>
        public void ClearBuffer() { lock (_bufferLock) _lines.Clear(); }

        private static string Escape(string s) => ConsoleText.Literal(s);
    }

    /// <summary>Per-mod <see cref="IAslCommands"/> facade over the shared <see cref="CommandRegistry"/>.</summary>
    internal sealed class ModCommands : IAslCommands
    {
        private readonly CommandRegistry _reg;
        private readonly string _modId;

        public ModCommands(CommandRegistry reg, string modId)
        {
            _reg = reg;
            _modId = modId;
        }

        public void Register(string name, string description, Action<string[]> handler) => _reg.Register(_modId, name, description, handler, null);
        public void Register(string name, string description, Func<string[], string> handler) => _reg.Register(_modId, name, description, null, handler);
        public void Unregister(string name) => _reg.Unregister(_modId, name);

        /// <summary>Returns false if any command in the line was unknown or failed.</summary>
        public bool Execute(string commandLine) => _reg.Execute(commandLine);
        public void Print(string text) => _reg.Print(text);
    }
}
