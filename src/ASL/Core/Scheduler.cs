using System;
using System.Collections;
using System.Collections.Generic;
using ASL.Api;

namespace ASL.Core
{
    /// <summary>
    /// Pure timer / coroutine-lite scheduler behind <c>ctx.Timers</c>. It has no Unity dependency: the
    /// host calls <see cref="Tick"/> once per frame and passes the scaled and unscaled frame deltas, so
    /// it is fully unit-testable with a fake clock.
    ///
    /// Guarantees: nothing allocates per <see cref="Tick"/> while idle (and only the work the callbacks
    /// themselves do while busy); callbacks may freely add or cancel timers (including themselves, or
    /// everything) while a tick is running; a timer added during a tick is first processed on the NEXT
    /// tick; an exception in a callback is passed to the error callback and cancels just that timer.
    /// Everything is single-threaded (the game's main thread).
    /// </summary>
    public sealed class Scheduler
    {
        private enum Kind : byte { After, Every, NextFrame, When, Routine }
        private enum RoutineWait : byte { Start, Frame, Seconds, Custom }

        private sealed class Task : IAslTimer
        {
            public Scheduler Owner;
            public Kind Kind;
            public bool Active = true;
            public bool Unscaled = true;
            public Action Action;
            public Func<bool> Condition;
            public float Remaining;     // After/Every: seconds left. Routine(Seconds): seconds left.
            public float Interval;      // Every
            public float Timeout;       // When (0 = none)
            public float Elapsed;       // When
            public IEnumerator Routine;
            public RoutineWait Wait;
            public IAslWait Custom;

            public bool IsActive => Active;
            public void Cancel() => Owner?.CancelTask(this);
        }

        private readonly List<Task> _tasks = new();
        private readonly Action<Exception, string> _onError;
        private int _active;
        private bool _ticking;
        private bool _dirty;     // cancelled entries are waiting to be compacted out of _tasks

        /// <param name="onError">Called with the exception and a short description (e.g. "After callback") when a callback throws. May be null.</param>
        public Scheduler(Action<Exception, string> onError = null) => _onError = onError;

        /// <summary>Number of timers / routines that are still scheduled.</summary>
        public int ActiveCount => _active;

        // ---- creation -----------------------------------------------------------------------

        /// <summary>Run <paramref name="action"/> once after <paramref name="seconds"/> (0 or less = on the next tick).</summary>
        public IAslTimer After(float seconds, Action action, bool unscaled = true)
        {
            if (action == null) return Inert();
            return Add(new Task { Kind = Kind.After, Action = action, Remaining = Sane(seconds), Unscaled = unscaled });
        }

        /// <summary>Run <paramref name="action"/> every <paramref name="seconds"/> until cancelled (at most once per tick).</summary>
        public IAslTimer Every(float seconds, Action action, bool unscaled = true)
        {
            if (action == null) return Inert();
            var s = Sane(seconds);
            return Add(new Task { Kind = Kind.Every, Action = action, Remaining = s, Interval = s, Unscaled = unscaled });
        }

        /// <summary>Run <paramref name="action"/> on the next tick.</summary>
        public IAslTimer NextFrame(Action action)
        {
            if (action == null) return Inert();
            return Add(new Task { Kind = Kind.NextFrame, Action = action });
        }

        /// <summary>Poll <paramref name="condition"/> each tick; run <paramref name="action"/> once it is true. <paramref name="timeoutSeconds"/> &gt; 0 gives up (silently) after that long (unscaled).</summary>
        public IAslTimer When(Func<bool> condition, Action action, float timeoutSeconds = 0f)
        {
            if (condition == null || action == null) return Inert();
            return Add(new Task { Kind = Kind.When, Condition = condition, Action = action, Timeout = Sane(timeoutSeconds) });
        }

        /// <summary>
        /// Start a coroutine-lite. Its first step runs on the next tick. <c>yield return null</c> = resume next tick;
        /// a float / int / double = that many (unscaled) seconds; an <see cref="IAslWait"/> = until it reports done;
        /// anything else = one tick.
        /// </summary>
        public IAslTimer Start(IEnumerator routine)
        {
            if (routine == null) return Inert();
            return Add(new Task { Kind = Kind.Routine, Routine = routine, Wait = RoutineWait.Start });
        }

        /// <summary>Cancel everything scheduled. Safe to call from inside a callback.</summary>
        public void CancelAll()
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                var t = _tasks[i];
                if (t.Active) { t.Active = false; _active--; }
            }
            if (_ticking) _dirty = true;
            else { _tasks.Clear(); _dirty = false; }
        }

        private static float Sane(float v) => float.IsNaN(v) || v < 0f ? 0f : v;

        private IAslTimer Add(Task t)
        {
            t.Owner = this;
            _tasks.Add(t);
            _active++;
            return t;
        }

        private static IAslTimer Inert() => new Task { Active = false };

        private void CancelTask(Task t)
        {
            if (!t.Active) return;
            t.Active = false;
            _active--;
            _dirty = true;   // compacted at the end of the running tick, or right away below if idle
            if (!_ticking && _active == 0) { _tasks.Clear(); _dirty = false; }
        }

        // ---- ticking ------------------------------------------------------------------------

        /// <summary>Advance the scheduler by one frame.</summary>
        /// <param name="scaledDt">Frame delta affected by the game's time scale (Time.deltaTime).</param>
        /// <param name="unscaledDt">Frame delta ignoring the time scale (Time.unscaledDeltaTime).</param>
        public void Tick(float scaledDt, float unscaledDt)
        {
            if (_tasks.Count == 0 || _ticking) return;   // idle: no work; a re-entrant Tick (a callback ticking us) is ignored
            if (scaledDt < 0f || float.IsNaN(scaledDt)) scaledDt = 0f;
            if (unscaledDt < 0f || float.IsNaN(unscaledDt)) unscaledDt = 0f;

            _ticking = true;
            try
            {
                int n = _tasks.Count;   // tasks added by callbacks (index >= n) wait for the next tick
                for (int i = 0; i < n; i++)
                {
                    var t = _tasks[i];
                    if (!t.Active) continue;
                    try { Step(t, scaledDt, unscaledDt); }
                    catch (Exception ex)
                    {
                        Finish(t);
                        Report(ex, t);
                    }
                }
            }
            finally
            {
                _ticking = false;
                if (_dirty) Compact();
            }
        }

        private void Step(Task t, float sdt, float udt)
        {
            switch (t.Kind)
            {
                case Kind.NextFrame:
                    Finish(t);
                    t.Action();
                    break;

                case Kind.After:
                    t.Remaining -= t.Unscaled ? udt : sdt;
                    if (t.Remaining <= 0f) { Finish(t); t.Action(); }
                    break;

                case Kind.Every:
                    t.Remaining -= t.Unscaled ? udt : sdt;
                    if (t.Remaining <= 0f)
                    {
                        t.Remaining += t.Interval;
                        if (t.Remaining <= 0f) t.Remaining = t.Interval;   // fell far behind (or interval 0): drop the backlog
                        t.Action();
                    }
                    break;

                case Kind.When:
                    if (t.Condition())
                    {
                        Finish(t);
                        t.Action();
                    }
                    else if (t.Timeout > 0f)
                    {
                        t.Elapsed += udt;
                        if (t.Elapsed >= t.Timeout) Finish(t);
                    }
                    break;

                case Kind.Routine:
                    StepRoutine(t, udt);
                    break;
            }
        }

        private void StepRoutine(Task t, float udt)
        {
            switch (t.Wait)
            {
                case RoutineWait.Seconds:
                    t.Remaining -= udt;
                    if (t.Remaining > 0f) return;
                    break;
                case RoutineWait.Custom:
                    if (!t.Custom.Tick(udt)) return;
                    t.Custom = null;
                    break;
                // Start / Frame: resume right away.
            }

            if (!t.Routine.MoveNext()) { Finish(t); return; }

            switch (t.Routine.Current)
            {
                case null: t.Wait = RoutineWait.Frame; break;
                case float f: t.Wait = RoutineWait.Seconds; t.Remaining = Sane(f); break;
                case int i: t.Wait = RoutineWait.Seconds; t.Remaining = Sane(i); break;
                case double d: t.Wait = RoutineWait.Seconds; t.Remaining = Sane((float)d); break;
                case IAslWait w: t.Wait = RoutineWait.Custom; t.Custom = w; break;
                default: t.Wait = RoutineWait.Frame; break;   // unknown yield value: behave like `yield return null`
            }
        }

        // Mark a task done so it is compacted out (does not call the owner's callback).
        private void Finish(Task t)
        {
            if (!t.Active) return;
            t.Active = false;
            _active--;
            _dirty = true;
        }

        private void Report(Exception ex, Task t)
        {
            try { _onError?.Invoke(ex, t.Kind + " callback"); }
            catch { /* the error sink must never break the scheduler */ }
        }

        // Remove inactive entries, preserving order.
        private void Compact()
        {
            int w = 0;
            for (int r = 0; r < _tasks.Count; r++)
            {
                var t = _tasks[r];
                if (t.Active) _tasks[w++] = t;
            }
            _tasks.RemoveRange(w, _tasks.Count - w);
            _dirty = false;
        }
    }
}
