using Metater;
using UnityEngine;

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

        // ---- Look + control. These act live on the underlying game player; the look helper uses the
        //      local camera, so it is meaningful for the local player. ----

        /// <summary>
        /// Raycast from the player's camera and report what it hits within <paramref name="maxDistance"/>
        /// metres (see <see cref="LookHit"/>). Uses the local view, so call it on the local player.
        /// </summary>
        LookHit GetLookedAt(float maxDistance = 6f);

        /// <summary>
        /// Freeze the player in place — stops movement <i>and</i> gravity, so they can hang where they are
        /// (e.g. against a wall). Idempotent; undo with <see cref="Unfreeze"/>.
        /// </summary>
        void Freeze();

        /// <summary>Undo <see cref="Freeze"/> — movement and gravity resume.</summary>
        void Unfreeze();

        /// <summary>True while frozen by <see cref="Freeze"/>.</summary>
        bool IsFrozen { get; }

        /// <summary>Teleport the player to a world position.</summary>
        void Teleport(Vector3 position);

        /// <summary>
        /// Shrink (or grow) the player's movement collider to <paramref name="radius"/> × <paramref name="height"/>
        /// — e.g. so a small disguise can fit through small gaps. Originals are remembered; call
        /// <see cref="ResetCollider"/> to restore. Only affects the local player's own physics.
        /// </summary>
        void SetColliderSize(float radius, float height);

        /// <summary>Restore the collider after <see cref="SetColliderSize"/>.</summary>
        void ResetCollider();
    }
}
