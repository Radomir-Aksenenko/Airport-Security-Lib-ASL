using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Applies no-code content from <c>type: "content"</c> mods. v1 targets texture swaps:
    /// a mod ships a PNG and names a game texture to replace; ASL writes the PNG into every loaded
    /// <see cref="Texture2D"/> with that name, re-applying on each scene change as textures stream in.
    ///
    /// Note: the pixel write uses <c>ImageConversion.LoadImage</c>, which on some Unity 6 / IL2CPP
    /// builds is span-based and not marshalable (missing <c>ReadOnlySpan.GetPinnableReference</c>).
    /// When that happens the swap is reported once and then disabled, so the rest of ASL — including
    /// the texture-name discovery dump, which is how modders find swap targets — keeps working.
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
            public Il2CppStructArray<byte> Pixels;
            public bool Disabled;   // set after a failed attempt so we stop retrying every scene
        }

        public ContentRegistry(ManualLogSource log, IAslEvents events)
        {
            _log = log;
            events.SceneChanged += _ => Apply();
        }

        public void RegisterTextureSwap(string modId, string target, byte[] png)
        {
            // Copy into an IL2CPP byte array element-wise (the implicit byte[] conversion routes
            // through a span path that is missing on some IL2CPP builds).
            var pixels = new Il2CppStructArray<byte>(png.Length);
            for (int i = 0; i < png.Length; i++) pixels[i] = png[i];

            _swaps.Add(new TextureSwap { ModId = modId, Target = target, Pixels = pixels });
            _log.LogInfo($"[content] '{modId}' queued texture swap for '{target}' ({png.Length} bytes).");
        }

        public void RequestTextureNameDump() => _dumpNames = true;

        public bool HasWork => _dumpNames || _swaps.Exists(s => !s.Disabled);

        public void Apply()
        {
            if (!HasWork) return;

            try
            {
                var all = Resources.FindObjectsOfTypeAll<Texture2D>();
                int scanned = all.Length;

                if (_dumpNames)
                {
                    _dumpNames = false;
                    int shown = Math.Min(scanned, 60);
                    _log.LogInfo($"[content] loaded textures: {scanned}. First {shown} names (use these as swap targets):");
                    for (int i = 0; i < shown; i++)
                    {
                        var t = all[i];
                        if (t != null && !string.IsNullOrEmpty(t.name)) _log.LogInfo($"    - {t.name}");
                    }
                }

                int replaced = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    var t = all[i];
                    if (t == null || string.IsNullOrEmpty(t.name)) continue;

                    for (int s = 0; s < _swaps.Count; s++)
                    {
                        var swap = _swaps[s];
                        if (swap.Disabled || t.name != swap.Target) continue;
                        try
                        {
                            // Replaces the texture's pixels in place; every material using it updates.
                            ImageConversion.LoadImage(t, swap.Pixels);
                            replaced++;
                        }
                        catch (Exception ex)
                        {
                            swap.Disabled = true;   // don't retry a swap the runtime can't perform
                            _log.LogWarning($"[content] swap '{swap.Target}' (from '{swap.ModId}') disabled: " +
                                            $"texture write unavailable on this build ({ex.Message}).");
                        }
                    }
                }

                if (replaced > 0)
                    _log.LogInfo($"[content] applied {replaced} texture swap(s) (scanned {scanned} textures).");
            }
            catch (Exception ex)
            {
                _log.LogError($"[content] apply pass failed: {ex.Message}");
            }
        }
    }
}
