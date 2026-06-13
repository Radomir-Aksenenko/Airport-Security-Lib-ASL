using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Applies no-code content from <c>type: "content"</c> mods. v1 = texture swaps: a mod ships a
    /// PNG and names a game texture; ASL decodes the PNG (managed <see cref="PngDecoder"/>) into
    /// RGBA32 and applies it via the marshal-safe path (Unity 6 / IL2CPP can't call the span-based
    /// <c>ImageConversion.LoadImage</c>):
    /// <list type="bullet">
    /// <item>readable target → write in place (<c>Reinitialize</c> + <c>LoadRawTextureData</c> + <c>Apply</c>); every holder updates.</item>
    /// <item>non-readable target → build a readable replacement texture and reassign <c>Material.mainTexture</c> references to it.</item>
    /// </list>
    /// Re-applied on each scene change as textures/materials stream in.
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

            // Flip to Unity's bottom-up rows and copy into a native byte array element-wise (the
            // implicit byte[] -> Il2Cpp conversion routes through a span path missing on this build).
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

                Il2CppArrayBase<Material> mats = null;   // loaded lazily (only if needed)
                int totalApplied = 0;

                foreach (var swap in _swaps)
                {
                    if (swap.Disabled) continue;

                    // Current target textures by name (pointers change as scenes reload).
                    var targets = new List<Texture2D>();
                    for (int i = 0; i < texAll.Length; i++)
                    {
                        var t = texAll[i];
                        if (t != null && t.name == swap.Target) targets.Add(t);
                    }
                    if (targets.Count == 0) continue;   // not loaded yet; retry next scene

                    int applied = 0;
                    try
                    {
                        foreach (var target in targets)
                        {
                            if (target.isReadable)
                            {
                                // In-place: every material/sprite using this texture updates automatically.
                                target.Reinitialize(swap.Width, swap.Height, TextureFormat.RGBA32, false);
                                target.LoadRawTextureData(swap.Pixels);
                                target.Apply(false);
                                applied++;
                            }
                            else
                            {
                                // Non-readable: reassign Material.mainTexture references to a replacement.
                                if (mats == null) mats = Resources.FindObjectsOfTypeAll<Material>();
                                var repl = GetReplacement(swap);
                                var tp = target.Pointer;
                                for (int m = 0; m < mats.Length; m++)
                                {
                                    var mat = mats[m];
                                    if (mat == null) continue;
                                    try
                                    {
                                        var mt = mat.mainTexture;
                                        if (mt != null && mt.Pointer == tp) { mat.mainTexture = repl; applied++; }
                                    }
                                    catch { /* shader without _MainTex, etc. */ }
                                }
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
                        _log.LogInfo($"[content] '{swap.Target}': applied to {applied} target(s)/material(s).");
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
            tex.hideFlags = HideFlags.HideAndDontSave;   // survive scene unloads, don't get saved
            swap.Replacement = tex;
            return tex;
        }
    }
}
