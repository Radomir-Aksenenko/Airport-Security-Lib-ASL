using ASL.Api;
using Metater;
using UnityEngine;

namespace ASL
{
    /// <summary>Snapshot of a player handed to mods as <see cref="IAslPlayer"/>. The look/control methods
    /// act live on the wrapped <see cref="MetaPlayer"/> (delegated to <see cref="PlayerControl"/>).</summary>
    internal sealed class AslPlayer : IAslPlayer
    {
        public MetaPlayer Player { get; }
        public uint NetId { get; }
        public int ConnectionId { get; }
        public bool IsLocal { get; }
        public string Name { get; }

        public AslPlayer(MetaPlayer player, uint netId, int connectionId, bool isLocal, string name)
        {
            Player = player;
            NetId = netId;
            ConnectionId = connectionId;
            IsLocal = isLocal;
            Name = name ?? string.Empty;
        }

        // ---- look + control ----

        public LookHit GetLookedAt(float maxDistance = 6f) => PlayerControl.GetLookedAt(maxDistance);
        public void Freeze() => PlayerControl.Freeze(Player, true);
        public void Unfreeze() => PlayerControl.Freeze(Player, false);
        public bool IsFrozen => PlayerControl.IsFrozen(Player);
        public void Teleport(Vector3 position) => PlayerControl.Teleport(Player, position);
        public void SetColliderSize(float radius, float height) => PlayerControl.SetColliderSize(Player, NetId, radius, height);
        public void ResetCollider() => PlayerControl.ResetCollider(Player, NetId);
    }
}
