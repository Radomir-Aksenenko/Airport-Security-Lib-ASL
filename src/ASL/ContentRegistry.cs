using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Applies no-code content from <c>type: "content"</c> mods. v1 = texture swaps: a mod ships a
    /// PNG and names a game texture; ASL decodes the PNG (managed <see cref="PngDecoder"/>) into
    /// RGBA32 and applies it the marshal-safe way (Unity 6 / IL2CPP can't call the span-based
    /// <c>ImageConversion.LoadImage</c>):
    /// <list type="bullet">
    /// <item>readable target → write in place; every holder updates automatically.</item>
    /// <item>non-readable target → build a readable replacement and repoint references that used the
    /// target: <c>Material.mainTexture</c>, UI <c>Image.sprite</c> (rebuilt via <c>Sprite.Create</c>),
    /// and <c>RawImage.texture</c>.</item>
    /// </list>
    /// Re-applied on each scene change as textures/materials/UI stream in.
    /// </summary>
    internal sealed class ContentRegistry
    {
        private readonly ManualLogSource _log;
        private readonly List<TextureSwap> _swaps = new();
        private bool _dumpNames;

        private sealed class TextureSwap
        {
            public string ModId;
            public string Target;
            public int Width;
            public int Height;
            public Il2CppStructArray<byte> Pixels;  // RGBA32, bottom-up (Unity origin)
            public Texture2D Replacement;           // built lazily for the reassignment path
            public Sprite ReplacementSprite;        // built lazily for UI Image reassignment
            public bool Disabled;
        }

        public ContentRegistry(ManualLogSource log, IAslEvents events)
        {
            _log = log;
            events.SceneChanged += _ => Apply();
        }

        public void RegisterTextureSwap(string modId, string target, byte[] png)
        {
            int w, h;
            byte[] rgbaTopDown;
            try { rgbaTopDown = PngDecoder.Decode(png, out w, out h); }
            catch (Exception ex)
            {
                _log.LogError($"[content] '{modId}' could not decode PNG for '{target}': {ex.Message}");
                return;
            }

            // Flip to Unity's bottom-up rows and copy into a native byte array element-wise.
            int stride = w * 4;
            var pixels = new Il2CppStructArray<byte>(rgbaTopDown.Length);
            for (int row = 0; row < h; row++)
            {
                int src = (h - 1 - row) * stride;
                int dst = row * stride;
                for (int b = 0; b < stride; b++) pixels[dst + b] = rgbaTopDown[src + b];
            }

            _swaps.Add(new TextureSwap { ModId = modId, Target = target, Width = w, Height = h, Pixels = pixels });
            _log.LogInfo($"[content] '{modId}' queued texture swap for '{target}' ({w}x{h} RGBA32).");
        }

        public void RequestTextureNameDump() => _dumpNames = true;

        public bool HasWork => _dumpNames || _swaps.Exists(s => !s.Disabled);

        public void Apply()
        {
            if (!HasWork) return;

            try
            {
                var texAll = Resources.FindObjectsOfTypeAll<Texture2D>();

                if (_dumpNames)
                {
                    _dumpNames = false;
                    int shown = Math.Min(texAll.Length, 60);
                    _log.LogInfo($"[content] loaded textures: {texAll.Length}. First {shown} (name | readable):");
                    for (int i = 0; i < shown; i++)
                    {
                        var t = texAll[i];
                        if (t != null && !string.IsNullOrEmpty(t.name)) _log.LogInfo($"    - {t.name} | readable={t.isReadable}");
                    }
                }

                // Loaded lazily, only when a non-readable target actually needs reassignment.
                Il2CppArrayBase<Material> mats = null;
                Il2CppArrayBase<Image> images = null;
                Il2CppArrayBase<RawImage> rawImages = null;
                int totalApplied = 0;

                foreach (var swap in _swaps)
                {
                    if (swap.Disabled) continue;

                    int applied = 0;
                    try
                    {
                        // Partition the named target textures into readable (write in place) and
                        // non-readable (collect pointers to repoint references away from).
                        var nonReadablePtrs = new HashSet<IntPtr>();
                        bool anyTarget = false;
                        for (int i = 0; i < texAll.Length; i++)
                        {
                            var t = texAll[i];
                            if (t == null || t.name != swap.Target) continue;
                            anyTarget = true;
                            if (t.isReadable)
                            {
                                t.Reinitialize(swap.Width, swap.Height, TextureFormat.RGBA32, false);
                                t.LoadRawTextureData(swap.Pixels);
                                t.Apply(false);
                                applied++;
                            }
                            else
                            {
                                nonReadablePtrs.Add(t.Pointer);
                            }
                        }
                        if (!anyTarget) continue;   // not loaded yet; retry next scene

                        if (nonReadablePtrs.Count > 0)
                        {
                            var repl = GetReplacement(swap);

                            if (mats == null) mats = Resources.FindObjectsOfTypeAll<Material>();
                            for (int m = 0; m < mats.Length; m++)
                            {
                                var mat = mats[m];
                                if (mat == null) continue;
                                try
                                {
                                    var mt = mat.mainTexture;
                                    if (mt != null && nonReadablePtrs.Contains(mt.Pointer)) { mat.mainTexture = repl; applied++; }
                                }
                                catch { /* shader without _MainTex */ }
                            }

                            if (images == null) images = Resources.FindObjectsOfTypeAll<Image>();
                            for (int im = 0; im < images.Length; im++)
                            {
                                var img = images[im];
                                if (img == null) continue;
                                try
                                {
                                    var spr = img.sprite;
                                    if (spr != null && spr.texture != null && nonReadablePtrs.Contains(spr.texture.Pointer))
                                    { img.sprite = GetReplacementSprite(swap); applied++; }
                                }
                                catch { }
                            }

                            if (rawImages == null) rawImages = Resources.FindObjectsOfTypeAll<RawImage>();
                            for (int r = 0; r < rawImages.Length; r++)
                            {
                                var raw = rawImages[r];
                                if (raw == null) continue;
                                try
                                {
                                    var tx = raw.texture;
                                    if (tx != null && nonReadablePtrs.Contains(tx.Pointer)) { raw.texture = repl; applied++; }
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        swap.Disabled = true;
                        _log.LogWarning($"[content] swap '{swap.Target}' (from '{swap.ModId}') disabled: {ex.Message}");
                        continue;
                    }

                    if (applied > 0)
                    {
                        totalApplied += applied;
                        _log.LogInfo($"[content] '{swap.Target}': applied to {applied} holder(s).");
                    }
                }

                if (totalApplied > 0)
                    _log.LogInfo($"[content] applied {totalApplied} texture write(s)/reassignment(s).");
            }
            catch (Exception ex)
            {
                _log.LogError($"[content] apply pass failed: {ex.Message}");
            }
        }

        private Texture2D GetReplacement(TextureSwap swap)
        {
            if (swap.Replacement != null) return swap.Replacement;
            var tex = new Texture2D(swap.Width, swap.Height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(swap.Pixels);
            tex.Apply(false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            swap.Replacement = tex;
            return tex;
        }

        private Sprite GetReplacementSprite(TextureSwap swap)
        {
            if (swap.ReplacementSprite != null) return swap.ReplacementSprite;
            var tex = GetReplacement(swap);
            // Full-texture sprite, centred pivot. Atlas sub-sprites aren't reconstructed (caveat).
            var spr = Sprite.Create(tex, new Rect(0f, 0f, swap.Width, swap.Height), new Vector2(0.5f, 0.5f), 100f);
            spr.hideFlags = HideFlags.HideAndDontSave;
            swap.ReplacementSprite = spr;
            return spr;
        }
    }
}
