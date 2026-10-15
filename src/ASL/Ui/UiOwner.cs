using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Identity of the mod that created a piece of UI (used to label log lines) plus the guard that wraps
    /// every mod callback: exceptions are caught per call and logged with the mod name, once per burst of
    /// identical messages (so a callback that throws every frame cannot flood the log). Nothing escapes
    /// into Unity.
    /// </summary>
    internal sealed class UiOwner
    {
        public readonly string ModId;
        public readonly string ModName;
        public readonly ManualLogSource Log;

        /// <summary>Dialogs (Confirm / Prompt / Alert) this mod has open; destroyed without callbacks when the mod is released.</summary>
        public readonly List<UiWindow> Dialogs = new List<UiWindow>();

        private readonly HashSet<string> _reportedOnce = new HashSet<string>(StringComparer.Ordinal);
        private string _lastMsg;
        private float _lastTime;
        private int _suppressed;

        public UiOwner(string modId, string modName, ManualLogSource log)
        {
            ModId = modId ?? modName ?? "?";
            ModName = modName ?? modId ?? "?";
            Log = log;
        }

        /// <summary>Runs a mod callback; never throws.</summary>
        public void Fire(Action a, string what)
        {
            if (a == null) return;
            try { a(); } catch (Exception ex) { Report(what, ex); }
        }

        /// <summary>Runs a one-argument mod callback; never throws.</summary>
        public void Fire<T>(Action<T> a, T value, string what)
        {
            if (a == null) return;
            try { a(value); } catch (Exception ex) { Report(what, ex); }
        }

        /// <summary>Evaluates a mod-supplied function; returns <paramref name="fallback"/> if it throws.</summary>
        public T Eval<T>(Func<T> f, T fallback, string what)
        {
            if (f == null) return fallback;
            try { return f(); } catch (Exception ex) { Report(what, ex); return fallback; }
        }

        /// <summary>Logs a widget / window build failure once per (kind, message): mod name, widget type and the exception message.</summary>
        public void ReportBuildFailure(string kind, Exception ex)
        {
            try
            {
                string msg = ex.GetType().Name + ": " + ex.Message;
                if (!_reportedOnce.Add(kind + "|" + msg)) return;
                Log?.LogError($"[{ModName}] UI '{kind}' could not be built ({msg}); showing a placeholder instead. {ex.StackTrace}");
            }
            catch { /* logging must never throw */ }
        }

        /// <summary>Logs an exception from a callback (deduplicated within a 5 s burst).</summary>
        public void Report(string what, Exception ex)
        {
            try
            {
                string msg = ex.GetType().Name + ": " + ex.Message;
                float now = Time.realtimeSinceStartup;
                if (msg == _lastMsg && now - _lastTime < 5f) { _suppressed++; _lastTime = now; return; }
                if (_suppressed > 0) Log?.LogWarning($"[{ModName}] ({_suppressed} similar UI error(s) suppressed)");
                _lastMsg = msg; _lastTime = now; _suppressed = 0;
                Log?.LogError($"[{ModName}] UI callback '{what}' threw: {ex}");
            }
            catch { /* logging must never throw */ }
        }
    }
}
