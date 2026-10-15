using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ASL.Api;
using ASL.Core;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslAssets"/>: files from the mod folder. Paths are joined safely (no rooted
    /// paths, no ".." escapes). PNGs go through the managed <see cref="PngDecoder"/> and the same
    /// <c>LoadRawTextureData</c> route as <see cref="UiUtil.SpriteFromPng"/> (never <c>ImageConversion</c>);
    /// WAVs through the managed <see cref="WavDecoder"/> into <c>AudioClip.Create</c> + <c>SetData</c>.
    /// Everything loaded is cached by absolute path; textures/sprites/clips are <c>HideAndDontSave</c>.
    /// The constructor touches nothing.
    /// </summary>
    internal sealed class ModAssets : IAslAssets
    {
        private readonly string _modId;
        private readonly string _modDirectory;
        private readonly ManualLogSource _log;

        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // log each bad file once

        public ModAssets(string modId, string modDirectory, ManualLogSource log)
        {
            _modId = modId;
            _modDirectory = modDirectory;
            _log = log;
        }

        public string GetPath(string relativePath) => StorageNames.SafeJoin(_modDirectory, relativePath);

        public byte[] ReadBytes(string relativePath) => File.ReadAllBytes(GetPath(relativePath));

        public string ReadText(string relativePath) => File.ReadAllText(GetPath(relativePath), Encoding.UTF8);

        public Texture2D LoadTexture(string relativePngPath)
        {
            if (!TryResolve(relativePngPath, out var path)) return null;
            if (_textures.TryGetValue(path, out var cached) && cached != null) return cached;

            var tex = BuildTexture(path, relativePngPath);
            if (tex != null) _textures[path] = tex;
            return tex;
        }

        public Sprite LoadSprite(string relativePngPath, float border = 0f)
        {
            if (!TryResolve(relativePngPath, out var path)) return null;
            if (border < 0f || float.IsNaN(border)) border = 0f;
            var key = path + "|" + border.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (_sprites.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = LoadTexture(relativePngPath);
            if (tex == null) return null;
            try
            {
                float w = tex.width, h = tex.height;
                float b = Math.Min(border, Math.Min(w, h) / 2f);
                var rect = new Rect(0f, 0f, w, h);
                var pivot = new Vector2(0.5f, 0.5f);
                Sprite spr;
                try
                {
                    // FullRect keeps UI sprites a plain quad (Tight would build an outline mesh); border → 9-slice.
                    spr = Sprite.Create(tex, rect, pivot, 100f, 0u, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
                }
                catch (Exception ex)
                {
                    // Same call UiUtil.SpriteFromPng uses (verified in-game); only loses the border.
                    if (b > 0f) Warn("sprite:" + key, $"Sprite.Create with border failed ({ex.Message}); '{relativePngPath}' will not be 9-sliced.");
                    spr = Sprite.Create(tex, rect, pivot, 100f);
                }
                if (spr == null) { Warn("sprite:" + key, $"could not create a sprite from '{relativePngPath}'."); return null; }
                spr.hideFlags = HideFlags.HideAndDontSave;
                _sprites[key] = spr;
                return spr;
            }
            catch (Exception ex)
            {
                Warn("sprite:" + key, $"LoadSprite('{relativePngPath}') failed: {ex.Message}");
                return null;
            }
        }

        public AudioClip LoadAudio(string relativeWavPath)
        {
            if (!TryResolve(relativeWavPath, out var path)) return null;
            if (_clips.TryGetValue(path, out var cached) && cached != null) return cached;

            try
            {
                if (!File.Exists(path)) { Warn("missing:" + path, $"audio file not found: {relativeWavPath}"); return null; }
                var clip = ClipFromWav(WavDecoder.Decode(File.ReadAllBytes(path)), Path.GetFileNameWithoutExtension(path));
                if (clip != null) _clips[path] = clip;
                return clip;
            }
            catch (Exception ex)
            {
                Warn("audio:" + path, $"could not load audio '{relativeWavPath}': {ex.Message}");
                return null;
            }
        }

        public AssetBundle LoadBundle(string relativePath)
        {
            if (!TryResolve(relativePath, out var path)) return null;
            if (_bundles.TryGetValue(path, out var cached) && cached != null) return cached;

            try
            {
                if (!File.Exists(path)) { Warn("missing:" + path, $"bundle not found: {relativePath}"); return null; }
                var bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) { Warn("bundle:" + path, $"AssetBundle.LoadFromFile returned null for '{relativePath}' (wrong Unity version/platform, or already loaded by another mod)."); return null; }
                _bundles[path] = bundle;
                return bundle;
            }
            catch (Exception ex)
            {
                Warn("bundle:" + path, $"could not load bundle '{relativePath}': {ex.Message}");
                return null;
            }
        }

        public UnityEngine.Object LoadBundleAsset(string bundleRelativePath, string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            var bundle = LoadBundle(bundleRelativePath);
            if (bundle == null) return null;
            try
            {
                var asset = bundle.LoadAsset(assetName, Il2CppType.Of<UnityEngine.Object>());
                if (asset == null) Warn($"asset:{bundleRelativePath}|{assetName}", $"asset '{assetName}' not found in bundle '{bundleRelativePath}'.");
                return asset;
            }
            catch (Exception ex)
            {
                Warn($"asset:{bundleRelativePath}|{assetName}", $"LoadBundleAsset('{bundleRelativePath}', '{assetName}') failed: {ex.Message}");
                return null;
            }
        }

        // ---- helpers ------------------------------------------------------------------------

        // Load* methods return null (and log) for bad paths instead of throwing, per the interface docs.
        private bool TryResolve(string relative, out string path)
        {
            path = null;
            try { path = GetPath(relative); return true; }
            catch (Exception ex)
            {
                Warn("path:" + relative, $"rejected asset path '{relative}': {ex.Message}");
                return false;
            }
        }

        private Texture2D BuildTexture(string path, string relative)
        {
            try
            {
                if (!File.Exists(path)) { Warn("missing:" + path, $"image not found: {relative}"); return null; }
                int w, h;
                var rgba = PngDecoder.Decode(File.ReadAllBytes(path), out w, out h);   // top-down RGBA32

                // Flip to Unity's bottom-up rows into a native array (same as UiUtil.SpriteFromPng / ContentRegistry).
                int stride = w * 4;
                var px = new Il2CppStructArray<byte>(rgba.Length);
                for (int row = 0; row < h; row++)
                {
                    int src = (h - 1 - row) * stride, dst = row * stride;
                    for (int b = 0; b < stride; b++) px[dst + b] = rgba[src + b];
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(px);
                tex.Apply(false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.name = Path.GetFileNameWithoutExtension(path);
                return tex;
            }
            catch (Exception ex)
            {
                Warn("tex:" + path, $"could not load image '{relative}': {ex.Message}");
                return null;
            }
        }

        /// <summary>Shared by assets and content audio swaps: decoded WAV → AudioClip (not streamed).</summary>
        internal static AudioClip ClipFromWav(WavData wav, string name)
        {
            int frames = wav.FrameCount;
            if (frames <= 0) throw new InvalidDataException("the file contains no audio samples");
            var clip = AudioClip.Create(name, frames, wav.Channels, wav.SampleRate, false);
            if (clip == null) throw new InvalidOperationException("AudioClip.Create returned null");

            var data = new Il2CppStructArray<float>(wav.Samples.Length);
            for (int i = 0; i < wav.Samples.Length; i++) data[i] = wav.Samples[i];
            if (!clip.SetData(data, 0)) throw new InvalidOperationException("AudioClip.SetData failed");
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        private void Warn(string id, string message)
        {
            if (_failed.Add(id)) _log.LogWarning($"[{_modId}] {message}");
        }
    }
}
