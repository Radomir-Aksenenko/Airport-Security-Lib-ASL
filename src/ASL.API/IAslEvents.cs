using System;
using Metater;

namespace ASL.Api
{
    /// <summary>
    /// Game events ASL surfaces to mods. Subscribe inside <see cref="AslMod.OnLoad"/> via
    /// <see cref="IModContext.Events"/>. All handlers are invoked on the game's main thread.
    /// </summary>
    public interface IAslEvents
    {
        /// <summary>Fires once per frame (Unity Update). The workhorse for per-frame mod logic.</summary>
        event Action Update;

        /// <summary>
        /// Fires when the active scene changes; the argument is the new scene name.
        /// Detected by a lightweight poll (a few times per second), not every frame.
        /// </summary>
        event Action<string> SceneChanged;

        /// <summary>
        /// Fires when the local player instance changes — spawned/assigned (non-null) or
        /// cleared (null). Mirrors <c>MetaPlayer.LocalPlayerInstance</c>.
        /// </summary>
        event Action<MetaPlayer> LocalPlayerChanged;

        /// <summary>Fires once per frame after all Update logic (Unity LateUpdate). Keep handlers cheap.</summary>
        event Action LateUpdate;

        /// <summary>Fires when ASL's F8 menu is shown (true) or hidden (false).</summary>
        event Action<bool> MenuVisibilityChanged;

        /// <summary>Fires once, after every mod's <c>OnLoad</c> has run (same moment as <see cref="AslMod.OnAllModsLoaded"/>).</summary>
        event Action AllModsLoaded;

        /// <summary>Fires when the player changes the game language; the argument is the new language token.</summary>
        event Action<string> LanguageChanged;

        /// <summary>Fires when the game is shutting down (or ASL unloads), at most once. On IL2CPP the game's quit callback is best-effort, so this is raised on a best-effort basis from the application-quit message: keep the handler quick (save small state; do not block) and do not rely on it alone for critical data.</summary>
        event Action GameQuitting;

        // Domain hooks (e.g. contraband scans, NPC spawns) arrive in a later phase via an
        // opt-in, collision-safe mechanism — not blanket Harmony patches on hot methods.
    }
}
