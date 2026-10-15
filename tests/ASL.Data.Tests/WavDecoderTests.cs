using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ASL.Core;
using Xunit;

namespace ASL.Data.Tests
{
    public class WavDecoderTests
    {
        // ---- helpers: build WAV files by hand ----

        private static byte[] Chunk(string id, byte[] body)
        {
            var l = new List<byte>(Encoding.ASCII.GetBytes(id));
            l.AddRange(BitConverter.GetBytes((uint)body.Length));
            l.AddRange(body);
            if ((body.Length & 1) == 1) l.Add(0);
            return l.ToArray();
        }

        private static byte[] Fmt(int tag, int ch, int rate, int bits, bool extensible = false, int subTag = 0)
        {
            int block = ch * bits / 8;
            var l = new List<byte>();
            l.AddRange(BitConverter.GetBytes((ushort)(extensible ? 0xFFFE : tag)));
            l.AddRange(BitConverter.GetBytes((ushort)ch));
            l.AddRange(BitConverter.GetBytes(rate));
            l.AddRange(BitConverter.GetBytes(rate * block));
            l.AddRange(BitConverter.GetBytes((ushort)block));
            l.AddRange(BitConverter.GetBytes((ushort)bits));
            if (extensible)
            {
                l.AddRange(BitConverter.GetBytes((ushort)22));
                l.AddRange(BitConverter.GetBytes((ushort)bits));
                l.AddRange(BitConverter.GetBytes(3));   // channel mask
                l.AddRange(BitConverter.GetBytes((ushort)subTag));
                l.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71 });
            }
            return Chunk("fmt ", l.ToArray());
        }

        private static byte[] Riff(params byte[][] chunks)
        {
            var body = new List<byte>(Encoding.ASCII.GetBytes("WAVE"));
            foreach (var c in chunks) body.AddRange(c);
            var l = new List<byte>(Encoding.ASCII.GetBytes("RIFF"));
            l.AddRange(BitConverter.GetBytes((uint)body.Count));
            l.AddRange(body);
            return l.ToArray();
        }

        private static byte[] Bytes(Action<BinaryWriter> w)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            w(bw);
            bw.Flush();
            return ms.ToArray();
        }

        // ---- formats ----

        [Fact]
        public void Pcm16_Mono()
        {
            var data = Bytes(w => { w.Write((short)0); w.Write((short)16384); w.Write((short)-32768); w.Write((short)32767); });
            var wav = WavDecoder.Decode(Riff(Fmt(1, 1, 44100, 16), Chunk("data", data)));
            Assert.Equal(1, wav.Channels);
            Assert.Equal(44100, wav.SampleRate);
            Assert.Equal(4, wav.FrameCount);
            Assert.Equal(0f, wav.Samples[0]);
            Assert.Equal(0.5f, wav.Samples[1]);
            Assert.Equal(-1f, wav.Samples[2]);
            Assert.Equal(32767f / 32768f, wav.Samples[3], 6);
        }

        [Fact]
        public void Pcm16_Stereo_IsInterleaved()
        {
            var data = Bytes(w => { w.Write((short)16384); w.Write((short)-16384); w.Write((short)8192); w.Write((short)-8192); });
            var wav = WavDecoder.Decode(Riff(Fmt(1, 2, 48000, 16), Chunk("data", data)));
            Assert.Equal(2, wav.Channels);
            Assert.Equal(2, wav.FrameCount);
            Assert.Equal(new[] { 0.5f, -0.5f, 0.25f, -0.25f }, wav.Samples);
        }

        [Fact]
        public void Pcm8_IsUnsigned()
        {
            var wav = WavDecoder.Decode(Riff(Fmt(1, 1, 8000, 8), Chunk("data", new byte[] { 128, 255, 0, 192 })));
            Assert.Equal(0f, wav.Samples[0]);
            Assert.Equal(127f / 128f, wav.Samples[1], 6);
            Assert.Equal(-1f, wav.Samples[2]);
            Assert.Equal(0.5f, wav.Samples[3]);
        }

        [Fact]
        public void Pcm24_SignExtends()
        {
            // 0x400000 = +0.5, 0xC00000 = -0.5, 0x800000 = -1
            var data = new byte[] { 0x00, 0x00, 0x40, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x80 };
            var wav = WavDecoder.Decode(Riff(Fmt(1, 1, 44100, 24), Chunk("data", data)));
            Assert.Equal(new[] { 0.5f, -0.5f, -1f }, wav.Samples);
        }

        [Fact]
        public void Pcm32()
        {
            var data = Bytes(w => { w.Write(int.MinValue); w.Write(1 << 30); w.Write(0); });
            var wav = WavDecoder.Decode(Riff(Fmt(1, 1, 44100, 32), Chunk("data", data)));
            Assert.Equal(new[] { -1f, 0.5f, 0f }, wav.Samples);
        }

        [Fact]
        public void Float32_PassesThrough_AndClamps()
        {
            var data = Bytes(w => { w.Write(0.25f); w.Write(-0.75f); w.Write(3f); w.Write(float.NaN); });
            var wav = WavDecoder.Decode(Riff(Fmt(3, 1, 44100, 32), Chunk("data", data)));
            Assert.Equal(new[] { 0.25f, -0.75f, 1f, 0f }, wav.Samples);
        }

        [Fact]
        public void Float64()
        {
            var data = Bytes(w => { w.Write(0.5d); w.Write(-0.5d); });
            var wav = WavDecoder.Decode(Riff(Fmt(3, 1, 44100, 64), Chunk("data", data)));
            Assert.Equal(new[] { 0.5f, -0.5f }, wav.Samples);
        }

        [Fact]
        public void Extensible_Pcm16()
        {
            var data = Bytes(w => { w.Write((short)16384); w.Write((short)-16384); });
            var wav = WavDecoder.Decode(Riff(Fmt(1, 2, 44100, 16, extensible: true, subTag: 1), Chunk("data", data)));
            Assert.Equal(2, wav.Channels);
            Assert.Equal(new[] { 0.5f, -0.5f }, wav.Samples);
        }

        [Fact]
        public void Extensible_Float32()
        {
            var data = Bytes(w => { w.Write(0.5f); w.Write(-0.5f); });
            var wav = WavDecoder.Decode(Riff(Fmt(3, 1, 44100, 32, extensible: true, subTag: 3), Chunk("data", data)));
            Assert.Equal(new[] { 0.5f, -0.5f }, wav.Samples);
        }

        [Fact]
        public void Multichannel_6()
        {
            var data = Bytes(w => { for (int i = 0; i < 12; i++) w.Write((short)(i * 1000)); });
            var wav = WavDecoder.Decode(Riff(Fmt(1, 6, 44100, 16), Chunk("data", data)));
            Assert.Equal(6, wav.Channels);
            Assert.Equal(2, wav.FrameCount);
            Assert.Equal(11000f / 32768f, wav.Samples[11], 6);
        }

        // ---- chunk handling ----

        [Fact]
        public void SkipsUnknownChunks_IncludingOddSizedPadding()
        {
            var list = Chunk("LIST", Encoding.ASCII.GetBytes("INFOISFT\x05\0\0\0abcd\0"));   // odd length -> padded
            var odd = Chunk("junk", new byte[] { 1, 2, 3 });                                   // odd length -> padded
            var data = Bytes(w => { w.Write((short)8192); });
            var wav = WavDecoder.Decode(Riff(list, Fmt(1, 1, 22050, 16), odd, Chunk("data", data), Chunk("LIST", new byte[8])));
            Assert.Equal(22050, wav.SampleRate);
            Assert.Equal(new[] { 0.25f }, wav.Samples);
        }

        [Fact]
        public void DataSizeLargerThanFile_IsTruncatedNotFatal()
        {
            var good = Riff(Fmt(1, 1, 44100, 16), Chunk("data", Bytes(w => { w.Write((short)16384); w.Write((short)16384); })));
            // overwrite the data chunk size with 0xFFFFFFFF (streamed writers do this)
            int dataPos = good.Length - 4 - 4 - 4;
            BitConverter.GetBytes(0xFFFFFFFF).CopyTo(good, dataPos + 4);
            var wav = WavDecoder.Decode(good);
            Assert.Equal(2, wav.FrameCount);
        }

        [Fact]
        public void PartialTrailingFrame_IsDropped()
        {
            var wav = WavDecoder.Decode(Riff(Fmt(1, 2, 44100, 16), Chunk("data", new byte[6])));   // 1.5 frames
            Assert.Equal(1, wav.FrameCount);
        }

        [Fact]
        public void EmptyData_GivesZeroFrames()
        {
            var wav = WavDecoder.Decode(Riff(Fmt(1, 1, 44100, 16), Chunk("data", new byte[0])));
            Assert.Equal(0, wav.FrameCount);
        }

        // ---- bad files ----

        [Fact] public void Null_Throws() => Assert.Throws<ArgumentNullException>(() => WavDecoder.Decode(null));
        [Fact] public void Empty_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(new byte[0]));
        [Fact] public void NotRiff_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Encoding.ASCII.GetBytes("OggS....WAVEfmt ....")));

        [Fact]
        public void NotWave_Throws()
        {
            var b = Riff(Fmt(1, 1, 44100, 16), Chunk("data", new byte[2]));
            b[8] = (byte)'A';
            Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(b));
        }

        [Fact] public void NoFmt_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(Chunk("data", new byte[4]))));
        [Fact] public void NoData_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(Fmt(1, 1, 44100, 16))));
        [Fact] public void DataBeforeFmt_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(Chunk("data", new byte[4]), Fmt(1, 1, 44100, 16))));

        [Fact]
        public void TruncatedHeader_Throws()
        {
            var b = Riff(Fmt(1, 1, 44100, 16), Chunk("data", new byte[4]));
            Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(new ArraySegment<byte>(b, 0, 20).ToArray()));
        }

        [Fact] public void Adpcm_NotSupported() => Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(Riff(Fmt(2, 1, 44100, 4), Chunk("data", new byte[8]))));
        [Fact] public void MuLaw_NotSupported() => Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(Riff(Fmt(7, 1, 8000, 8), Chunk("data", new byte[8]))));
        [Fact] public void Pcm12_NotSupported() => Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(Riff(Fmt(1, 1, 8000, 12), Chunk("data", new byte[8]))));
        [Fact] public void ZeroChannels_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(Fmt(1, 0, 8000, 16), Chunk("data", new byte[8]))));
        [Fact] public void ZeroRate_Throws() => Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(Fmt(1, 1, 0, 16), Chunk("data", new byte[8]))));

        [Fact]
        public void DataChunkSizeZero_UsesRemainingBytes()
        {
            var data = new byte[] { 0x00, 0x40, 0x00, 0xC0 };   // two 16-bit mono samples
            var b = Riff(Fmt(1, 1, 44100, 16), Chunk("data", data));
            int dataHdr = b.Length - data.Length - 4;           // the size field of the data chunk
            b[dataHdr] = b[dataHdr + 1] = b[dataHdr + 2] = b[dataHdr + 3] = 0;
            var w = WavDecoder.Decode(b);
            Assert.Equal(2, w.FrameCount);
            Assert.Equal(0.5f, w.Samples[0], 3);
            Assert.Equal(-0.5f, w.Samples[1], 3);
        }

        [Fact]
        public void DataChunkSizeMax_UsesRemainingBytes()
        {
            var data = new byte[] { 0x00, 0x40, 0x00, 0xC0 };
            var b = Riff(Fmt(1, 1, 44100, 16), Chunk("data", data));
            int dataHdr = b.Length - data.Length - 4;
            b[dataHdr] = b[dataHdr + 1] = b[dataHdr + 2] = b[dataHdr + 3] = 0xFF;
            Assert.Equal(2, WavDecoder.Decode(b).FrameCount);
        }

        [Fact]
        public void BadBlockAlign_Throws()
        {
            var fmt = Fmt(1, 2, 44100, 16);
            fmt[8 + 12] = 3;   // block align 3 instead of 4
            Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(fmt, Chunk("data", new byte[8]))));
        }

        [Fact]
        public void Garbage_NeverThrowsUnexpectedTypes()
        {
            var rnd = new Random(1234);
            for (int n = 0; n < 500; n++)
            {
                var good = Riff(Fmt(1, 2, 44100, 16), Chunk("LIST", new byte[6]), Chunk("data", new byte[32]));
                var b = (byte[])good.Clone();
                for (int k = 0; k < 4; k++) b[rnd.Next(b.Length)] = (byte)rnd.Next(256);
                if (rnd.Next(3) == 0) b = new ArraySegment<byte>(b, 0, rnd.Next(b.Length)).ToArray();
                try { WavDecoder.Decode(b); }
                catch (InvalidDataException) { }
                catch (NotSupportedException) { }
            }
        }
    }
}
