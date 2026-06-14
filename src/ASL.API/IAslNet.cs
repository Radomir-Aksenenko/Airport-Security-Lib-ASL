using System;
using System.Collections.Generic;

namespace ASL.Api
{
    /// <summary>
    /// Networking for mods (the game uses Mirror). Two layers:
    /// <list type="bullet">
    /// <item><b>Awareness</b> — read-only state (host/client/connected, connection count) and a
    /// connection-count change event. Pure Mirror reads, no risk.</item>
    /// <item><b>Message transport</b> — send arbitrary bytes between the host and clients on named
    /// channels (<see cref="Send"/> / <see cref="Subscribe"/>). ASL tunnels mod payloads through a
    /// Mirror message the game already ships, so it works around the IL2CPP limit that new
    /// mod-defined message types have no serializer. See <see cref="MessagingAvailable"/>.</item>
    /// </list>
    /// All of this is reached through <c>ctx.Net</c>; mods never touch Mirror directly.
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

        // ---- Message transport ----

        /// <summary>
        /// True if the custom-message transport could be installed (its Mirror intercepts are in
        /// place). It is set up lazily on first <see cref="Subscribe"/>/<see cref="Send"/>; reading
        /// this triggers that setup. If it is false, the <c>Send*</c> calls below are no-ops that
        /// return false — gate your networking on it.
        /// </summary>
        bool MessagingAvailable { get; }

        /// <summary>
        /// Send <paramref name="data"/> on a named <paramref name="channel"/> to the other side,
        /// routed automatically: from a client it goes to the server; from the server/host it is
        /// broadcast to every connected client (including the host's own client). For explicit
        /// targeting use <see cref="SendToServer"/>, <see cref="SendToAll"/>, or
        /// <see cref="SendToClient"/>. Returns false if not online or the transport is unavailable.
        /// </summary>
        /// <remarks>
        /// Both peers must run a mod that subscribes to the same <paramref name="channel"/>. Keep
        /// messages small; they ride Mirror's reliable channel. Pick a channel name unique to your
        /// mod (e.g. <c>"com.author.mymod/state"</c>) to avoid clashing with other mods.
        /// </remarks>
        bool Send(string channel, byte[] data);

        /// <summary>Client → server. Returns false unless our client is connected.</summary>
        bool SendToServer(string channel, byte[] data);

        /// <summary>Server → all connected clients (incl. the host's own client). Returns false unless we are the server.</summary>
        bool SendToAll(string channel, byte[] data);

        /// <summary>
        /// Server → one specific client, identified by its Mirror connection id (e.g.
        /// <see cref="AslNetMessage.SenderConnectionId"/> from a message it sent). Returns false
        /// unless we are the server and the connection exists.
        /// </summary>
        bool SendToClient(int connectionId, string channel, byte[] data);

        /// <summary>
        /// Subscribe to messages arriving on <paramref name="channel"/>. The handler runs on the
        /// main thread, so it is safe to touch Unity/game state from it — keep it fast. Subscribing
        /// installs the transport if it is not already up.
        /// </summary>
        void Subscribe(string channel, Action<AslNetMessage> handler);

        /// <summary>Remove a handler previously added with <see cref="Subscribe"/>.</summary>
        void Unsubscribe(string channel, Action<AslNetMessage> handler);

        // ---- Typed messages (no manual byte packing) ----

        /// <summary>
        /// Send a typed <see cref="IAslMessage"/> on a named <paramref name="channel"/>, auto-routed
        /// like <see cref="Send(string, byte[])"/> (client→server, or host→all). ASL serializes
        /// <paramref name="message"/> for you. Receive it with <see cref="Subscribe{T}"/> on the same
        /// channel. Returns false if not online or the transport is unavailable.
        /// </summary>
        bool Send<T>(string channel, T message) where T : IAslMessage;

        /// <summary>Typed <see cref="SendToServer"/>: client → server.</summary>
        bool SendToServer<T>(string channel, T message) where T : IAslMessage;

        /// <summary>Typed <see cref="SendToAll"/>: server → all clients (incl. the host's own client).</summary>
        bool SendToAll<T>(string channel, T message) where T : IAslMessage;

        /// <summary>Typed <see cref="SendToClient"/>: server → one client by connection id.</summary>
        bool SendToClient<T>(int connectionId, string channel, T message) where T : IAslMessage;

        /// <summary>
        /// Subscribe to typed messages of type <typeparamref name="T"/> on <paramref name="channel"/>.
        /// ASL constructs a <typeparamref name="T"/>, deserializes the payload into it, and hands it to
        /// <paramref name="handler"/> along with the raw <see cref="AslNetMessage"/> (for the sender's
        /// connection id, etc.). Runs on the main thread. A malformed payload is logged and skipped, not
        /// thrown at you. <typeparamref name="T"/> needs a public parameterless constructor.
        /// </summary>
        void Subscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new();

        /// <summary>Remove a typed handler previously added with <see cref="Subscribe{T}"/>.</summary>
        void Unsubscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new();

        // ---- Player identity ----

        /// <summary>
        /// A snapshot of the players currently in the session (empty when not online). Each entry pairs
        /// the game player object with its Mirror identity — see <see cref="IAslPlayer"/>. The list is
        /// rebuilt on read, so capture it if you need a stable view.
        /// </summary>
        IReadOnlyList<IAslPlayer> Players { get; }

        /// <summary>The local player (you), or null if there isn't one yet.</summary>
        IAslPlayer LocalPlayer { get; }

        /// <summary>
        /// Find a player by their server-side connection id (e.g. an
        /// <see cref="AslNetMessage.SenderConnectionId"/> you received), or null if none matches. Most
        /// useful on the server to act on whoever sent a message.
        /// </summary>
        IAslPlayer GetPlayer(int connectionId);

        /// <summary>Fires when a player joins the session. Runs on the main thread (detected by a poll).</summary>
        event Action<IAslPlayer> PlayerJoined;

        /// <summary>
        /// Fires when a player leaves. Runs on the main thread. The supplied <see cref="IAslPlayer"/> is a
        /// last-known snapshot — its <see cref="IAslPlayer.Player"/> may already be destroyed.
        /// </summary>
        event Action<IAslPlayer> PlayerLeft;

        // ---- Synced state ----

        /// <summary>
        /// Get (or create) a host-authoritative synced key/value store identified by <paramref name="id"/>
        /// — see <see cref="IAslSync"/>. The same id returns the same store; namespace it with your mod id
        /// (e.g. <c>"com.author.mymod/state"</c>) so mods don't share a store by accident.
        /// </summary>
        IAslSync GetSync(string id);
    }
}
