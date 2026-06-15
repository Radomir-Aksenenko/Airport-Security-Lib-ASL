using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ASL.Api;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Mirror;

namespace ASL
{
    /// <summary>
    /// Custom message transport over Mirror for mods. New mod-defined Mirror message types can't be
    /// used on this Unity 6 / IL2CPP build (their generic <c>Send&lt;T&gt;</c>/serializer code is
    /// never AOT-compiled), so instead we tunnel mod payloads through <see cref="EntityStateMessage"/>
    /// — a message the game already ships, which carries an <c>ArraySegment&lt;byte&gt;</c> payload and
    /// therefore has compiled send/serialize code.
    ///
    /// Outgoing: pack <c>[magic][version][channel][data]</c> into the payload and stamp a
    /// <see cref="SentinelNetId"/> the server never assigns. Incoming: a Harmony prefix on Mirror's
    /// two <c>OnEntityStateMessage</c> handlers checks that sentinel (one <c>uint</c> compare on the
    /// hot path), and for our packets it decodes, dispatches to subscribers, and consumes the message
    /// (skips the game handler). Everything else passes straight through untouched.
    ///
    /// Installed lazily on first use, so players with no networking mods pay zero netcode overhead.
    /// </summary>
    internal sealed class NetTransport
    {
        // Verbose bring-up diagnostics + extra Mirror sniffer patches. Off for the normal (vanilla)
        // experience — flip on only when debugging the transport against a live session.
        internal static bool Diagnostics = false;
        private static long _clientCalls, _serverCalls, _outDiag;
        private static int _sendDiagCount;

        // Gate #1: a netId Mirror never hands out (it assigns from 1 upward). Cheap hot-path check.
        private const uint SentinelNetId = 0xA51A51A5;

        // Gate #2 (defence in depth): a payload header, so even a freak sentinel collision can't be
        // misread as an ASL packet. 'A','S','L','1'.
        private static readonly byte[] Magic = { 0x41, 0x53, 0x4C, 0x31 };
        private const byte WireVersion = 1;
        private const int HeaderLen = 4 /*magic*/ + 1 /*version*/ + 1 /*channelLen*/;

        // Mirror's reliable, ordered channel. Mod messages should arrive intact and in order.
        private const int ReliableChannel = 0;

        private readonly ManualLogSource _log;
        private readonly Harmony _harmony;
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<Action<AslNetMessage>>> _subs =
            new Dictionary<string, List<Action<AslNetMessage>>>(StringComparer.Ordinal);

        private bool _installAttempted;
        private bool _installed;

        // Resolved lazily on first send: the message id Mirror uses for EntityStateMessage.
        private ushort _entityStateId;
        private bool _idResolved;
        private bool _idWarned;

        // The static Harmony prefixes need to reach the live instance.
        private static NetTransport _active;

        public NetTransport(ManualLogSource log, Harmony harmony)
        {
            _log = log;
            _harmony = harmony;
        }

        public bool MessagingAvailable => EnsureInstalled();

        // ---------------------------------------------------------------- send

        public bool Send(string channel, byte[] data)
        {
            // Auto-route: a server/host fans out to clients, a pure client talks to the server.
            if (Safe(() => NetworkServer.active)) return SendToAll(channel, data);
            if (Safe(() => NetworkClient.active)) return SendToServer(channel, data);
            return false;
        }

        public bool SendToServer(string channel, byte[] data)
        {
            if (!EnsureInstalled()) return false;
            if (!Safe(() => NetworkClient.active && NetworkClient.isConnected)) return false;
            return TrySend(channel, data, batch =>
            {
                var tr = Transport.active;
                if (tr != null) tr.ClientSend(batch, ReliableChannel);   // client -> server, via the transport
            });
        }

        public bool SendToAll(string channel, byte[] data)
        {
            if (!EnsureInstalled()) return false;
            if (!Safe(() => NetworkServer.active)) return false;
            return TrySend(channel, data, batch =>
            {
                var tr = Transport.active;
                if (tr == null) return;
                foreach (var kv in NetworkServer.connections)
                {
                    try
                    {
                        var conn = kv.Value;
                        if (conn != null) tr.ServerSend(conn.connectionId, batch, ReliableChannel);   // server -> each client
                    }
                    catch (Exception ex) { _log.LogError($"Net.SendToAll: a connection send failed: {ex.Message}"); }
                }
            });
        }

        public bool SendToClient(int connectionId, string channel, byte[] data)
        {
            if (!EnsureInstalled()) return false;
            if (!Safe(() => NetworkServer.active)) return false;
            return TrySend(channel, data, batch =>
            {
                var tr = Transport.active;
                if (tr == null) return;
                foreach (var kv in NetworkServer.connections)
                {
                    var conn = kv.Value;
                    if (conn != null && conn.connectionId == connectionId)
                    {
                        tr.ServerSend(connectionId, batch, ReliableChannel);   // server -> one client
                        return;
                    }
                }
                _log.LogWarning($"Net.SendToClient: no connection with id {connectionId}.");
            });
        }

        private bool _stateDumped;
        private void DumpMirrorStateOnce()
        {
            if (_stateDumped) return;
            _stateDumped = true;
            try
            {
                _log.LogInfo($"Net.DIAG state: server.active={Safe(() => NetworkServer.active)} client.active={Safe(() => NetworkClient.active)} client.isConnected={Safe(() => NetworkClient.isConnected)} client.ready={Safe(() => NetworkClient.ready)}");
                try
                {
                    var tr = Transport.active;
                    _log.LogInfo($"Net.DIAG state: Transport.active={(tr != null ? tr.GetType().Name : "NULL")}");
                }
                catch (Exception ex) { _log.LogWarning($"Net.DIAG state Transport.active: {ex.Message}"); }
                try
                {
                    var conns = NetworkServer.connections;
                    _log.LogInfo($"Net.DIAG state: server.connections.Count={(conns != null ? conns.Count : -1)}");
                    if (conns != null)
                        foreach (var kv in conns)
                        {
                            var c = kv.Value;
                            _log.LogInfo($"Net.DIAG state:   server conn id={kv.Key} type={(c != null ? c.GetType().Name : "null")}");
                        }
                }
                catch (Exception ex) { _log.LogWarning($"Net.DIAG state conns: {ex.Message}"); }
                try
                {
                    var lc = NetworkClient.connection;
                    _log.LogInfo($"Net.DIAG state: client.connection type={(lc != null ? lc.GetType().Name : "null")}");
                }
                catch (Exception ex) { _log.LogWarning($"Net.DIAG state clientconn: {ex.Message}"); }
            }
            catch (Exception ex) { _log.LogWarning($"Net.DIAG state: {ex.Message}"); }
        }

        private bool TrySend(string channel, byte[] data, Action<Il2CppSystem.ArraySegment<byte>> rawSender)
        {
            if (string.IsNullOrEmpty(channel)) { _log.LogError("Net.Send: channel is null/empty."); return false; }
            if (!ResolveEntityStateId())   // lazy: Mirror only registers the id once a session is up
            {
                if (!_idWarned) { _idWarned = true; _log.LogWarning("Net.Send: EntityStateMessage id not registered yet (no active session?); send skipped."); }
                return false;
            }
            if (Diagnostics) DumpMirrorStateOnce();
            try
            {
                byte[] payload = Encode(channel, data ?? Array.Empty<byte>());
                var batch = BuildBatch(payload);
                if (Diagnostics && _sendDiagCount++ < 8) _log.LogInfo($"Net.DIAG send ch='{channel}' payloadLen={payload.Length} batchLen={batch.Count} id=0x{_entityStateId:X4}");
                rawSender(batch);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError($"Net.Send on channel '{channel}' failed: {ex.Message}");
                return false;
            }
        }

        // Build a complete Mirror transport batch carrying one EntityStateMessage, using Mirror's own
        // writers/compression so the framing is exact. We send this straight to Transport.active
        // (ClientSend/ServerSend) — the only send path that is neither a generic Send&lt;T&gt; (which
        // IL2CPP interop won't dispatch) nor an inlined-away internal (which can't be reached).
        // Batch layout: [double localTime][CompressVarUInt(msgLen)][message], where
        // message = [ushort EntityStateMessage-id][uint netId][CompressVarUInt(payload.Count+1)][payload].
        private Il2CppSystem.ArraySegment<byte> BuildBatch(byte[] payload)
        {
            // EntityStateMessage on the wire is [CompressVarUInt netId][CompressVarUInt(count+1)][bytes]
            // — netId is var-compressed, NOT a fixed uint (the game's _Read reads it that way). The
            // message is prefixed with its 2-byte id; the whole thing is length-prefixed inside the batch.
            int netIdSize = (int)Compression.VarUIntSize(SentinelNetId);
            int sizePrefix = (int)Compression.VarUIntSize((ulong)(payload.Length + 1));
            int msgLen = 2 + netIdSize + sizePrefix + payload.Length;

            var wb = new NetworkWriter();
            NetworkWriterExtensions.WriteDouble(wb, NetworkTime.localTime);   // batch timestamp header
            Compression.CompressVarUInt(wb, (ulong)msgLen);                   // per-message length prefix
            NetworkWriterExtensions.WriteUShort(wb, _entityStateId);          // message id
            Compression.CompressVarUInt(wb, SentinelNetId);                   // netId (var-compressed)
            var nativePayload = new Il2CppStructArray<byte>(payload);
            NetworkWriterExtensions.WriteBytesAndSize(wb, nativePayload, 0, payload.Length);
            return wb.ToArraySegment();
        }

        // Build one of our batches and parse it straight back through Mirror's own reader exactly as the
        // game does on receive (skip timestamp + length + message id, then DecompressVarUInt netId,
        // ReadBytesAndSize payload). If this round-trips, the wire format is correct — provable solo.
        private void RunWireSelfCheck()
        {
            try
            {
                byte[] testPayload = Encode("asl/wirecheck", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x42 });
                var batch = BuildBatch(testPayload);

                var reader = new NetworkReader(batch);
                NetworkReaderExtensions.ReadDouble(reader);                 // batch timestamp
                ulong msgLen = Compression.DecompressVarUInt(reader);       // per-message length
                ushort id = NetworkReaderExtensions.ReadUShort(reader);     // message id
                ulong netId = Compression.DecompressVarUInt(reader);        // EntityStateMessage.netId
                var payloadSeg = NetworkReaderExtensions.ReadBytesAndSize(reader);

                bool netOk = (uint)netId == SentinelNetId;
                bool payloadOk = payloadSeg != null && payloadSeg.Length == testPayload.Length;
                if (payloadOk)
                    for (int i = 0; i < testPayload.Length; i++) if (payloadSeg[i] != testPayload[i]) { payloadOk = false; break; }

                if (netOk && payloadOk)
                    _log.LogInfo($"NET WIRE SELF-CHECK: PASS — batch parses with Mirror's reader (netId=0x{(uint)netId:X8}, msgLen={msgLen}, id=0x{id:X4}).");
                else
                    _log.LogError($"NET WIRE SELF-CHECK: FAIL — netId=0x{(uint)netId:X8} (expect 0x{SentinelNetId:X8}), payload {(payloadOk ? "ok" : "MISMATCH")}.");
            }
            catch (Exception ex) { _log.LogError($"NET WIRE SELF-CHECK: FAIL — {ex.Message}"); }
        }

        // The message id Mirror assigns to EntityStateMessage, read from its own registry at runtime
        // (version-proof — no hashing assumptions). Mirror only populates Lookup once a session
        // registers its handlers, so this is resolved lazily and cached on first success.
        private bool ResolveEntityStateId()
        {
            if (_idResolved) return true;
            try
            {
                var lookup = NetworkMessages.Lookup;   // Dictionary<ushort, Type>
                if (lookup != null)
                {
                    var en = lookup.GetEnumerator();
                    while (en.MoveNext())
                    {
                        var cur = en.Current;
                        string name = null;
                        try { var ty = cur.Value; name = ty != null ? ty.FullName : null; } catch { /* skip */ }
                        if (name != null && name.IndexOf("EntityStateMessage", StringComparison.Ordinal) >= 0)
                        {
                            _entityStateId = cur.Key;
                            _idResolved = true;
                            _log.LogInfo($"Net: resolved EntityStateMessage id = 0x{_entityStateId:X4} ({_entityStateId}).");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) { if (!_idWarned) { _idWarned = true; _log.LogError($"Net: ResolveEntityStateId failed: {ex.Message}"); } }
            return false;
        }

        // ---------------------------------------------------------------- subscribe

        public void Subscribe(string channel, Action<AslNetMessage> handler)
        {
            if (string.IsNullOrEmpty(channel) || handler == null) { _log.LogError("Net.Subscribe: null/empty argument."); return; }
            if (!EnsureInstalled())
                _log.LogWarning($"Net.Subscribe('{channel}'): transport unavailable; you will receive nothing.");
            lock (_gate)
            {
                if (!_subs.TryGetValue(channel, out var list)) { list = new List<Action<AslNetMessage>>(); _subs[channel] = list; }
                list.Add(handler);
            }
        }

        public void Unsubscribe(string channel, Action<AslNetMessage> handler)
        {
            if (string.IsNullOrEmpty(channel) || handler == null) return;
            lock (_gate)
            {
                if (_subs.TryGetValue(channel, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0) _subs.Remove(channel);
                }
            }
        }

        // ---------------------------------------------------------------- wire format

        private static byte[] Encode(string channel, byte[] data)
        {
            byte[] ch = Encoding.UTF8.GetBytes(channel);
            if (ch.Length > 255) throw new ArgumentException("channel name too long (max 255 UTF-8 bytes)");

            byte[] buf = new byte[HeaderLen + ch.Length + data.Length];
            int o = 0;
            Buffer.BlockCopy(Magic, 0, buf, o, Magic.Length); o += Magic.Length;
            buf[o++] = WireVersion;
            buf[o++] = (byte)ch.Length;
            Buffer.BlockCopy(ch, 0, buf, o, ch.Length); o += ch.Length;
            Buffer.BlockCopy(data, 0, buf, o, data.Length);
            return buf;
        }

        /// <summary>Decode an ASL packet from the message payload, or null if it isn't one of ours.</summary>
        private AslNetMessage Decode(Il2CppSystem.ArraySegment<byte> seg, int senderConnId)
        {
            var arr = seg.Array;
            if (arr == null) { if (Diagnostics) _log.LogWarning("Net.DIAG decode: payload.Array is null"); return null; }
            int off = seg.Offset, cnt = seg.Count;
            if (Diagnostics) _log.LogInfo($"Net.DIAG decode: off={off} cnt={cnt} senderConn={senderConnId}");
            if (cnt < HeaderLen) return null;

            // Pull the native segment into a managed buffer once (messages are small).
            byte[] bytes = new byte[cnt];
            for (int i = 0; i < cnt; i++) bytes[i] = arr[off + i];

            for (int i = 0; i < Magic.Length; i++) if (bytes[i] != Magic[i]) { if (Diagnostics) _log.LogWarning($"Net.DIAG decode: magic mismatch at {i} (0x{bytes[i]:X2})"); return null; }
            int p = Magic.Length;
            if (bytes[p++] != WireVersion) { if (Diagnostics) _log.LogWarning("Net.DIAG decode: version mismatch"); return null; }
            int chLen = bytes[p++];
            if (p + chLen > cnt) { if (Diagnostics) _log.LogWarning("Net.DIAG decode: channel length overflow"); return null; }

            string channel = Encoding.UTF8.GetString(bytes, p, chLen); p += chLen;
            if (Diagnostics) _log.LogInfo($"Net.DIAG decode OK: channel='{channel}' dataLen={cnt - p}");
            int dataLen = cnt - p;
            byte[] data = new byte[dataLen];
            Buffer.BlockCopy(bytes, p, data, 0, dataLen);
            return new AslNetMessage(channel, data, senderConnId);
        }

        private void Dispatch(AslNetMessage msg)
        {
            if (msg == null) return;
            List<Action<AslNetMessage>> snapshot;
            lock (_gate)
            {
                if (!_subs.TryGetValue(msg.Channel, out var list) || list.Count == 0) return;
                snapshot = new List<Action<AslNetMessage>>(list);   // allow (un)subscribe from within a handler
            }
            for (int i = 0; i < snapshot.Count; i++)
            {
                try { snapshot[i](msg); }
                catch (Exception ex) { _log.LogError($"Net handler for channel '{msg.Channel}' threw: {ex.Message}"); }
            }
        }

        // ---------------------------------------------------------------- install + intercept

        private bool EnsureInstalled()
        {
            if (_installAttempted) return _installed;
            _installAttempted = true;
            _active = this;
            try
            {
                var clientTarget = AccessTools.Method(typeof(NetworkClient), "OnEntityStateMessage");
                var serverTarget = AccessTools.Method(typeof(NetworkServer), "OnEntityStateMessage");
                if (clientTarget == null || serverTarget == null)
                {
                    _log.LogWarning("Net transport: EntityStateMessage handler(s) not found; messaging disabled.");
                    return false;
                }

                var flags = BindingFlags.Static | BindingFlags.NonPublic;
                _harmony.Patch(clientTarget, prefix: new HarmonyMethod(
                    typeof(NetTransport).GetMethod(nameof(ClientReceivePrefix), flags)));
                _harmony.Patch(serverTarget, prefix: new HarmonyMethod(
                    typeof(NetTransport).GetMethod(nameof(ServerReceivePrefix), flags)));

                if (Diagnostics)
                {
                    try
                    {
                        var sendTarget = AccessTools.Method(typeof(NetworkConnection), "Send",
                            new Type[] { typeof(Il2CppSystem.ArraySegment<byte>), typeof(int) });
                        if (sendTarget != null)
                            _harmony.Patch(sendTarget, postfix: new HarmonyMethod(
                                typeof(NetTransport).GetMethod(nameof(OutgoingDiagPostfix), flags)));

                        var cTransport = AccessTools.Method(typeof(NetworkClient), "OnTransportData");
                        var sTransport = AccessTools.Method(typeof(NetworkServer), "OnTransportData");
                        if (cTransport != null)
                            _harmony.Patch(cTransport, postfix: new HarmonyMethod(
                                typeof(NetTransport).GetMethod(nameof(ClientTransportDiag), flags)));
                        if (sTransport != null)
                            _harmony.Patch(sTransport, postfix: new HarmonyMethod(
                                typeof(NetTransport).GetMethod(nameof(ServerTransportDiag), flags)));
                        _log.LogInfo($"Net.DIAG sniffers installed: send={(sendTarget != null)} clientTransport={(cTransport != null)} serverTransport={(sTransport != null)}");
                    }
                    catch (Exception ex) { _log.LogWarning($"Net.DIAG sniffers failed: {ex.Message}"); }
                }

                _installed = true;
                RunWireSelfCheck();   // confirm our batch parses with Mirror's own reader (no peer needed)
                _log.LogInfo("Net transport installed (EntityStateMessage envelope) — mods can Send/Subscribe.");
            }
            catch (Exception ex)
            {
                _log.LogError($"Net transport failed to install (messaging disabled, netcode untouched): {ex.Message}");
                _installed = false;
            }
            return _installed;
        }

        // Client receives an EntityStateMessage from the server. Param name 'message' must match the
        // game method (HarmonyX binds injected args by name on IL2CPP). Return false = skip original.
        private static bool ClientReceivePrefix(EntityStateMessage message)
        {
            try
            {
                if (Diagnostics && message != null)
                {
                    long n = ++_clientCalls;
                    if (n <= 30 || message.netId == SentinelNetId)
                        _active?._log.LogInfo($"Net.DIAG client recv #{n} netId=0x{message.netId:X8} payloadCount={message.payload.Count} sentinel={(message.netId == SentinelNetId)}");
                }
                if (message == null || message.netId != SentinelNetId) return true;   // not ours -> run game handler
                var t = _active;
                if (t != null) t.Dispatch(t.Decode(message.payload, -1));             // -1: came from the server
                return false;                                                          // consume sentinel-tagged traffic
            }
            catch (Exception ex) { _active?._log.LogError($"Net client receive failed: {ex.Message}"); return true; }
        }

        // Server receives an EntityStateMessage from a client. Param names 'connection'/'message'
        // must match the game method.
        private static bool ServerReceivePrefix(NetworkConnectionToClient connection, EntityStateMessage message)
        {
            try
            {
                if (Diagnostics && message != null)
                {
                    long n = ++_serverCalls;
                    if (n <= 30 || message.netId == SentinelNetId)
                        _active?._log.LogInfo($"Net.DIAG server recv #{n} netId=0x{message.netId:X8} payloadCount={message.payload.Count} sentinel={(message.netId == SentinelNetId)}");
                }
                if (message == null || message.netId != SentinelNetId) return true;
                var t = _active;
                if (t != null)
                {
                    int sender = 0;
                    try { if (connection != null) sender = connection.connectionId; } catch { /* keep 0 */ }
                    t.Dispatch(t.Decode(message.payload, sender));
                }
                return false;
            }
            catch (Exception ex) { _active?._log.LogError($"Net server receive failed: {ex.Message}"); return true; }
        }

        // Diagnostic: postfix on the low-level NetworkConnection.Send(segment, channelId) — the path all
        // typed sends funnel through after packing. Scans each outgoing packed message for our "ASL1"
        // magic: if it appears, our generic Send<EntityStateMessage> really did put bytes on the wire,
        // and the segment's first two bytes are the real EntityStateMessage message id (which we need
        // for a non-generic send path).
        private static void OutgoingDiagPostfix(Il2CppSystem.ArraySegment<byte> segment)
        {
            try
            {
                var t = _active;
                if (t == null) return;
                var arr = segment.Array;
                if (arr == null) return;
                int off = segment.Offset, cnt = segment.Count;
                if (cnt < 6 || cnt > 8192) return;

                for (int i = 0; i + 4 <= cnt; i++)
                {
                    if (arr[off + i] == Magic[0] && arr[off + i + 1] == Magic[1] && arr[off + i + 2] == Magic[2] && arr[off + i + 3] == Magic[3])
                    {
                        int id = arr[off] | (arr[off + 1] << 8);
                        t._log.LogInfo($"Net.DIAG OUTGOING contains ASL1 at offset {i}! segLen={cnt} msgId=0x{id:X4}");
                        return;
                    }
                }

                long n = ++_outDiag;
                if (n <= 15)
                {
                    int id = arr[off] | (arr[off + 1] << 8);
                    t._log.LogInfo($"Net.DIAG outgoing #{n} segLen={cnt} firstId=0x{id:X4}");
                }
            }
            catch { /* diagnostic only */ }
        }

        // Diagnostic: scan raw inbound transport data (a whole Mirror batch) for our "ASL1" magic.
        // OnTransportData is the transport's data callback (held as a delegate, so not inlined → a
        // reliable patch point). If our magic shows up here, our bytes really did cross the wire and
        // the problem is later (unpack/dispatch); if it never shows, the send never transmitted.
        private static void ClientTransportDiag(Il2CppSystem.ArraySegment<byte> data) => ScanIncoming("client", -1, data);
        private static void ServerTransportDiag(int connectionId, Il2CppSystem.ArraySegment<byte> data) => ScanIncoming("server", connectionId, data);

        private static long _inDiag;
        private static void ScanIncoming(string where, int conn, Il2CppSystem.ArraySegment<byte> data)
        {
            try
            {
                var t = _active;
                if (t == null) return;
                var arr = data.Array;
                if (arr == null) return;
                int off = data.Offset, cnt = data.Count;
                if (cnt < 4 || cnt > 65536) return;
                for (int i = 0; i + 4 <= cnt; i++)
                {
                    if (arr[off + i] == Magic[0] && arr[off + i + 1] == Magic[1] && arr[off + i + 2] == Magic[2] && arr[off + i + 3] == Magic[3])
                    {
                        t._log.LogInfo($"Net.DIAG INCOMING-TRANSPORT {where}(conn={conn}) contains ASL1 at {i}! batchLen={cnt}");
                        return;
                    }
                }
                long n = ++_inDiag;
                if (n <= 4)
                {
                    var sb = new StringBuilder();
                    int show = cnt < 40 ? cnt : 40;
                    for (int i = 0; i < show; i++) sb.Append(arr[off + i].ToString("X2"));
                    t._log.LogInfo($"Net.DIAG incoming-transport {where}(conn={conn}) #{n} batchLen={cnt} hex={sb}");
                }
            }
            catch { /* diagnostic only */ }
        }

        private static bool Safe(Func<bool> f)
        {
            try { return f(); }
            catch { return false; }
        }
    }
}
