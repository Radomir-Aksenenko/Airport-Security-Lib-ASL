using System;

namespace ASL.Api
{
    /// <summary>
    /// The developer console, reached through <c>ctx.Commands</c>. Register commands that players (or you)
    /// can type into the in-game console (default key: <c>`</c>). Commands are global; if two mods use the
    /// same name, the first keeps the bare name and the other stays reachable as
    /// <c>&lt;modId&gt;.&lt;name&gt;</c>. Your commands are removed when your mod unloads.
    /// <code>
    /// ctx.Commands.Register("give", "give &lt;item&gt; [count]", args => Give(args[0], args.Length &gt; 1 ? int.Parse(args[1]) : 1));
    /// ctx.Commands.Register("where", "print position", args => "You are at " + player.position);
    /// </code>
    /// Arguments are split on spaces; "quoted text" stays one argument.
    /// </summary>
    public interface IAslCommands
    {
        /// <summary>Register a command whose handler receives the arguments (without the command name).</summary>
        void Register(string name, string description, Action<string[]> handler);

        /// <summary>Register a command whose returned text (if any) is printed to the console.</summary>
        void Register(string name, string description, Func<string[], string> handler);

        /// <summary>Remove a command this mod registered.</summary>
        void Unregister(string name);

        /// <summary>Run a console line as if the player typed it. Returns false if no such command exists.</summary>
        bool Execute(string commandLine);

        /// <summary>Write a line to the console output.</summary>
        void Print(string text);
    }
}
