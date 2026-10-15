using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ASL
{
    /// <summary>Accumulated per-frame cost of one mod, written by its <see cref="ModEvents"/> / <see cref="ModTimers"/> (Stopwatch ticks, no allocation).</summary>
    internal sealed class ModPerf
    {
        public readonly string ModId;
        public ModPerf(string modId) => ModId = modId;

        // Written every frame by the owners (reset by the profiler once per second).
        public long UpdateTicks;
        public long LateUpdateTicks;
        public long TimerTicks;

        /// <summary>Set by <see cref="ModTimers"/> whenever it changes (cheap read for snapshots).</summary>
        public Func<int> ActiveTimers;
        public volatile bool UpdateDisabled;
        public volatile bool LateUpdateDisabled;

        // Last completed one-second window.
        public float AvgUpdateMs;
        public float AvgLateUpdateMs;
        public float AvgTimerMs;
    }

    /// <summary>One row of <see cref="Profiler.Snapshot"/>.</summary>
    internal struct ProfilerEntry
    {
        public string ModId;
        /// <summary>Average cost of the mod's Update handlers per frame over the last second, in milliseconds.</summary>
        public float UpdateMs;
        public float LateUpdateMs;
        /// <summary>Average cost of the mod's timer / routine callbacks per frame, in milliseconds.</summary>
        public float TimerMs;
        public int ActiveTimers;
        /// <summary>True if ASL switched this mod's Update/LateUpdate handlers off after repeated exceptions.</summary>
        public bool Disabled;
    }

    /// <summary>
    /// Lightweight frame / per-mod profiler for the "ASL" F8 page and the console <c>perf</c> command.
    /// <see cref="EndFrame"/> runs once per frame from the event bus (no allocation); queries are for the UI:
    /// <see cref="Fps"/>, <see cref="FrameTimeMs"/>, <see cref="Snapshot"/>. All main-thread.
    /// </summary>
    internal static class Profiler
    {
        private const float WindowSeconds = 1f;
        private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

        private static ModPerf[] _mods = Array.Empty<ModPerf>();   // copy-on-write: registration is rare, frames just read
        private static float _windowTime;
        private static int _windowFrames;
        private static bool _first = true;

        /// <summary>Frames per second averaged over the last completed window.</summary>
        public static float Fps { get; private set; }

        /// <summary>Average frame time (unscaled) over the last completed window, in milliseconds.</summary>
        public static float FrameTimeMs { get; private set; }

        /// <summary>Duration of the most recent frame (unscaled), in milliseconds.</summary>
        public static float LastFrameMs { get; private set; }

        public static void Register(ModPerf perf)
        {
            if (perf == null) return;
            var cur = _mods;
            for (int i = 0; i < cur.Length; i++) if (ReferenceEquals(cur[i], perf)) return;
            var next = new ModPerf[cur.Length + 1];
            Array.Copy(cur, next, cur.Length);
            next[cur.Length] = perf;
            _mods = next;
        }

        public static void Unregister(ModPerf perf)
        {
            var cur = _mods;
            int at = Array.IndexOf(cur, perf);
            if (at < 0) return;
            var next = new ModPerf[cur.Length - 1];
            if (at > 0) Array.Copy(cur, 0, next, 0, at);
            if (at < cur.Length - 1) Array.Copy(cur, at + 1, next, at, cur.Length - at - 1);
            _mods = next;
        }

        /// <summary>Called once per frame by the event bus.</summary>
        public static void EndFrame(float unscaledDt)
        {
            if (unscaledDt <= 0f) return;
            LastFrameMs = unscaledDt * 1000f;
            if (_first)
            {
                _first = false;
                Fps = 1f / unscaledDt;
                FrameTimeMs = LastFrameMs;
            }

            _windowTime += unscaledDt;
            _windowFrames++;
            if (_windowTime < WindowSeconds) return;

            Fps = _windowFrames / _windowTime;
            FrameTimeMs = _windowTime * 1000f / _windowFrames;

            var mods = _mods;
            double perFrame = MsPerTick / _windowFrames;
            for (int i = 0; i < mods.Length; i++)
            {
                var m = mods[i];
                m.AvgUpdateMs = (float)(m.UpdateTicks * perFrame);
                m.AvgLateUpdateMs = (float)(m.LateUpdateTicks * perFrame);
                m.AvgTimerMs = (float)(m.TimerTicks * perFrame);
                m.UpdateTicks = 0; m.LateUpdateTicks = 0; m.TimerTicks = 0;
            }
            _windowTime = 0f;
            _windowFrames = 0;
        }

        /// <summary>Clear <paramref name="into"/> and fill it with one entry per code mod (load order). Allocation-free once the list has capacity.</summary>
        public static void Snapshot(List<ProfilerEntry> into)
        {
            if (into == null) return;
            into.Clear();
            var mods = _mods;
            for (int i = 0; i < mods.Length; i++)
            {
                var m = mods[i];
                int timers = 0;
                try { timers = m.ActiveTimers?.Invoke() ?? 0; } catch { }
                into.Add(new ProfilerEntry
                {
                    ModId = m.ModId,
                    UpdateMs = m.AvgUpdateMs,
                    LateUpdateMs = m.AvgLateUpdateMs,
                    TimerMs = m.AvgTimerMs,
                    ActiveTimers = timers,
                    Disabled = m.UpdateDisabled || m.LateUpdateDisabled,
                });
            }
        }
    }
}
