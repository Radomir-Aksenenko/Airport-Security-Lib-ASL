using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Il2CppInterop.Runtime;

namespace ASL
{
    /// <summary>
    /// The toolkit's single driver. <see cref="Tick"/> is called once per frame from
    /// <see cref="AslBehaviour.Update"/> (the only injected MonoBehaviour); it reads the pointer once, works
    /// out which host is under it, then polls ONLY open windows / visible HUDs and only the widgets that
    /// registered for polling. Also owns: the holder object + per-window overlay canvases, the z-order stack,
    /// Escape / Enter handling, the EventSystem, text-focus tracking and the cursor reasons.
    /// Idle cost (nothing open): one property check.
    /// </summary>
    internal static class UiRuntime
    {
        private const string HolderName = "ASL_UiCanvas";

        private static GameObject _holder;
        private static readonly List<UiWindow> Stack = new List<UiWindow>();
        private static readonly List<UiWindow> Huds = new List<UiWindow>();
        private static readonly Comparison<UiWindow> Cmp = CompareWindows;
        private static int _stamp;

        // ---- per-frame input snapshot (read once in Tick; widgets read these statics) ----
        public static Vector2 Mouse;
        public static bool MouseDown, MouseHeld, MouseUp, PointerChanged, EnterPressed, EscapePressed;
        /// <summary>Set when something already acted on this frame's Enter (a text field submitted): nothing else may also react to it.</summary>
        public static bool EnterConsumed;
        public static float Dt, Wheel;
        public static bool PointerActive = true;

        private static UiHost _hover;
        private static bool _hoverDirty = true, _menuWas;
        private static bool _focusNow, _focusPrev;
        private static string _lastTickError;
        private static float _lastTickErrorTime;
        private static bool _inputErrorLogged;
        private static float _nextEsCheck, _esRetryAt;

        /// <summary>The captured game menu-button prefab clone (shared with NativeMenu), for <c>UiShape.Native</c>.</summary>
        public static GameObject NativeTemplate { get; private set; }

        /// <summary>The F8 sub-page currently shown by NativeMenu (polled while the menu is visible).</summary>
        public static UiHost PageHost { get; set; }

        public static UiHost HoverHost => _hover;
        public static UiWindow HoverWindow { get; private set; }

        /// <summary>True while any toolkit text field has keyboard focus (with one frame of grace).</summary>
        public static bool IsTextInputFocused => _focusNow || _focusPrev;

        public static void SetNativeTemplate(GameObject template)
        {
            if (template == null || NativeTemplate != null) return;
            NativeTemplate = template;
            UiFonts.NoteTemplate(template);
        }

        public static void NoteTextFocused() { _focusNow = true; }

        // ---- canvases / event system -----------------------------------------------------------

        private static void EnsureHolder()
        {
            if (_holder != null) return;
            _holder = new GameObject(HolderName);
            UnityEngine.Object.DontDestroyOnLoad(_holder);
            // Before Ready (plugin Load: no scene, TMP settings or fonts yet) everything is built under a deactivated
            // holder: nothing renders, nothing ticks; SetReady() switches it on.
            if (!Ready) _holder.SetActive(false);
        }

        // ---- readiness gate ----------------------------------------------------------------------

        private const int ReadyFrames = 60;
        private static int _readyFrames;
        private static readonly List<UiWindow> DeferredOpens = new List<UiWindow>();
        private static readonly List<Action> DeferredWork = new List<Action>();

        /// <summary>
        /// False while the plugin is still loading (mods' <c>OnLoad</c> run before any scene, TMP settings or fonts
        /// exist). Windows / HUDs may be created and filled before then, but are built hidden and opened only once
        /// Ready; no EventSystem is created before. Becomes true on the first scene change or after 60 frames.
        /// </summary>
        public static bool Ready { get; private set; }

        /// <summary>Queues an <c>Open()</c> requested before Ready.</summary>
        public static void DeferOpen(UiWindow w) { if (!DeferredOpens.Contains(w)) DeferredOpens.Add(w); }

        /// <summary>Runs <paramref name="work"/> now when Ready, else once Ready (e.g. manifest <c>ui</c> loading).</summary>
        public static void RunWhenReady(Action work)
        {
            if (work == null) return;
            if (Ready) { work(); return; }
            DeferredWork.Add(work);
        }

        /// <summary>Flips the gate (idempotent): activates the holder, runs deferred work, then performs deferred opens.</summary>
        public static void SetReady()
        {
            if (Ready) return;
            Ready = true;
            try { if (_holder != null) _holder.SetActive(true); }
            catch (Exception ex) { AslPlugin.Logger?.LogWarning($"[ui] activating the UI holder failed: {ex.Message}"); }
            var work = DeferredWork.ToArray(); DeferredWork.Clear();
            for (int i = 0; i < work.Length; i++)
                try { work[i](); } catch (Exception ex) { AslPlugin.Logger?.LogError($"[ui] deferred UI work failed: {ex.Message}"); }
            var opens = DeferredOpens.ToArray(); DeferredOpens.Clear();
            for (int i = 0; i < opens.Length; i++)
                try { opens[i].OpenDeferred(); } catch (Exception ex) { opens[i].Host.Owner.Report("deferred window open", ex); }
        }

        /// <summary>A new full-screen overlay canvas (1920x1080 scaler, raycaster) under the shared holder; created active.</summary>
        public static GameObject NewCanvas(string name, int sortingOrder)
        {
            EnsureHolder();
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            go.transform.SetParent(_holder.transform, false);
            // A CanvasScaler only applies its scale when enabled (and from its Update): toggling the object once
            // everything is configured makes canvas.scaleFactor correct immediately instead of one frame late
            // (placement maths that read it right after creation would otherwise be off at non-1080p).
            go.SetActive(false);
            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// The canvas scale the 1920x1080 / match-0.5 scalers resolve to at the current screen size, computed from
        /// the screen alone (so it is right even before a fresh canvas has applied its scaler).
        /// </summary>
        public static float RefScale()
        {
            float w = Screen.width, h = Screen.height;
            if (w < 1f || h < 1f) return 1f;
            return Mathf.Sqrt((w / 1920f) * (h / 1080f));
        }

        /// <summary>
        /// Same logic as NativeMenu: make sure uGUI can receive clicks. Called on every window open and (throttled)
        /// while a toolkit host is open, so a scene change that destroyed the game's EventSystem is healed. If the
        /// input module cannot be created the half-made EventSystem is removed again and the attempt retried later
        /// (a module-less EventSystem would make <c>EventSystem.current</c> non-null and block every later fix).
        /// </summary>
        public static void EnsureEventSystem(bool menu = false)
        {
            // Never before Ready (a boot-time EventSystem would shadow the game's own), and only for hosts that need
            // raycasts: HUDs and toasts are click-through. The F8 menu passes menu=true.
            if (!Ready) return;
            if (!menu && !NeedsEventSystem()) return;
            GameObject es = null;
            try
            {
                if (EventSystem.current != null) return;
                float now = Time.realtimeSinceStartup;
                if (now < _esRetryAt) return;
                es = new GameObject("ASL_EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                _ourEs = es;
                _nextEsWatch = now + 2f;
                AslPlugin.Logger?.LogInfo("[ui] created an EventSystem (none was present).");
            }
            catch (Exception ex)
            {
                try { if (es != null) UnityEngine.Object.Destroy(es); } catch { }
                _ourEs = null;
                _esRetryAt = Time.realtimeSinceStartup + 30f;
                AslPlugin.Logger?.LogWarning($"[ui] EventSystem check failed: {ex.Message} (retrying in 30 s)");
            }
        }

        private static bool NeedsEventSystem() => Stack.Count > 0 || PageHost != null || UiPopup.Current != null;

        private static GameObject _ourEs;
        private static float _nextEsWatch;

        // If the game later brings its own EventSystem, ours must go (two would fight over input; the game's wins).
        private static void WatchEventSystem()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextEsWatch) return;
            _nextEsWatch = now + 2f;
            try
            {
                if (_ourEs == null) return;
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<EventSystem>());
                if (all == null) return;
                for (int i = 0; i < all.Length; i++)
                {
                    var e = all[i] != null ? all[i].TryCast<EventSystem>() : null;
                    if (e == null || e.gameObject == _ourEs) continue;
                    UnityEngine.Object.Destroy(_ourEs);
                    _ourEs = null;
                    AslPlugin.Logger?.LogInfo("[ui] the game created its own EventSystem; removed ours.");
                    return;
                }
            }
            catch (Exception ex) { _nextEsWatch = now + 30f; AslPlugin.Logger?.LogWarning($"[ui] EventSystem watch failed: {ex.Message}"); }
        }

        public static bool IsOurs(Transform t)
        {
            try
            {
                var root = t != null ? t.root : null;
                var n = root != null ? root.name : null;
                return n != null && n.StartsWith("ASL_", StringComparison.Ordinal);
            }
            catch { return false; }
        }

        // ---- window registry -------------------------------------------------------------------

        /// <summary>Open (non-HUD) windows, bottom to top; read-only view for the console <c>windows</c> command.</summary>
        public static IReadOnlyList<UiWindow> OpenWindows => Stack;

        /// <summary>Open HUD overlays; read-only view for the console <c>windows</c> command.</summary>
        public static IReadOnlyList<UiWindow> OpenHuds => Huds;

        /// <summary>
        /// Called as a (non-HUD) window opens. The F8 menu canvas (32760) is above every window layer, so a window
        /// or dialog that opens while the menu is up would be hidden behind it (and not ticked): close the menu
        /// first. Dropdown popups are not windows (they live above the menu at 32763) and are unaffected.
        /// </summary>
        public static void WindowOpening(UiWindow w)
        {
            if (w.IsHud) return;
            try
            {
                var menu = AslPlugin.Menu;
                if (menu != null && menu.Visible) menu.Visible = false;
            }
            catch (Exception ex) { AslPlugin.Logger?.LogWarning($"[ui] could not close the F8 menu for a window: {ex.Message}"); }
        }

        public static void WindowOpened(UiWindow w)
        {
            if (w.IsHud) { if (!Huds.Contains(w)) Huds.Add(w); }
            else
            {
                if (!Stack.Contains(w)) Stack.Add(w);
                w.FocusStamp = ++_stamp;
                Restack();
            }
            _hoverDirty = true;
            RecomputeCursor();
        }

        public static void WindowClosed(UiWindow w)
        {
            Huds.Remove(w);
            Stack.Remove(w);
            if (_hover == w.Host) SetHover(null);
            if (HoverWindow == w) HoverWindow = null;
            _hoverDirty = true;
            UiTooltips.Reset();   // the tooltip may belong to this window; the idle branch of Tick never runs for it
            RecomputeCursor();
        }

        /// <summary>The pointer-over state must be re-evaluated next tick (a popup or window appeared / vanished).</summary>
        public static void MarkHoverDirty() { _hoverDirty = true; }

        public static void DestroyWindow(UiWindow w)
        {
            if (w == null || w.Dead) return;
            w.Close();
            Huds.Remove(w);
            Stack.Remove(w);
            w.DisposeVisuals();
            UiTooltips.Reset();
            _hoverDirty = true;
            RecomputeCursor();   // never leave a cursor / input-block reason behind for a destroyed window
        }

        public static void BringToFront(UiWindow w)
        {
            if (w == null || !w.IsOpen || w.IsHud) return;
            if (Stack.Count > 0 && Stack[Stack.Count - 1] == w) return;
            w.FocusStamp = ++_stamp;
            Restack();
            _hoverDirty = true;
        }

        private static int CompareWindows(UiWindow a, UiWindow b)
        {
            int c = a.IsDialog.CompareTo(b.IsDialog);
            if (c != 0) return c;
            c = a.Opt.Modal.CompareTo(b.Opt.Modal);
            if (c != 0) return c;
            c = a.Opt.SortOrder.CompareTo(b.Opt.SortOrder);
            if (c != 0) return c;
            return a.FocusStamp.CompareTo(b.FocusStamp);
        }

        private static void Restack()
        {
            Stack.Sort(Cmp);
            int nWin = 0, nDlg = 0;
            for (int i = 0; i < Stack.Count; i++)
            {
                var w = Stack[i];
                if (w.IsDialog) w.SetSorting(32700 + Mathf.Min(nDlg++, 40));
                else w.SetSorting(32100 + Mathf.Min(nWin++, 580));
            }
        }

        private static void RecomputeCursor()
        {
            bool cursor = false, block = false;
            for (int i = 0; i < Stack.Count; i++)
            {
                if (Stack[i].Opt.CaptureCursor) cursor = true;
                if (Stack[i].Opt.BlockGameInput) block = true;
            }
            CursorManager.SetWindows(cursor, block);
        }

        // ---- the tick --------------------------------------------------------------------------

        private static bool IsActive() =>
            Stack.Count > 0 || Huds.Count > 0 || PageHost != null || UiToasts.Count > 0 || UiPopup.Current != null || UiFonts.NeedsTick;

        /// <summary>Called once per frame from <see cref="AslBehaviour"/>.</summary>
        public static void Tick()
        {
            if (!Ready)
            {
                if (++_readyFrames < ReadyFrames) return;
                SetReady();
            }
            if (_ourEs != null) WatchEventSystem();
            if (!IsActive())
            {
                if (_focusNow || _focusPrev) { _focusNow = false; _focusPrev = false; CursorManager.SetTextInput(false); }
                UiTooltips.Reset();   // nothing is open any more: no tooltip may outlive its window
                return;
            }
            try { TickCore(); }
            catch (Exception ex)
            {
                string msg = ex.GetType().Name + ": " + ex.Message;
                float now = Time.realtimeSinceStartup;
                if (msg != _lastTickError || now - _lastTickErrorTime > 5f)
                    AslPlugin.Logger?.LogError($"[ui] tick failed: {ex}");
                _lastTickError = msg; _lastTickErrorTime = now;
            }
        }

        private static bool _inputLive;

        private static void ReadInput()
        {
            _inputLive = true;
            Dt = Time.unscaledDeltaTime;
            EnterConsumed = false;
            try
            {
                var mp = Input.mousePosition;
                float dx = mp.x - Mouse.x, dy = mp.y - Mouse.y;
                Mouse = new Vector2(mp.x, mp.y);
                MouseDown = Input.GetMouseButtonDown(0);
                MouseHeld = Input.GetMouseButton(0);
                MouseUp = Input.GetMouseButtonUp(0);
                Wheel = Input.mouseScrollDelta.y;
                EnterPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                EscapePressed = Input.GetKeyDown(KeyCode.Escape);
                PointerChanged = dx != 0f || dy != 0f || MouseDown || MouseUp || Wheel != 0f;
            }
            catch (Exception ex)
            {
                // A failing input read must not take the whole toolkit down every frame: log once, report "no input".
                MouseDown = MouseHeld = MouseUp = EnterPressed = EscapePressed = PointerChanged = false;
                Wheel = 0f;
                if (!_inputErrorLogged)
                {
                    _inputErrorLogged = true;
                    AslPlugin.Logger?.LogError($"[ui] reading input failed ({ex.GetType().Name}: {ex.Message}); the toolkit keeps running without input until it recovers.");
                }
            }
        }

        private static void TickCore()
        {
            UiFonts.Tick(Time.realtimeSinceStartup);
            if (Stack.Count == 0 && PageHost == null && UiPopup.Current == null) { TickPassive(); return; }
            ReadInput();
            bool menuVisible = AslPlugin.Menu != null && AslPlugin.Menu.Visible;
            if (menuVisible != _menuWas) { _menuWas = menuVisible; _hoverDirty = true; }

            _focusNow = false;
            UpdateHover(menuVisible);

            // Clicking a window raises it.
            if (MouseDown && HoverWindow != null) BringToFront(HoverWindow);

            // Mouse wheel (polled; the ScrollRects' own wheel handling is disabled).
            if (Wheel != 0f && _hover != null) _hover.ScrollAt(Mouse, Wheel);

            // Widgets.
            // One window's failure must not stop the others from ticking.
            TickHuds();
            if (!menuVisible)
            {
                for (int i = Stack.Count - 1; i >= 0; i--)
                {
                    if (i >= Stack.Count) continue;
                    var w = Stack[i];
                    try { w.Tick(); } catch (Exception ex) { w.Host.Owner.Report("window tick", ex); }
                }
            }
            else if (PageHost != null)
            {
                try { PageHost.PollAll(); } catch (Exception ex) { PageHost.Owner.Report("page tick", ex); }
            }

            var pop = UiPopup.Current;
            if (pop != null)
            {
                try { pop.Host.PollAll(); pop.Tick(); } catch (Exception ex) { pop.Host.Owner.Report("popup tick", ex); }
            }

            // Heal a missing EventSystem (a scene change can destroy the game's) while any interactive toolkit UI is up.
            float nowRt = Time.realtimeSinceStartup;
            if (nowRt >= _nextEsCheck)
            {
                _nextEsCheck = nowRt + 1f;
                if (Stack.Count > 0 || PageHost != null || UiPopup.Current != null) EnsureEventSystem();
            }

            // Keyboard: Escape closes the popup / top window, Enter confirms the top dialog. Not while typing in a text
            // field (this frame or last: TMP consumes Escape/Enter before we run) or rebinding a key - with one
            // exception: Escape in a dialog's own text field (Prompt) cancels the dialog in a single press.
            //
            // Caveat: toolkit code cannot consume input, so the game's own Escape handling (pause menu) may react to
            // the same key press that closes a window. To keep that to a minimum we only act on Escape when the
            // top-most open toolkit window opted in (CloseOnEscape) and is closable - never for HUDs, never for
            // windows beneath it, and never while the F8 menu is up (its behaviour is unchanged).
            bool typing = _focusNow || _focusPrev;
            bool rebinding = AslPlugin.Keybinds != null && AslPlugin.Keybinds.IsRebinding;
            if (!rebinding)
            {
                if (EscapePressed)
                {
                    if (UiPopup.Current != null) UiPopup.CloseCurrent();
                    else if (!menuVisible)
                    {
                        if (!typing) CloseTopOnEscape();
                        else CloseTopDialogOnEscape();
                    }
                }
                else if (EnterPressed && !typing && !menuVisible && !EnterConsumed) ConfirmTopDialog();
            }

            if (MouseUp) DeselectNonInput();

            UiTooltips.Tick(UiPopup.Current != null ? UiPopup.Current.Host : _hover);
            UiToasts.Tick(Dt);

            CursorManager.SetTextInput(_focusNow);
            _focusPrev = _focusNow;
        }

        private static void TickHuds()
        {
            for (int i = Huds.Count - 1; i >= 0; i--)
            {
                if (i >= Huds.Count) continue;
                var h = Huds[i];
                try { h.TickHud(); } catch (Exception ex) { h.Host.Owner.Report("hud tick", ex); }
            }
        }

        // Only click-through HUDs / toasts are up (no window, F8 page or popup): no pointer or keyboard work, just
        // the live widgets and toast timing. Input flags are cleared once so nothing reads a stale click later.
        private static void TickPassive()
        {
            Dt = Time.unscaledDeltaTime;
            EnterConsumed = false;
            if (_inputLive)
            {
                _inputLive = false;
                MouseDown = MouseHeld = MouseUp = PointerChanged = EnterPressed = EscapePressed = false;
                Wheel = 0f;
            }
            if (_hover != null) SetHover(null);
            HoverWindow = null;
            if (_focusNow || _focusPrev) { _focusNow = false; _focusPrev = false; CursorManager.SetTextInput(false); }
            TickHuds();
            UiTooltips.Reset();
            UiToasts.Tick(Dt);
        }

        private static void UpdateHover(bool menuVisible)
        {
            if (!PointerChanged && !_hoverDirty) return;
            _hoverDirty = false;
            UiHost host = null;
            UiWindow win = null;
            var pop = UiPopup.Current;
            if (pop != null) host = pop.PanelContainsMouse() ? pop.Host : null;
            else if (menuVisible)
            {
                var ph = PageHost;
                if (ph != null && ph.HoverRect != null
                    && RectTransformUtility.RectangleContainsScreenPoint(ph.HoverRect, Mouse, null)) host = ph;
            }
            else
            {
                for (int i = Stack.Count - 1; i >= 0; i--)
                {
                    var w = Stack[i];
                    if (w.ContainsMouse()) { host = w.Host; win = w; break; }
                    if (w.Opt.Modal) break;
                }
            }
            SetHover(host);
            HoverWindow = win;
        }

        private static void SetHover(UiHost h)
        {
            if (_hover == h) return;
            if (_hover != null) { _hover.IsHover = false; _hover.Repoll = 2; }
            _hover = h;
            if (h != null) { h.IsHover = true; h.Repoll = 2; }
        }

        // Only the top-most window may react: if it does not want / cannot be closed by Escape, stop - never close
        // the window beneath it (that one is hidden behind a window the user did not dismiss).
        private static void CloseTopOnEscape()
        {
            if (Stack.Count == 0) return;
            var w = Stack[Stack.Count - 1];
            if (w.Opt.CloseOnEscape && (w.Opt.Closable || w.IsDialog)) w.RunEscape();
        }

        // Escape while a text field is focused: only a dialog (Prompt) reacts, so one press cancels it.
        private static void CloseTopDialogOnEscape()
        {
            if (Stack.Count == 0) return;
            var w = Stack[Stack.Count - 1];
            if (w.IsDialog && w.Opt.CloseOnEscape && w.EscapeAction != null && Time.frameCount > w.OpenedFrame) w.RunEscape();
        }

        private static void ConfirmTopDialog()
        {
            if (Stack.Count == 0) return;
            var w = Stack[Stack.Count - 1];
            if (w.IsDialog && w.EnterAction != null && Time.frameCount > w.OpenedFrame)
            {
                EnterConsumed = true;   // one Enter confirms one dialog, even if its callback opens / closes another
                w.Host.Owner.Fire(w.EnterAction, "dialog confirm");
            }
        }

        // A clicked slider / button stays "selected" in the EventSystem, so Space/Enter/arrows would re-trigger
        // it. After every click release, drop the selection unless it is a text field (or not ours).
        private static void DeselectNonInput()
        {
            try
            {
                var es = EventSystem.current;
                if (es == null) return;
                var sel = es.currentSelectedGameObject;
                if (sel == null || sel.GetComponent<TMP_InputField>() != null) return;
                if (!IsOurs(sel.transform)) return;
                es.SetSelectedGameObject(null);
            }
            catch { }
        }
    }
}
