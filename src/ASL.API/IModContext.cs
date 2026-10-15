namespace ASL.Api
{
    /// <summary>
    /// Per-mod handle into ASL, passed to <see cref="AslMod.OnLoad"/>. This is the surface
    /// mods are compiled against; it grows over the phases (events, content registry, config,
    /// networking, UI toolkit, timers, ...) without breaking existing members.
    /// </summary>
    public interface IModContext
    {
        /// <summary>Unique mod id from the manifest, e.g. <c>"com.author.mymod"</c>.</summary>
        string ModId { get; }

        /// <summary>Human-readable mod name from the manifest.</summary>
        string ModName { get; }

        /// <summary>Absolute path to this mod's folder under <c>mods/</c>. Load your assets from here.</summary>
        string ModDirectory { get; }

        /// <summary>Per-mod logger; output is tagged with the mod name.</summary>
        IModLogger Log { get; }

        /// <summary>Game events to subscribe to (frame tick, scene changes, player).</summary>
        IAslEvents Events { get; }

        /// <summary>Opt-in Harmony hooks on game methods (advanced; installed only when used).</summary>
        IModHooks Hooks { get; }

        /// <summary>Register controls into ASL's shared in-game menu (toggle with F8).</summary>
        IModMenu Menu { get; }

        /// <summary>On-screen UI helpers (the game's announcement banner).</summary>
        IAslUi Ui { get; }

        /// <summary>Keyboard input and rebindable, conflict-checked named keybinds.</summary>
        IAslInput Input { get; }

        /// <summary>Read-only networking awareness (host/client/connected, connection count).</summary>
        IAslNet Net { get; }

        /// <summary>Typed, persisted per-mod settings (BepInEx <c>.cfg</c>), shown automatically in the F8 menu.</summary>
        IAslConfig Config { get; }

        /// <summary>Per-mod save data (JSON files) that survives mod updates.</summary>
        IAslStorage Storage { get; }

        /// <summary>Delays, intervals, next-frame callbacks, conditions and coroutine-style sequences.</summary>
        IAslTimers Timers { get; }

        /// <summary>Other loaded mods: info, shared services and a cross-mod message bus.</summary>
        IAslMods Mods { get; }

        /// <summary>Developer-console commands.</summary>
        IAslCommands Commands { get; }

        /// <summary>Load textures, sprites, audio, asset bundles and text from your mod folder.</summary>
        IAslAssets Assets { get; }

        /// <summary>Per-mod translations from <c>lang/*.json</c> in your mod folder.</summary>
        IAslLocalization Loc { get; }

        /// <summary>This mod's own manifest info (id, version, state, dependencies, ...).</summary>
        IModInfo Info { get; }
    }

    /// <summary>
    /// Minimal logging surface. Deliberately framework-agnostic so the public API does not
    /// leak BepInEx types onto mod authors.
    /// </summary>
    public interface IModLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
