using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
        public static float Dt, Wheel;
        public static bool PointerActive = true;

        private static UiHost _hover;
        private static bool _hoverDirty = true, _menuWas;
        private static bool _focusNow, _focusPrev;
        private static string _lastTickError;
        private static float _lastTickErrorTime;

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
            return go;
        }

        /// <summary>Same logic as NativeMenu: make sure uGUI can receive clicks.</summary>
        public static void EnsureEventSystem()
        {
            try
            {
                if (EventSystem.current != null) return;
                var es = new GameObject("ASL_EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                AslPlugin.Logger?.LogInfo("[ui] created an EventSystem (none was present).");
            }
            catch (Exception ex) { AslPlugin.Logger?.LogWarning($"[ui] EventSystem check failed: {ex.Message}"); }
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
            RecomputeCursor();
        }

        public static void DestroyWindow(UiWindow w)
        {
            if (w == null || w.Dead) return;
            w.Close();
            Huds.Remove(w);
            Stack.Remove(w);
            w.DisposeVisuals();
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
            if (!IsActive())
            {
                if (_focusNow || _focusPrev) { _focusNow = false; _focusPrev = false; CursorManager.SetTextInput(false); }
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

        private static void ReadInput()
        {
            Dt = Time.unscaledDeltaTime;
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

        private static void TickCore()
        {
            UiFonts.Tick(Time.realtimeSinceStartup);
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
            for (int i = Huds.Count - 1; i >= 0; i--) { if (i < Huds.Count) Huds[i].TickHud(); }
            if (!menuVisible)
                for (int i = Stack.Count - 1; i >= 0; i--) { if (i < Stack.Count) Stack[i].Tick(); }
            else if (PageHost != null) PageHost.PollAll();

            var pop = UiPopup.Current;
            if (pop != null) { pop.Host.PollAll(); pop.Tick(); }

            // Keyboard: Escape closes the popup / top window, Enter confirms the top dialog. Never while
            // typing in a text field (this frame or last: TMP consumes Escape/Enter before we run) or rebinding a key.
            bool typing = _focusNow || _focusPrev;
            bool rebinding = AslPlugin.Keybinds != null && AslPlugin.Keybinds.IsRebinding;
            if (!typing && !rebinding)
            {
                if (EscapePressed)
                {
                    if (UiPopup.Current != null) UiPopup.CloseCurrent();
                    else if (!menuVisible) CloseTopOnEscape();
                }
                else if (EnterPressed && !menuVisible) ConfirmTopDialog();
            }

            if (MouseUp) DeselectNonInput();

            UiTooltips.Tick(UiPopup.Current != null ? UiPopup.Current.Host : _hover);
            UiToasts.Tick(Dt);

            CursorManager.SetTextInput(_focusNow);
            _focusPrev = _focusNow;
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

        private static void CloseTopOnEscape()
        {
            for (int i = Stack.Count - 1; i >= 0; i--)
            {
                var w = Stack[i];
                if (w.Opt.CloseOnEscape && (w.Opt.Closable || w.IsDialog)) { w.RunEscape(); return; }
                if (w.Opt.Modal) return;
            }
        }

        private static void ConfirmTopDialog()
        {
            if (Stack.Count == 0) return;
            var w = Stack[Stack.Count - 1];
            if (w.IsDialog && w.EnterAction != null && Time.frameCount > w.OpenedFrame)
                w.Host.Owner.Fire(w.EnterAction, "dialog confirm");
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
