using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ASL.Api;
using ASL.Core;
using BepInEx.Logging;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// The in-game dev console: a toolkit window (output scroll + input row) over <see cref="CommandRegistry"/>.
    /// Toggle with the "Console" keybind (ASL's own, default BackQuote). The window is built lazily on first open;
    /// while closed the per-frame <see cref="Tick"/> is a couple of field checks (plus draining mirrored log lines
    /// when that option is on). Everything text-related is polled (no UnityEvent listeners).
    /// </summary>
    internal static class DevConsole
    {
        private const string WindowId = "console";
        private const float RebuildInterval = 0.1f;      // output text is rebuilt at most 10x/s
        private const int MaxChars = 14000;              // TMP mesh limit is ~16k visible glyphs: stay below
        private const int MaxDrainPerTick = 200;
        private const int MaxQueued = 2000;             // mirrored lines kept while the console is open
        private const int MaxClosedQueued = 200;        // ... and while it is closed (oldest dropped; drained when it opens)

        private static ManualLogSource _log;
        private static CommandRegistry _reg;
        private static KeybindManager _keys;
        private static ModUi _ui;
        private static AslKeybind _bind;

        private static UiWindow _win;
        private static UiText _out;
        private static UiContainer _scroll;
        private static IUiInput _input;
        private static IUiToggle _mirrorToggle;
        private static bool _buildFailed;

        private static readonly CommandHistory History = new CommandHistory(50);
        private static readonly List<string> Snap = new List<string>(CommandRegistry.BufferLines);
        private static readonly StringBuilder Sb = new StringBuilder(MaxChars + 256);
        private static int _renderedVersion = -1;
        private static float _lastRebuild;
        private static int _scrollFrames;
        private static int _stripFrames;
        private static bool _forceRebuild;
        private static int _openFrame;

        // BepInEx log mirroring: the listener runs on whatever thread logs; it only enqueues.
        private static readonly ConcurrentQueue<string> LogQueue = new ConcurrentQueue<string>();
        private static MirrorListener _listener;         // registered once, lazily, and never removed (Listeners is not thread-safe to edit while logging)
        private static volatile bool _mirrorOn;          // the on/off switch the listener reads
        private static volatile bool _openFlag;          // mirror of IsOpen the listener thread may read
        private static int _queued;

        /// <summary>True while the console window is showing.</summary>
        public static bool IsOpen => _win != null && _win.IsOpen;

        /// <summary>True while BepInEx log lines are mirrored into the console.</summary>
        public static bool Mirroring => _mirrorOn;

        /// <summary>The toolkit facade ASL's own windows (console, <c>ui.demo</c>) are created through.</summary>
        public static ModUi Ui => _ui;

        /// <summary>The console's own toggle keybind (shown on the F8 "ASL" page so it can be rebound); null if registration failed.</summary>
        internal static IAslKeybind Bind => _bind;

        public static CommandHistory CommandHistory => History;

        public static void Init(ManualLogSource log, EventBus bus, CommandRegistry reg, KeybindManager keys, UiManager sharedUi)
        {
            _log = log; _reg = reg; _keys = keys;
            _ui = new ModUi("asl", "ASL", null, sharedUi, log) { Theme = UiTheme.Dark };

            try
            {
                _bind = keys.GetOrCreate("asl", "ASL", "console", "Console", KeyCode.BackQuote);
                _bind.Pressed += Toggle;
            }
            catch (Exception ex) { log.LogWarning($"Console keybind failed: {ex.Message}"); }

            ConsoleCommands.RegisterAll(reg);
            reg.AddLines(ConsoleText.Color(CommandRegistry.DimColor, "ASL console. Type 'help' for commands, Tab completes, Up/Down is history."));
            bus.Update += Tick;
        }

        // ---- open / close -------------------------------------------------------------------------

        public static void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public static void Open()
        {
            if (!EnsureWindow()) return;
            if (_win.IsOpen) return;
            _forceRebuild = true;
            _openFrame = Time.frameCount;
            _scrollFrames = 3;
            _stripFrames = _bind != null && _bind.Key == KeyCode.BackQuote ? 3 : 0;
            _win.Open();
            _openFlag = true;
            try { _input?.Focus(); } catch (Exception ex) { Report("focus", ex); }
        }

        public static void Close()
        {
            if (_win != null && _win.IsOpen) _win.Close();
        }

        private static bool EnsureWindow()
        {
            if (_win != null && !_win.Dead) return true;
            if (_buildFailed) return false;
            try
            {
                var opt = new UiWindowOptions
                {
                    Width = 1000f, Anchor = UiAnchor.Top, Scrollable = false, CloseOnEscape = true,
                    Theme = UiTheme.Dark, RememberPosition = true,
                };
                var win = (UiWindow)_ui.CreateWindow(WindowId, "ASL Console", opt);

                var scroll = win.AddScroll(460f);
                _scroll = (UiContainer)scroll;
                var label = scroll.AddLabel(string.Empty, UiTextStyle.Mono);
                label.FontSize = 20f;
                _out = (UiText)label;

                var row = win.AddRow(8f);
                _input = row.AddInput("Type a command (help)...", string.Empty, OnSubmit);
                _input.CharacterLimit = 512;
                row.AddButton("Clear", () => _reg.ClearBuffer(), UiButtonStyle.Secondary).SetSize(110f, 44f);

                _mirrorToggle = win.AddToggle("Mirror BepInEx log", Mirroring, SetMirror);
                _mirrorToggle.Tooltip = "Also show BepInEx log lines here (same as: log.mirror on|off)";

                win.Closed += OnClosed;
                _win = win;
                return true;
            }
            catch (Exception ex)
            {
                _buildFailed = true;
                try { _log.LogError($"Console window build failed: {ex}"); } catch { }
                return false;
            }
        }

        private static void OnClosed()
        {
            _openFlag = false;
            try { if (_input != null) _input.Text = string.Empty; } catch { }
        }

        // ---- input --------------------------------------------------------------------------------

        private static void OnSubmit(string text)
        {
            text ??= string.Empty;
            string line = text.Trim();
            try
            {
                if (line.Length > 0)
                {
                    _reg.AddLines(ConsoleText.Color(CommandRegistry.DimColor, "> " +ConsoleText.Literal(line)));
                    History.Add(line);
                    _reg.Execute(line);
                }
            }
            catch (Exception ex) { Report("submit", ex); }
            finally
            {
                try { _input.Text = string.Empty; _input.Focus(); } catch (Exception ex) { Report("refocus", ex); }
                _scrollFrames = 3;
            }
        }

        private static void SetInput(string text)
        {
            try
            {
                _input.Text = text;
                var field = _input.GameObject != null ? _input.GameObject.GetComponent<TMP_InputField>() : null;
                if (field != null) field.MoveTextEnd(false);
            }
            catch (Exception ex) { Report("set input", ex); }
        }

        private static void TabComplete()
        {
            string cur = _input.Text ?? string.Empty;
            cur = cur.Replace("\t", string.Empty);
            string next = Autocomplete.Complete(_reg.Names, cur, out var cands);
            if (cands.Count > 1) _reg.AddLines(ConsoleText.Color(CommandRegistry.DimColor, string.Join("  ", cands)));
            SetInput(next);
        }

        // ---- log mirroring ------------------------------------------------------------------------

        /// <summary>Turn mirroring of BepInEx log lines into the console on or off.</summary>
        public static void SetMirror(bool on)
        {
            try
            {
                // One permanent listener (added on first enable): adding / removing listeners while other threads log
                // races with the logging; flipping a volatile flag does not.
                if (on && _listener == null)
                {
                    _listener = new MirrorListener();
                    BepInEx.Logging.Logger.Listeners.Add(_listener);
                }
                _mirrorOn = on;
                if (!on)
                {
                    while (LogQueue.TryDequeue(out _)) { }
                    Interlocked.Exchange(ref _queued, 0);
                }
            }
            catch (Exception ex) { Report("log mirror", ex); }
            try { if (_mirrorToggle != null && _mirrorToggle.Value != on) _mirrorToggle.Value = on; } catch { }
        }

        /// <summary>Receives BepInEx log events on any thread; enqueues a formatted line (bounded) and never touches Unity.</summary>
        private sealed class MirrorListener : ILogListener
        {
            public void LogEvent(object sender, LogEventArgs e)
            {
                try
                {
                    if (!_mirrorOn) return;
                    string msg = e.Data?.ToString();
                    if (msg == null || msg.StartsWith("[console]", StringComparison.Ordinal)) return;   // our own prints: no feedback loop
                    // Bounded, drop-oldest: while the console is closed only a small tail is kept (it is drained on open).
                    int limit = _openFlag ? MaxQueued : MaxClosedQueued;
                    while (Volatile.Read(ref _queued) >= limit && LogQueue.TryDequeue(out _)) Interlocked.Decrement(ref _queued);
                    LogQueue.Enqueue("[" + e.Level + ":" + e.Source?.SourceName + "] " + msg);
                    Interlocked.Increment(ref _queued);
                }
                catch { /* a log listener must never throw */ }
            }

            public LogLevel LogLevelFilter => LogLevel.All;

            public void Dispose() { }
        }

        private static void Drain()
        {
            for (int n = 0; n < MaxDrainPerTick && LogQueue.TryDequeue(out var line); n++)
            {
                Interlocked.Decrement(ref _queued);
                _reg.AddLines(ConsoleText.Color(CommandRegistry.DimColor, ConsoleText.Literal(line)));
            }
        }

        // ---- per-frame ----------------------------------------------------------------------------

        private static void Tick()
        {
            var win = _win;
            if (win == null || !win.IsOpen) return;   // closed: mirrored lines wait in the small bounded queue
            if (_mirrorOn && !LogQueue.IsEmpty) Drain();

            try { TickOpen(); }
            catch (Exception ex) { Report("tick", ex); }
        }

        private static void TickOpen()
        {
            // keyboard, only while the input has focus (typing); polled, no allocation
            if (_input != null && _input.IsFocused)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
                if (_bind != null && _bind.Key != KeyCode.None && Input.GetKeyDown(_bind.Key) && _stripFrames == 0 && Time.frameCount > _openFrame) { Close(); return; }
                if (Input.GetKeyDown(KeyCode.UpArrow)) SetInput(History.Previous(_input.Text));
                else if (Input.GetKeyDown(KeyCode.DownArrow)) SetInput(History.Next(_input.Text));
                else if (Input.GetKeyDown(KeyCode.Tab)) TabComplete();

                if (_stripFrames > 0)
                {
                    _stripFrames--;
                    string t = _input.Text;
                    if (t.Length > 0 && t.IndexOfAny(OpenKeyChars) >= 0) SetInput(t.Trim(OpenKeyChars));
                }
            }

            // output text: rebuilt only when the buffer changed, at most every RebuildInterval
            float now = Time.unscaledTime;
            if ((_reg.Version != _renderedVersion || _forceRebuild) && (now - _lastRebuild >= RebuildInterval || _forceRebuild))
            {
                Rebuild();
                _lastRebuild = now;
                _forceRebuild = false;
            }

            if (_scrollFrames > 0 && --_scrollFrames == 0) ScrollToBottom();
        }

        private static readonly char[] OpenKeyChars = { '`', '~', 'ё', 'Ё', '§' };   // the BackQuote key types these on common layouts

        private static void Rebuild()
        {
            _renderedVersion = _reg.Version;
            _reg.Snapshot(Snap);
            bool atBottom = true;
            try
            {
                var rect = _scroll?.ScrollArea?.Rect;
                if (rect != null) atBottom = rect.verticalNormalizedPosition <= 0.02f;
            }
            catch { }

            int first = Snap.Count, total = 0;
            for (int i = Snap.Count - 1; i >= 0; i--)
            {
                total += Snap[i].Length + 1;
                if (total > MaxChars && first < Snap.Count) break;
                first = i;
            }
            Sb.Clear();
            for (int i = first; i < Snap.Count; i++) { if (i > first) Sb.Append('\n'); Sb.Append(Snap[i]); }
            _out.Text = Sb.ToString();
            if (atBottom) _scrollFrames = 3;     // wait for the layout to grow, then stick to the bottom
        }

        private static void ScrollToBottom()
        {
            try
            {
                var rect = _scroll?.ScrollArea?.Rect;
                if (rect != null) rect.verticalNormalizedPosition = 0f;
            }
            catch (Exception ex) { Report("scroll", ex); }
        }

        private static string _lastErr;
        private static void Report(string what, Exception ex)
        {
            string m = what + ex.GetType().Name + ex.Message;
            if (m == _lastErr) return;     // log each distinct failure once, never per frame
            _lastErr = m;
            try { _log.LogError($"Console {what} failed: {ex}"); } catch { }
        }
    }
}
