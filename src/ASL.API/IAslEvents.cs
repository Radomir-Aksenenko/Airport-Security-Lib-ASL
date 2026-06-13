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

        // Domain hooks (e.g. contraband scans, NPC spawns) arrive in a later phase via an
        // opt-in, collision-safe mechanism — not blanket Harmony patches on hot methods.
    }
}
