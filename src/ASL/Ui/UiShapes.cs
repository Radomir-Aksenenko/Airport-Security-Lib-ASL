using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Procedural, anti-aliased sprites generated in managed code (white RGBA32, tinted via Image.color):
    /// 9-sliceable rounded rectangles (all corners or top only), rings (outlines), circles, a chevron and a
    /// cross. Built with <c>Texture2D.LoadRawTextureData</c> + <c>Sprite.Create</c> (the verified marshal-safe
    /// path; no ImageConversion) and cached by shape parameters. Sprites are flagged
    /// <see cref="HideFlags.DontUnloadUnusedAsset"/> and regenerated if Unity ever destroyed one.
    /// </summary>
    internal static class UiShapes
    {
        private const int TL = 1, TR = 2, BL = 4, BR = 8, All = 15, Top = TL | TR;
        private static readonly Dictionary<long, Sprite> Cache = new Dictionary<long, Sprite>();

        /// <summary>Rounded rectangle, all corners; use with Image.Type.Sliced.</summary>
        public static Sprite Rounded(float radius) => RoundedMasked(radius, All, 0);

        /// <summary>Rounded rectangle with only the top corners rounded (title bars).</summary>
        public static Sprite RoundedTop(float radius) => RoundedMasked(radius, Top, 0);

        /// <summary>Outline (hollow rounded rectangle); use with Image.Type.Sliced.</summary>
        public static Sprite Ring(float radius, float thickness = 2f) =>
            RoundedMasked(radius, All, Mathf.Clamp(Mathf.RoundToInt(thickness), 1, 8));

        /// <summary>Filled circle (64 px); use with Image.Type.Simple + preserveAspect.</summary>
        public static Sprite Circle() => Get(100L << 40, () => BuildCircle(64));

        /// <summary>Down-pointing chevron glyph (32 px).</summary>
        public static Sprite Chevron() => Get(101L << 40, () => BuildGlyph(false));

        /// <summary>Close "x" glyph (32 px).</summary>
        public static Sprite Cross() => Get(102L << 40, () => BuildGlyph(true));

        private static Sprite RoundedMasked(float radius, int mask, int thick)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(radius), 1, 96);
            long key = ((long)r << 20) | ((long)thick << 8) | (long)mask;
            return Get(key, () => BuildRounded(r, mask, thick));
        }

        private static Sprite Get(long key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            Cache[key] = s;
            return s;
        }

        // ---- generation -----------------------------------------------------------------------

        // Signed distance to a box (half extents hx,hy) with per-quadrant corner radii (y up).
        private static float Sdf(float x, float y, float hx, float hy, float rtl, float rtr, float rbl, float rbr)
        {
            float r = x > 0f ? (y > 0f ? rtr : rbr) : (y > 0f ? rtl : rbl);
            float qx = MathF.Abs(x) - hx + r, qy = MathF.Abs(y) - hy + r;
            float ox = MathF.Max(qx, 0f), oy = MathF.Max(qy, 0f);
            return MathF.Min(MathF.Max(qx, qy), 0f) + MathF.Sqrt(ox * ox + oy * oy) - r;
        }

        private static float Cov(float sdf) => MathF.Min(1f, MathF.Max(0f, 0.5f - sdf));

        private static Sprite BuildRounded(int r, int mask, int thick)
        {
            int size = 2 * r + 2;
            float half = size * 0.5f;
            var px = new byte[size * size * 4];
            float rtl = (mask & TL) != 0 ? r : 0f, rtr = (mask & TR) != 0 ? r : 0f;
            float rbl = (mask & BL) != 0 ? r : 0f, rbr = (mask & BR) != 0 ? r : 0f;
            float t = thick;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - half, fy = y + 0.5f - half;
                    float a = Cov(Sdf(fx, fy, half, half, rtl, rtr, rbl, rbr));
                    if (thick > 0)
                    {
                        float inner = Cov(Sdf(fx, fy, half - t, half - t,
                            MathF.Max(rtl - t, 0f), MathF.Max(rtr - t, 0f), MathF.Max(rbl - t, 0f), MathF.Max(rbr - t, 0f)));
                        a *= 1f - inner;
                    }
                    int i = (y * size + x) * 4;
                    px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = (byte)Mathf.RoundToInt(a * 255f);
                }
            }
            return MakeSprite(px, size, size, new Vector4(r, r, r, r));
        }

        private static Sprite BuildCircle(int size)
        {
            var px = new byte[size * size * 4];
            float half = size * 0.5f, rad = half - 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f - half, fy = y + 0.5f - half;
                    float a = Cov(MathF.Sqrt(fx * fx + fy * fy) - rad);
                    int i = (y * size + x) * 4;
                    px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = (byte)Mathf.RoundToInt(a * 255f);
                }
            return MakeSprite(px, size, size, Vector4.zero);
        }

        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy);
            t = MathF.Min(1f, MathF.Max(0f, t));
            float cx = ax + dx * t - px, cy = ay + dy * t - py;
            return MathF.Sqrt(cx * cx + cy * cy);
        }

        private static Sprite BuildGlyph(bool cross)
        {
            const int size = 32;
            const float halfThick = 1.5f;
            var px = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, d;
                    if (cross) d = MathF.Min(SegDist(fx, fy, 9f, 9f, 23f, 23f), SegDist(fx, fy, 9f, 23f, 23f, 9f));
                    else d = MathF.Min(SegDist(fx, fy, 8f, 19f, 16f, 11f), SegDist(fx, fy, 16f, 11f, 24f, 19f));
                    float a = Cov(d - halfThick);
                    int i = (y * size + x) * 4;
                    px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = (byte)Mathf.RoundToInt(a * 255f);
                }
            return MakeSprite(px, size, size, Vector4.zero);
        }

        private static Sprite MakeSprite(byte[] managed, int w, int h, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.LoadRawTextureData(new Il2CppStructArray<byte>(managed));
            tex.Apply(false);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var spr = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
            spr.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return spr;
        }
    }
}
