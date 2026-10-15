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
        public IAslConfig Config { get; }
        public IAslStorage Storage { get; }
        public IAslTimers Timers { get; }
        public IAslMods Mods { get; }
        public IAslCommands Commands { get; }
        public IAslAssets Assets { get; }
        public IAslLocalization Loc { get; }
        public IModInfo Info { get; }

        /// <summary>The per-mod service objects this context was built from (kept for release on unload/reload).</summary>
        internal ModServices Services { get; }

        public ModContext(string modId, string modName, string modDirectory,
                          ManualLogSource logSource, ModServices services, IModHooks hooks, IModMenu menu,
                          IAslInput input, IAslNet net)
        {
            ModId = modId;
            ModName = modName;
            ModDirectory = modDirectory;
            Log = new ModLogger(logSource);
            Services = services;
            Events = services.Events;
            Hooks = hooks;
            Menu = menu;
            Ui = services.Ui;
            Input = input;
            Net = net;
            Config = services.Config;
            Storage = services.Storage;
            Timers = services.Timers;
            Mods = services.Mods;
            Commands = services.Commands;
            Assets = services.Assets;
            Loc = services.Loc;
            Info = services.Info;
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
