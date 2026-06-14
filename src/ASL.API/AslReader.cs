using System;
using System.IO;
using System.Text;

namespace ASL.Api
{
    /// <summary>
    /// Reads a typed network message back out of the bytes ASL delivered (see <see cref="IAslMessage"/>).
    /// Mirror of <see cref="AslWriter"/>: call the <c>Read*</c> methods in the exact same order the
    /// sender wrote them. Reading past the end throws (a malformed/short message), which ASL catches per
    /// handler so one bad packet can't take down your mod.
    /// </summary>
    public sealed class AslReader
    {
        private readonly BinaryReader _reader;

        /// <summary>Wraps the received bytes (never mutates them).</summary>
        public AslReader(byte[] data)
        {
            _reader = new BinaryReader(new MemoryStream(data ?? Array.Empty<byte>()), Encoding.UTF8);
        }

        public bool ReadBool() => _reader.ReadBoolean();
        public byte ReadByte() => _reader.ReadByte();
        public sbyte ReadSByte() => _reader.ReadSByte();
        public short ReadShort() => _reader.ReadInt16();
        public ushort ReadUShort() => _reader.ReadUInt16();
        public int ReadInt() => _reader.ReadInt32();
        public uint ReadUInt() => _reader.ReadUInt32();
        public long ReadLong() => _reader.ReadInt64();
        public ulong ReadULong() => _reader.ReadUInt64();
        public float ReadFloat() => _reader.ReadSingle();
        public double ReadDouble() => _reader.ReadDouble();

        /// <summary>Reads a length-prefixed UTF-8 string (as written by <see cref="AslWriter.WriteString"/>).</summary>
        public string ReadString() => _reader.ReadString();

        /// <summary>Reads a length-prefixed raw byte blob (as written by <see cref="AslWriter.WriteBytes"/>).</summary>
        public byte[] ReadBytes()
        {
            int n = _reader.ReadInt32();
            return n <= 0 ? Array.Empty<byte>() : _reader.ReadBytes(n);
        }
    }
}
