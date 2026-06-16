using System;
using UnityEngine;

namespace ASL.Api
{
    /// <summary>
    /// Per-mod input, reached through <c>ctx.Input</c>. The recommended path is
    /// <see cref="RegisterKey"/>: a <i>named</i>, rebindable key that automatically appears in ASL's
    /// in-game Controls UI (F8 → your mod) with its display name and current key, can be rebound by the
    /// player, is remembered across restarts, and is checked for clashes with other mods' keys (and
    /// warns on keys the game itself uses). For quick one-offs there are raw passthrough queries.
    /// </summary>
    public interface IAslInput
    {
        /// <summary>
        /// Register (or fetch) a rebindable named key. The first call with a given <paramref name="id"/>
        /// creates it bound to <paramref name="defaultKey"/> (or to the player's saved rebind, if any);
        /// later calls with the same id return the same handle. <paramref name="displayName"/> is what
        /// the player sees in the Controls UI. Query the returned handle each frame (e.g. from your
        /// <c>Events.Update</c> handler) via <see cref="IAslKeybind.WasPressed"/>, or subscribe to
        /// <see cref="IAslKeybind.Pressed"/>.
        /// </summary>
        IAslKeybind RegisterKey(string id, string displayName, KeyCode defaultKey);

        /// <summary>Raw passthrough: true on the frame <paramref name="key"/> went down. Unregistered, no conflict checks.</summary>
        bool GetKeyDown(KeyCode key);

        /// <summary>Raw passthrough: true while <paramref name="key"/> is held.</summary>
        bool GetKey(KeyCode key);

        /// <summary>Raw passthrough: true on the frame <paramref name="key"/> was released.</summary>
        bool GetKeyUp(KeyCode key);
    }

    /// <summary>
    /// A registered keybind handle (see <see cref="IAslInput.RegisterKey"/>). The bound
    /// <see cref="Key"/> reflects any rebind the player has done in the Controls UI.
    /// </summary>
    public interface IAslKeybind
    {
        /// <summary>Stable id you passed to <see cref="IAslInput.RegisterKey"/> (namespaced by your mod).</summary>
        string Id { get; }

        /// <summary>Human-readable name shown in the Controls UI.</summary>
        string DisplayName { get; }

        /// <summary>The key currently bound (after any user rebind). <see cref="KeyCode.None"/> if unbound.</summary>
        KeyCode Key { get; }

        /// <summary>True on the frame the bound key went down.</summary>
        bool WasPressed { get; }

        /// <summary>True while the bound key is held.</summary>
        bool IsHeld { get; }

        /// <summary>True on the frame the bound key was released.</summary>
        bool WasReleased { get; }

        /// <summary>True if this key currently clashes with another mod's key or one the game uses.</summary>
        bool HasConflict { get; }

        /// <summary>Fires once each time the bound key goes down (alternative to polling <see cref="WasPressed"/>).</summary>
        event Action Pressed;
    }
}
