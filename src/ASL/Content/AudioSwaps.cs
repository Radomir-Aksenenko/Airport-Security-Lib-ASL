using System;
using System.Collections.Generic;
using ASL.Core;
using BepInEx.Logging;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Content-mod audio swaps (manifest <c>content.audio</c>) and the <c>listAudioNames</c> discovery log.
    /// A swap replaces <c>AudioSource.clip</c> on every AudioSource whose current clip is named
    /// <c>target</c>. WAVs are decoded to managed floats at registration (pure C#, no Unity calls at boot);
    /// the <c>AudioClip</c> is built lazily the first time a target is found. Not driven per frame:
    /// <see cref="ContentRegistry"/> calls <see cref="Apply"/> on scene change and on a throttled poll.
    /// Limitation: one-shots played via <c>AudioSource.PlayOneShot(clip)</c> with a clip held in a script
    /// field are not reachable this way (there is no AudioSource.clip to repoint).
    /// </summary>
    internal sealed class AudioSwaps
    {
        private sealed class Swap
        {
            public string ModId;
            public string Target;
            public string Name;       // replacement clip name (file name without extension)
            public WavData Wav;       // freed once the clip exists
            public AudioClip Clip;
            public bool Disabled;
        }

        private readonly ManualLogSource _log;
        private readonly Dictionary<string, Swap> _byTarget = new Dictionary<string, Swap>(StringComparer.Ordinal);
        private bool _dumpNames;
        private bool _failedOnce;

        public AudioSwaps(ManualLogSource log) => _log = log;

        public bool HasWork
        {
            get
            {
                if (_dumpNames) return true;
                foreach (var s in _byTarget.Values) if (!s.Disabled) return true;
                return false;
            }
        }

        public void RequestNameDump() => _dumpNames = true;

        public void Register(string modId, string target, string name, WavData wav)
        {
            if (_byTarget.TryGetValue(target, out var prev))
            {
                _log.LogWarning($"[content] audio target '{target}' from '{modId}' ignored: already replaced by '{prev.ModId}'.");
                return;
            }
            _byTarget[target] = new Swap { ModId = modId, Target = target, Name = name, Wav = wav };
            _log.LogInfo($"[content] '{modId}' queued audio swap for '{target}' ({wav.FrameCount} frames, {wav.Channels}ch, {wav.SampleRate} Hz).");
        }

        public void Apply()
        {
            try
            {
                if (_dumpNames) DumpNames();

                bool any = false;
                foreach (var s in _byTarget.Values) if (!s.Disabled) { any = true; break; }
                if (!any) return;

                var sources = Resources.FindObjectsOfTypeAll<AudioSource>();
                int applied = 0;
                for (int i = 0; i < sources.Length; i++)
                {
                    var src = sources[i];
                    if (src == null) continue;
                    AudioClip cur;
                    string curName;
                    try
                    {
                        cur = src.clip;
                        if (cur == null) continue;
                        curName = cur.name;
                    }
                    catch { continue; }

                    if (!_byTarget.TryGetValue(curName, out var swap) || swap.Disabled) continue;
                    var repl = GetClip(swap);
                    if (repl == null) continue;
                    // The replacement may legitimately share the target's name (target "click" ← click.wav): compare identity.
                    if (cur.Pointer == repl.Pointer) continue;

                    bool wasPlaying = src.isPlaying;   // looping music keeps playing — with the new clip
                    src.clip = repl;
                    if (wasPlaying) src.Play();
                    applied++;
                }
                if (applied > 0) _log.LogInfo($"[content] audio: replaced the clip on {applied} AudioSource(s).");
            }
            catch (Exception ex)
            {
                if (!_failedOnce) { _failedOnce = true; _log.LogWarning($"[content] audio pass failed (will keep retrying quietly): {ex.Message}"); }
            }
        }

        private AudioClip GetClip(Swap swap)
        {
            if (swap.Clip != null) return swap.Clip;
            try
            {
                swap.Clip = ModAssets.ClipFromWav(swap.Wav, swap.Name);
                swap.Wav = null;
                return swap.Clip;
            }
            catch (Exception ex)
            {
                swap.Disabled = true;
                _log.LogWarning($"[content] audio swap '{swap.Target}' (from '{swap.ModId}') disabled: {ex.Message}");
                return null;
            }
        }

        // Lists distinct clip names once clips exist (first scene may not have any yet → stay pending).
        private void DumpNames()
        {
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<AudioClip>());
            if (all == null || all.Length == 0) return;
            _dumpNames = false;

            var names = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < all.Length; i++)
            {
                var o = all[i];
                if (o == null) continue;
                var n = o.name;
                if (!string.IsNullOrEmpty(n)) names.Add(n);
            }

            int shown = Math.Min(names.Count, 400);
            _log.LogInfo($"[content] loaded AudioClips: {all.Length}, {names.Count} distinct name(s). Showing {shown}:");
            int k = 0;
            foreach (var n in names)
            {
                if (k++ >= shown) break;
                _log.LogInfo($"    - {n}");
            }
        }
    }
}
