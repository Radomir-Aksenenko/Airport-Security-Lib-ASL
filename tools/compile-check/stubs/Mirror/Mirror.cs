// STUB: Mirror (the networking library the game uses) as exposed by Il2CppInterop. Members are the subset
// ASL touches; signatures marked GUESS in ../../README.md are inferred from usage, not from the real DLL.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Mirror
{
    /// <summary>Non-blittable struct (holds an ArraySegment) -> a class in the interop output.</summary>
    public class EntityStateMessage : Il2CppSystem.ValueType
    {
        public EntityStateMessage(IntPtr pointer) : base(pointer) { }
        public uint netId { get; set; }
        public Il2CppSystem.ArraySegment<byte> payload { get; set; }
    }

    public class NetworkConnection : Il2CppSystem.Object
    {
        public NetworkConnection(IntPtr pointer) : base(pointer) { }
        public int connectionId { get; }
        public bool isAuthenticated { get; set; }
        public bool isReady { get; set; }
        public string address { get; }
        public void Send(Il2CppSystem.ArraySegment<byte> segment, int channelId) => throw null;
        public void Disconnect() => throw null;
    }
    public class NetworkConnectionToClient : NetworkConnection { public NetworkConnectionToClient(IntPtr pointer) : base(pointer) { } }
    public class NetworkConnectionToServer : NetworkConnection { public NetworkConnectionToServer(IntPtr pointer) : base(pointer) { } }

    public class NetworkIdentity : MonoBehaviour
    {
        public NetworkIdentity(IntPtr pointer) : base(pointer) { }
        public uint netId { get; }
        public bool isLocalPlayer { get; }
        public bool isServer { get; }
        public bool isClient { get; }
        public bool isOwned { get; }
        public NetworkConnectionToClient connectionToClient { get; }
        public NetworkConnectionToServer connectionToServer { get; }
    }

    public class NetworkBehaviour : MonoBehaviour
    {
        public NetworkBehaviour(IntPtr pointer) : base(pointer) { }
        public NetworkIdentity netIdentity { get; }
        public uint netId { get; }
        public bool isLocalPlayer { get; }
        public bool isServer { get; }
        public bool isClient { get; }
        public bool isOwned { get; }
        public bool isServerOnly { get; }
        public bool isClientOnly { get; }
        public NetworkConnectionToClient connectionToClient { get; }
        public NetworkConnectionToServer connectionToServer { get; }
    }

    public abstract class Transport : MonoBehaviour
    {
        protected Transport(IntPtr pointer) : base(pointer) { }
        public static Transport active { get; set; }
        public virtual void ClientSend(Il2CppSystem.ArraySegment<byte> segment, int channelId) => throw null;
        public virtual void ServerSend(int connectionId, Il2CppSystem.ArraySegment<byte> segment, int channelId) => throw null;
    }

    public static class NetworkServer
    {
        public static bool active { get; }
        public static bool isLoadingScene { get; set; }
        public static int maxConnections { get; set; }
        public static Il2CppSystem.Collections.Generic.Dictionary<int, NetworkConnectionToClient> connections { get; }
        public static Il2CppSystem.Collections.Generic.Dictionary<uint, NetworkIdentity> spawned { get; }
    }

    public static class NetworkClient
    {
        public static bool active { get; }
        public static bool isConnected { get; }
        public static bool isConnecting { get; }
        public static bool ready { get; }
        public static NetworkConnectionToServer connection { get; }
        public static Il2CppSystem.Collections.Generic.Dictionary<uint, NetworkIdentity> spawned { get; }
    }

    public static class NetworkTime
    {
        public static double localTime { get; }
        public static double time { get; }
        public static double rtt { get; }
    }

    public static class NetworkMessages
    {
        public static Il2CppSystem.Collections.Generic.Dictionary<ushort, Il2CppSystem.Type> Lookup { get; }
        public static ushort GetId(Il2CppSystem.Type type) => throw null;
    }

    public static class Compression
    {
        public static int VarUIntSize(ulong value) => throw null;
        public static void CompressVarUInt(NetworkWriter writer, ulong value) => throw null;
        public static ulong DecompressVarUInt(NetworkReader reader) => throw null;
        public static void CompressVarInt(NetworkWriter writer, long i) => throw null;
        public static long DecompressVarInt(NetworkReader reader) => throw null;
    }

    public class NetworkWriter : Il2CppSystem.Object
    {
        public NetworkWriter(IntPtr pointer) : base(pointer) { }
        public NetworkWriter() : base(IntPtr.Zero) { }
        public int Position { get; set; }
        public void Reset() => throw null;
        public Il2CppSystem.ArraySegment<byte> ToArraySegment() => throw null;
        public Il2CppStructArray<byte> ToArray() => throw null;
    }

    public class NetworkReader : Il2CppSystem.Object
    {
        public NetworkReader(IntPtr pointer) : base(pointer) { }
        public NetworkReader(Il2CppSystem.ArraySegment<byte> segment) : base(IntPtr.Zero) { }
        public NetworkReader(Il2CppStructArray<byte> bytes) : base(IntPtr.Zero) { }
        public int Position { get; set; }
        public int Length { get; }
        public int Remaining { get; }
    }

    public static class NetworkWriterExtensions
    {
        public static void WriteByte(this NetworkWriter writer, byte value) => throw null;
        public static void WriteBool(this NetworkWriter writer, bool value) => throw null;
        public static void WriteUShort(this NetworkWriter writer, ushort value) => throw null;
        public static void WriteInt(this NetworkWriter writer, int value) => throw null;
        public static void WriteUInt(this NetworkWriter writer, uint value) => throw null;
        public static void WriteFloat(this NetworkWriter writer, float value) => throw null;
        public static void WriteDouble(this NetworkWriter writer, double value) => throw null;
        public static void WriteString(this NetworkWriter writer, string value) => throw null;
        public static void WriteBytes(this NetworkWriter writer, Il2CppStructArray<byte> buffer, int offset, int count) => throw null;
        public static void WriteBytesAndSize(this NetworkWriter writer, Il2CppStructArray<byte> buffer, int offset, int count) => throw null;
        public static void WriteBytesAndSize(this NetworkWriter writer, Il2CppStructArray<byte> buffer) => throw null;
        public static void WriteVector3(this NetworkWriter writer, Vector3 value) => throw null;
    }

    public static class NetworkReaderExtensions
    {
        public static byte ReadByte(this NetworkReader reader) => throw null;
        public static bool ReadBool(this NetworkReader reader) => throw null;
        public static ushort ReadUShort(this NetworkReader reader) => throw null;
        public static int ReadInt(this NetworkReader reader) => throw null;
        public static uint ReadUInt(this NetworkReader reader) => throw null;
        public static float ReadFloat(this NetworkReader reader) => throw null;
        public static double ReadDouble(this NetworkReader reader) => throw null;
        public static string ReadString(this NetworkReader reader) => throw null;
        public static Il2CppStructArray<byte> ReadBytesAndSize(this NetworkReader reader) => throw null;
        public static Vector3 ReadVector3(this NetworkReader reader) => throw null;
    }
}
