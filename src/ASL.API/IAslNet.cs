using System;

namespace ASL.Api
{
    /// <summary>
    /// Read-only networking awareness for mods (the game uses Mirror). Lets a mod know whether it is
    /// the host/server, a client, connected, and how many clients are connected, and react when that
    /// changes — without touching Mirror directly.
    ///
    /// Note: ASL does not yet expose a custom message <i>transport</i> (sending arbitrary data between
    /// clients). On this Unity 6 / IL2CPP build Mirror's generic message API can't serialize new
    /// mod-defined message types, so cross-client sync needs a more involved (and multiplayer-tested)
    /// path — see the docs. This interface is the safe, available subset.
    /// </summary>
    public interface IAslNet
    {
        /// <summary>True if any networking is active (we are a server and/or a client).</summary>
        bool IsOnline { get; }

        /// <summary>True if this process is running the server (host or dedicated).</summary>
        bool IsServer { get; }

        /// <summary>True if this process is running a client (including the host's local client).</summary>
        bool IsClient { get; }

        /// <summary>True if this process is the host (server + local client).</summary>
        bool IsHost { get; }

        /// <summary>True if our client is connected to a server.</summary>
        bool IsConnectedClient { get; }

        /// <summary>Server-side: number of connected client connections (0 when not the server).</summary>
        int ConnectionCount { get; }

        /// <summary>
        /// Server-side: fires with the new connection count whenever it changes (a client joined or
        /// left). Detected by a lightweight poll, so handlers run on the main thread.
        /// </summary>
        event Action<int> ConnectionsChanged;
    }
}
