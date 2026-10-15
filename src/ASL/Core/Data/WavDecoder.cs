using System;
using System.IO;

namespace ASL.Core
{
    /// <summary>Decoded audio: interleaved samples in [-1, 1].</summary>
    public sealed class WavData
    {
        /// <summary>Interleaved samples (frame0ch0, frame0ch1, frame1ch0, ...).</summary>
        public float[] Samples;
        /// <summary>Channel count (1 = mono, 2 = stereo, ...).</summary>
        public int Channels;
        /// <summary>Sample rate in Hz.</summary>
        public int SampleRate;
        /// <summary>Samples per channel.</summary>
        public int FrameCount => Channels > 0 ? Samples.Length / Channels : 0;
    }

    /// <summary>
    /// Managed RIFF/WAVE decoder (pure C#, no Unity types). Supports PCM 8 (unsigned) / 16 / 24 / 32-bit
    /// integer, IEEE float 32/64, and WAVE_FORMAT_EXTENSIBLE wrapping either; skips unknown chunks
    /// (LIST, fact, cue, ...). Unsupported or malformed files throw a descriptive exception.
    /// </summary>
    public static class WavDecoder
    {
        private const int FormatPcm = 1, FormatFloat = 3, FormatExtensible = 0xFFFE;
        private const int MaxChannels = 32;

        /// <summary>Decode a whole .wav file.</summary>
        /// <exception cref="InvalidDataException">Not a WAV file, truncated, or inconsistent.</exception>
        /// <exception cref="NotSupportedException">A valid WAV in a format we do not decode (ADPCM, mp3-in-wav, 12-bit...).</exception>
        public static WavData Decode(byte[] b)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (b.Length < 12 || !Tag(b, 0, "RIFF") || !Tag(b, 8, "WAVE"))
                throw new InvalidDataException("not a RIFF/WAVE file");

            int format = 0, channels = 0, rate = 0, bits = 0, blockAlign = 0;
            bool haveFmt = false;
            long pos = 12;
            while (pos + 8 <= b.Length)
            {
                uint size = U32(b, (int)pos + 4);
                long body = pos + 8;
                long avail = b.Length - body;
                // A data chunk may legally claim more than the file holds (streamed/truncated writers: size 0 or 0xFFFFFFFF).
                long len = size > avail ? avail : size;

                if (Tag(b, (int)pos, "fmt "))
                {
                    if (len < 16) throw new InvalidDataException("fmt chunk too short");
                    int o = (int)body;
                    format = U16(b, o);
                    channels = U16(b, o + 2);
                    rate = (int)U32(b, o + 4);
                    blockAlign = U16(b, o + 12);
                    bits = U16(b, o + 14);
                    if (format == FormatExtensible)
                    {
                        // cbSize(2) validBits(2) channelMask(4) subFormat GUID (first 2 bytes = the real format tag)
                        if (len < 26) throw new InvalidDataException("WAVE_FORMAT_EXTENSIBLE fmt chunk too short");
                        format = U16(b, o + 24);
                    }
                    haveFmt = true;
                }
                else if (Tag(b, (int)pos, "data"))
                {
                    if (!haveFmt) throw new InvalidDataException("data chunk before fmt chunk");
                    return DecodeData(b, (int)body, (int)len, format, channels, rate, bits, blockAlign);
                }

                pos = body + len + (len & 1);   // chunks are word-aligned
            }
            throw new InvalidDataException(haveFmt ? "no data chunk" : "no fmt chunk");
        }

        private static WavData DecodeData(byte[] b, int off, int len, int format, int channels, int rate, int bits, int blockAlign)
        {
            if (channels < 1 || channels > MaxChannels) throw new InvalidDataException($"invalid channel count {channels}");
            if (rate < 1) throw new InvalidDataException($"invalid sample rate {rate}");

            if (format == FormatPcm)
            {
                if (bits != 8 && bits != 16 && bits != 24 && bits != 32)
                    throw new NotSupportedException($"PCM {bits}-bit is not supported (use 8, 16, 24 or 32-bit)");
            }
            else if (format == FormatFloat)
            {
                if (bits != 32 && bits != 64) throw new NotSupportedException($"float {bits}-bit is not supported (use 32-bit float)");
            }
            else
            {
                throw new NotSupportedException($"WAV format tag 0x{format:X} is not supported (only PCM and IEEE float; re-export as PCM 16-bit)");
            }

            int bytesPer = bits / 8;
            int frameBytes = bytesPer * channels;
            if (blockAlign != 0 && blockAlign != frameBytes) throw new InvalidDataException($"inconsistent block align {blockAlign} (expected {frameBytes})");

            int frames = len / frameBytes;   // drop a trailing partial frame
            long total = (long)frames * channels;
            if (total > int.MaxValue / 2) throw new NotSupportedException("audio too long");
            var s = new float[total];

            int p = off;
            switch (format == FormatPcm ? bits : -bits)
            {
                case 8:
                    for (int i = 0; i < s.Length; i++) s[i] = (b[p++] - 128) / 128f;
                    break;
                case 16:
                    for (int i = 0; i < s.Length; i++, p += 2) s[i] = (short)(b[p] | (b[p + 1] << 8)) / 32768f;
                    break;
                case 24:
                    for (int i = 0; i < s.Length; i++, p += 3)
                        s[i] = ((b[p] << 8 | b[p + 1] << 16 | b[p + 2] << 24) >> 8) / 8388608f;   // arithmetic shift sign-extends
                    break;
                case 32:
                    for (int i = 0; i < s.Length; i++, p += 4)
                        s[i] = (int)(b[p] | (uint)b[p + 1] << 8 | (uint)b[p + 2] << 16 | (uint)b[p + 3] << 24) / 2147483648f;
                    break;
                case -32:
                    for (int i = 0; i < s.Length; i++, p += 4) s[i] = Clamp(BitConverter.ToSingle(b, p));
                    break;
                case -64:
                    for (int i = 0; i < s.Length; i++, p += 8) s[i] = Clamp((float)BitConverter.ToDouble(b, p));
                    break;
            }
            return new WavData { Samples = s, Channels = channels, SampleRate = rate };
        }

        // NaN/Inf/out-of-range floats would make Unity's mixer misbehave; keep them sane.
        private static float Clamp(float v) => float.IsNaN(v) ? 0f : v > 1f ? 1f : v < -1f ? -1f : v;

        private static bool Tag(byte[] b, int o, string t) => o + 4 <= b.Length && b[o] == t[0] && b[o + 1] == t[1] && b[o + 2] == t[2] && b[o + 3] == t[3];
        private static int U16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
        private static uint U32(byte[] b, int o) => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }
}
