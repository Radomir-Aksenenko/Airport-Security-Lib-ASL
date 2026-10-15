using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// The bag of per-mod service objects behind <see cref="IModContext"/>, plus the factory that builds
    /// them. <see cref="ModLoader"/> calls <see cref="Create"/> once per mod and hands the result to
    /// <see cref="ModContext"/>, so neither of those has to change when a service implementation is
    /// swapped or extended: subsystem agents edit their own class file and, at most, the one line here.
    /// All constructors must be cheap and side-effect free (they run for every mod at boot).
    /// </summary>
    internal sealed class ModServices
    {
        public IAslEvents Events { get; private set; }
        public IAslUi Ui { get; private set; }
        public IAslConfig Config { get; private set; }
        public IAslStorage Storage { get; private set; }
        public IAslTimers Timers { get; private set; }
        public IAslMods Mods { get; private set; }
        public IAslCommands Commands { get; private set; }
        public IAslAssets Assets { get; private set; }
        public IAslLocalization Loc { get; private set; }
        public IModInfo Info { get; private set; }

        /// <summary>Create every per-mod service for one mod.</summary>
        /// <param name="info">The mod's registry record (also exposed as <c>ctx.Info</c>).</param>
        /// <param name="displayName">The mod's display name (used in logs/menu).</param>
        /// <param name="modLog">The per-mod BepInEx log source.</param>
        /// <param name="sharedBus">The shared event bus the per-mod <see cref="ModEvents"/> proxies.</param>
        /// <param name="sharedUi">The shared announcement-banner helper.</param>
        public static ModServices Create(ModInfo info, string displayName, ManualLogSource modLog, IAslEvents sharedBus, UiManager sharedUi)
        {
            var id = info.Id;
            var dir = info.Directory;
            return new ModServices
            {
                Events = new ModEvents(id, sharedBus),
                Ui = new ModUi(id, displayName, dir, sharedUi, modLog),
                Config = new ModConfig(id, modLog),
                Storage = new ModStorage(id, modLog),
                Timers = new ModTimers(id, modLog),
                Mods = new ModMods(AslPlugin.Registry, id),
                Commands = new ModCommands(AslPlugin.CommandHub, id),
                Assets = new ModAssets(id, dir, modLog),
                Loc = new ModLocalization(id, dir, modLog),
                Info = info,
            };
        }
    }
}
