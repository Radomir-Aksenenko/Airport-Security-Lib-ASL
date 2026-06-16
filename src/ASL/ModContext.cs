using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>Concrete <see cref="IModContext"/> handed to each loaded mod.</summary>
    internal sealed class ModContext : IModContext
    {
        public string ModId { get; }
        public string ModName { get; }
        public string ModDirectory { get; }
        public IModLogger Log { get; }
        public IAslEvents Events { get; }
        public IModHooks Hooks { get; }
        public IModMenu Menu { get; }
        public IAslUi Ui { get; }
        public IAslInput Input { get; }
        public IAslNet Net { get; }

        public ModContext(string modId, string modName, string modDirectory,
                          ManualLogSource logSource, IAslEvents events, IModHooks hooks, IModMenu menu,
                          IAslUi ui, IAslInput input, IAslNet net)
        {
            ModId = modId;
            ModName = modName;
            ModDirectory = modDirectory;
            Log = new ModLogger(logSource);
            Events = events;
            Hooks = hooks;
            Menu = menu;
            Ui = ui;
            Input = input;
            Net = net;
        }
    }

    /// <summary>Bridges the framework-agnostic <see cref="IModLogger"/> onto a BepInEx log source.</summary>
    internal sealed class ModLogger : IModLogger
    {
        private readonly ManualLogSource _log;

        public ModLogger(ManualLogSource log) => _log = log;

        public void Info(string message) => _log.LogInfo(message);
        public void Warning(string message) => _log.LogWarning(message);
        public void Error(string message) => _log.LogError(message);
    }
}
