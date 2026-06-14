using System;
using System.IO;
using System.Text;

namespace ASL.Api
{
    /// <summary>
    /// A tiny, allocation-light binary writer for serializing a typed network message into the bytes
    /// ASL's transport carries (see <see cref="IAslMessage"/> and <c>IAslNet.Send&lt;T&gt;</c>). All
    /// values are written little-endian; strings are length-prefixed UTF-8. Pair every <c>Write*</c>
    /// here with the matching <c>Read*</c> on <see cref="AslReader"/>, in the same order.
    /// </summary>
    public sealed class AslWriter
    {
        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;

        /// <summary>Creates an empty writer.</summary>
        public AslWriter()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream, Encoding.UTF8);
        }

        public void WriteBool(bool value) => _writer.Write(value);
        public void WriteByte(byte value) => _writer.Write(value);
        public void WriteSByte(sbyte value) => _writer.Write(value);
        public void WriteShort(short value) => _writer.Write(value);
        public void WriteUShort(ushort value) => _writer.Write(value);
        public void WriteInt(int value) => _writer.Write(value);
        public void WriteUInt(uint value) => _writer.Write(value);
        public void WriteLong(long value) => _writer.Write(value);
        public void WriteULong(ulong value) => _writer.Write(value);
        public void WriteFloat(float value) => _writer.Write(value);
        public void WriteDouble(double value) => _writer.Write(value);

        /// <summary>Writes a string (length-prefixed UTF-8). Null is written as empty.</summary>
        public void WriteString(string value) => _writer.Write(value ?? string.Empty);

        /// <summary>Writes a length-prefixed raw byte blob (null is written as length 0).</summary>
        public void WriteBytes(byte[] value)
        {
            if (value == null) { _writer.Write(0); return; }
            _writer.Write(value.Length);
            if (value.Length > 0) _writer.Write(value);
        }

        /// <summary>The serialized bytes so far. Pass this to <c>IAslNet.Send</c> (or it does it for you).</summary>
        public byte[] ToArray()
        {
            _writer.Flush();
            return _stream.ToArray();
        }
    }
}
