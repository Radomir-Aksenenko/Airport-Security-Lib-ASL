using Metater;

namespace ASL.Api
{
    /// <summary>
    /// A player in the current networked session, surfaced through <c>ctx.Net</c> (see
    /// <see cref="IAslNet.Players"/>). Wraps the game's own player object plus its Mirror identity, so a
    /// mod can tell players apart, map a received message back to who sent it, and reach into the game
    /// player to read or change its state.
    /// </summary>
    public interface IAslPlayer
    {
        /// <summary>
        /// The game's player component (<see cref="MetaPlayer"/>) — the real object, for reading or
        /// manipulating that player's in-game state. May become invalid after the player leaves.
        /// </summary>
        MetaPlayer Player { get; }

        /// <summary>The player object's Mirror network id (stable for the session, same on every peer).</summary>
        uint NetId { get; }

        /// <summary>
        /// Server-side connection id of this player (matches <see cref="AslNetMessage.SenderConnectionId"/>
        /// for messages they send), or <c>-1</c> when it isn't known on this peer (e.g. a remote player as
        /// seen from a client). Use it on the server to reply to a specific player.
        /// </summary>
        int ConnectionId { get; }

        /// <summary>True if this is the local player (you).</summary>
        bool IsLocal { get; }

        /// <summary>
        /// Display name, best-effort. The local player's name is filled in; remote players' names may be
        /// empty for now (a later version sources them from Steam). Never null.
        /// </summary>
        string Name { get; }
    }
}
