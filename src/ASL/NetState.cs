using System;
using ASL.Api;
using BepInEx.Logging;
using Mirror;

namespace ASL
{
    /// <summary>
    /// Implements <see cref="IAslNet"/> by reading Mirror's public state. Everything here is a plain
    /// property read or a count poll — no generic message methods, no delegate marshalling, no
    /// Harmony patches — so it is safe and stable on this IL2CPP build. <see cref="Poll"/> is driven
    /// from the framework's throttled tick to detect connection-count changes.
    /// </summary>
    internal sealed class NetState : IAslNet
    {
        private readonly ManualLogSource _log;
        private int _lastCount = -1;

        public NetState(ManualLogSource log) => _log = log;

        public bool IsServer => Safe(() => NetworkServer.active);
        public bool IsClient => Safe(() => NetworkClient.active);
        public bool IsHost => IsServer && IsClient;
        public bool IsConnectedClient => Safe(() => NetworkClient.isConnected);
        public bool IsOnline => IsServer || IsClient;

        public int ConnectionCount
        {
            get
            {
                try
                {
                    if (!NetworkServer.active) return 0;
                    var conns = NetworkServer.connections;
                    return conns != null ? conns.Count : 0;
                }
                catch { return 0; }
            }
        }

        public event Action<int> ConnectionsChanged;

        public void Poll()
        {
            int n = ConnectionCount;
            if (n == _lastCount) return;
            _lastCount = n;

            var d = ConnectionsChanged;
            if (d == null) return;
            foreach (var h in d.GetInvocationList())
            {
                try { ((Action<int>)h)(n); }
                catch (Exception ex) { _log.LogError($"Net 'ConnectionsChanged' handler threw: {ex.Message}"); }
            }
        }

        private static bool Safe(Func<bool> f)
        {
            try { return f(); }
            catch { return false; }
        }
    }
}
