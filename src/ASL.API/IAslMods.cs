using System;
using System.Collections.Generic;

namespace ASL.Api
{
    /// <summary>The lifecycle state of a mod as seen by ASL.</summary>
    public enum ModState
    {
        /// <summary>Loaded and running.</summary>
        Loaded,
        /// <summary>Failed to load (see <see cref="IModInfo.Error"/>).</summary>
        Failed,
        /// <summary>Switched off by the player in the Mod Manager (applies on next launch).</summary>
        Disabled,
        /// <summary>A required dependency is missing or failed.</summary>
        MissingDependency,
        /// <summary>The mod requires a newer ASL than the one installed.</summary>
        Incompatible,
    }

    /// <summary>Read-only information about one mod (from its manifest plus its runtime state).</summary>
    public interface IModInfo
    {
        /// <summary>Unique mod id.</summary>
        string Id { get; }
        /// <summary>Display name.</summary>
        string Name { get; }
        /// <summary>Version string from the manifest.</summary>
        string Version { get; }
        /// <summary>Author (may be null).</summary>
        string Author { get; }
        /// <summary>Description (may be null).</summary>
        string Description { get; }
        /// <summary><c>"dll"</c>, <c>"script"</c> or <c>"content"</c>.</summary>
        string Kind { get; }
        /// <summary>Absolute mod folder.</summary>
        string Directory { get; }
        /// <summary>Current state.</summary>
        ModState State { get; }
        /// <summary>Why the mod is not <see cref="ModState.Loaded"/> (null when fine).</summary>
        string Error { get; }
        /// <summary>Hard dependencies from the manifest, as written (<c>"modId"</c> or <c>"modId@&gt;=1.2.0"</c>).</summary>
        IReadOnlyList<string> Dependencies { get; }
        /// <summary>Tags from the manifest.</summary>
        IReadOnlyList<string> Tags { get; }
    }

    /// <summary>
    /// Other mods, reached through <c>ctx.Mods</c>: who is installed, shared services, and a simple
    /// cross-mod message bus. Services and subscriptions are released automatically when your mod unloads.
    /// <code>
    /// // Mod A (publishes):   ctx.Mods.Provide&lt;IEconomy&gt;(new Economy());
    /// // Mod B (consumes):    public override void OnAllModsLoaded() { econ = ctx.Mods.GetService&lt;IEconomy&gt;(); }
    /// </code>
    /// </summary>
    public interface IAslMods
    {
        /// <summary>Every mod ASL found, including failed and disabled ones.</summary>
        IReadOnlyList<IModInfo> All { get; }

        /// <summary>True if a mod with this id is in state <see cref="ModState.Loaded"/>.</summary>
        bool IsLoaded(string modId);

        /// <summary>The mod with this id, or null.</summary>
        IModInfo Get(string modId);

        /// <summary>Publish an object of type <typeparamref name="T"/> for other mods to find with <see cref="GetService{T}"/>.</summary>
        void Provide<T>(T service) where T : class;

        /// <summary>The first published service of type <typeparamref name="T"/>, or that of mod <paramref name="fromModId"/>; null if none.</summary>
        T GetService<T>(string fromModId = null) where T : class;

        /// <summary>Send <paramref name="payload"/> to every subscriber of <paramref name="topic"/> (synchronous; one throwing handler does not affect others).</summary>
        void Publish(string topic, object payload = null);

        /// <summary>Listen to a topic. Dispose the result to unsubscribe.</summary>
        IDisposable Subscribe(string topic, Action<object> handler);

        /// <summary>Raised once after all mods have loaded.</summary>
        event Action AllModsLoaded;
    }
}
