using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Shared dev-console command registry. Owned by the Console agent (tokenizer, built-ins, console
    /// window). Placeholder: commands are stored but <see cref="Execute"/> does not run them yet.
    /// </summary>
    internal sealed class CommandRegistry
    {
        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

        public CommandRegistry(ManualLogSource log) => _log = log;

        internal sealed class Command
        {
            public string Name;
            public string Description;
            public string Owner;
            public Action<string[]> Action;
            public Func<string[], string> Func;
        }

        public IReadOnlyDictionary<string, Command> Commands => _commands;

        public void Register(string owner, string name, string description, Action<string[]> action, Func<string[], string> func)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_commands.ContainsKey(name))
            {
                _log.LogWarning($"Console command '{name}' (from '{owner}') clashes with an existing command; not registered.");
                return;
            }
            _commands[name] = new Command { Name = name, Description = description, Owner = owner, Action = action, Func = func };
        }

        public void Unregister(string owner, string name)
        {
            if (name != null && _commands.TryGetValue(name, out var c) && c.Owner == owner) _commands.Remove(name);
        }

        public bool Execute(string commandLine) => false;   // TODO(Console agent)

        public void Print(string text) => _log.LogInfo($"[console] {text}");
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
        public bool Execute(string commandLine) => _reg.Execute(commandLine);
        public void Print(string text) => _reg.Print(text);
    }
}
