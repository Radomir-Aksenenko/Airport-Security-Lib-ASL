using System;
using System.Collections.Generic;

namespace ASL.Api
{
    /// <summary>
    /// A small host-authoritative synced key/value store, shared across everyone in the session — get
    /// one with <c>ctx.Net.GetSync(id)</c>. The <b>host</b> sets values; they replicate to every client
    /// automatically, and a client that joins late receives the full current state. Both ends read with
    /// <see cref="Get"/> / <see cref="All"/> and react to <see cref="Changed"/>. Values are strings (pack
    /// numbers/JSON yourself if you need more). Built on the message transport, so it carries the same
    /// experimental status for send until a two-peer round trip is confirmed.
    /// </summary>
    public interface IAslSync
    {
        /// <summary>The id this store was created with (namespace it with your mod id).</summary>
        string Id { get; }

        /// <summary>
        /// Set <paramref name="key"/> to <paramref name="value"/> and replicate to all clients.
        /// <b>Host only</b> — on a client this is ignored and returns false. Returns false if the
        /// transport is unavailable (the value is still applied locally on the host).
        /// </summary>
        bool Set(string key, string value);

        /// <summary>The value for <paramref name="key"/>, or null if it isn't set.</summary>
        string Get(string key);

        /// <summary>Try-get variant of <see cref="Get"/>.</summary>
        bool TryGet(string key, out string value);

        /// <summary>True if <paramref name="key"/> has a value.</summary>
        bool Contains(string key);

        /// <summary>A snapshot copy of all current key/value pairs.</summary>
        IReadOnlyDictionary<string, string> All { get; }

        /// <summary>
        /// Fires when a value changes: on the host when it <see cref="Set"/>s, and on a client when an
        /// update (or the initial snapshot) arrives. Runs on the main thread. Args are (key, newValue).
        /// </summary>
        event Action<string, string> Changed;
    }
}
