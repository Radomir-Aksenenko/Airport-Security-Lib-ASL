using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ASL
{
    /// <summary>
    /// Minimal pure-managed PNG decoder: PNG bytes -> top-down RGBA32. Supports 8-bit depth,
    /// non-interlaced, color types 0 (gray), 2 (RGB), 3 (palette), 4 (gray+alpha), 6 (RGBA).
    /// Uses <see cref="DeflateStream"/> for the zlib step — no Unity, no System.Drawing.
    ///
    /// Exists because Unity 6 / IL2CPP's <c>ImageConversion.LoadImage</c> is span-based and can't be
    /// marshalled from managed code; we decode here and write via <c>Texture2D.LoadRawTextureData</c>.
    /// </summary>
    internal static class PngDecoder
    {
        /// <summary>Decodes a PNG into top-down RGBA32 bytes (length = width*height*4).</summary>
        public static byte[] Decode(byte[] png, out int width, out int height)
        {
            width = height = 0;
            if (png == null || png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
                throw new Exception("not a PNG file");

            int pos = 8;
            int w = 0, h = 0, bitDepth = 0, colorType = 0, interlace = 0;
            byte[] palette = null;
            using var idat = new MemoryStream();

            while (pos + 8 <= png.Length)
            {
                int len = ReadBE32(png, pos); pos += 4;
                string type = Encoding.ASCII.GetString(png, pos, 4); pos += 4;
                if (len < 0 || pos + len + 4 > png.Length) break;

                if (type == "IHDR")
                {
                    w = ReadBE32(png, pos);
                    h = ReadBE32(png, pos + 4);
                    bitDepth = png[pos + 8];
                    colorType = png[pos + 9];
                    interlace = png[pos + 12];
                }
                else if (type == "PLTE")
                {
                    palette = new byte[len];
                    Array.Copy(png, pos, palette, 0, len);
                }
                else if (type == "IDAT")
                {
                    idat.Write(png, pos, len);
                }
                else if (type == "IEND")
                {
                    break;
                }

                pos += len + 4; // chunk data + 4-byte CRC
            }

            if (w <= 0 || h <= 0) throw new Exception("invalid PNG dimensions");
            if (bitDepth != 8) throw new Exception($"unsupported PNG bit depth {bitDepth} (only 8 supported)");
            if (interlace != 0) throw new Exception("interlaced PNG not supported");

            int channels = colorType switch
            {
                0 => 1, // grayscale
                2 => 3, // RGB
                3 => 1, // palette index
                4 => 2, // grayscale + alpha
                6 => 4, // RGBA
                _ => throw new Exception($"unsupported PNG color type {colorType}")
            };

            // zlib stream = 2-byte header + raw deflate + 4-byte Adler32. DeflateStream wants raw deflate.
            var zlib = idat.ToArray();
            if (zlib.Length < 3) throw new Exception("no image data");
            byte[] raw;
            using (var ms = new MemoryStream(zlib, 2, zlib.Length - 2))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                ds.CopyTo(outMs);
                raw = outMs.ToArray();
            }

            int stride = w * channels;  // scanline bytes (excluding the per-line filter byte)
            int bpp = channels;         // bytes per pixel for the filter predictors (8-bit)
            var img = new byte[h * stride];

            int rp = 0;
            for (int y = 0; y < h; y++)
            {
                if (rp >= raw.Length) throw new Exception("truncated PNG data");
                int filter = raw[rp++];
                int line = y * stride;
                for (int x = 0; x < stride; x++)
                {
                    int rawVal = raw[rp++];
                    int a = x >= bpp ? img[line + x - bpp] : 0;                       // left
                    int b = y > 0 ? img[line - stride + x] : 0;                       // up
                    int c = (y > 0 && x >= bpp) ? img[line - stride + x - bpp] : 0;   // up-left
                    int val = filter switch
                    {
                        0 => rawVal,
                        1 => rawVal + a,
                        2 => rawVal + b,
                        3 => rawVal + ((a + b) >> 1),
                        4 => rawVal + Paeth(a, b, c),
                        _ => throw new Exception($"unknown PNG filter {filter}")
                    };
                    img[line + x] = (byte)(val & 0xFF);
                }
            }

            var rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int si = y * stride + x * channels;
                    int di = (y * w + x) * 4;
                    byte r, g, bl, al;
                    switch (colorType)
                    {
                        case 0: r = g = bl = img[si]; al = 255; break;
                        case 2: r = img[si]; g = img[si + 1]; bl = img[si + 2]; al = 255; break;
                        case 3:
                            int idx = img[si] * 3;
                            if (palette != null && idx + 2 < palette.Length) { r = palette[idx]; g = palette[idx + 1]; bl = palette[idx + 2]; }
                            else { r = g = bl = 0; }
                            al = 255; break;
                        case 4: r = g = bl = img[si]; al = img[si + 1]; break;
                        default: r = img[si]; g = img[si + 1]; bl = img[si + 2]; al = img[si + 3]; break;
                    }
                    rgba[di] = r; rgba[di + 1] = g; rgba[di + 2] = bl; rgba[di + 3] = al;
                }
            }

            width = w; height = h;
            return rgba;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        private static int ReadBE32(byte[] d, int o) =>
            (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
    }
}
