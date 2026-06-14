using ASL.Api;
using Metater;

namespace ASL
{
    /// <summary>Immutable snapshot of a player, handed to mods as <see cref="IAslPlayer"/>.</summary>
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
    }
}
