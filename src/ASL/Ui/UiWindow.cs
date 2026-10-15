using System;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// A toolkit window or HUD. Each window owns one overlay canvas (a child of the shared <c>ASL_UiCanvas</c>
    /// holder) so z-order is exactly the canvas sort order; the panel (rounded, bordered) is anchored on
    /// screen; a title bar (drag handle + close button) and a scrollable body hold the content. Everything
    /// interactive is driven by <see cref="Tick"/>, called by <see cref="UiRuntime"/> only while open.
    /// </summary>
    internal sealed class UiWindow : UiContainer, IUiWindow
    {
        public const float TitleH = 56f;

        public readonly string Key, LocalId;
        public readonly UiWindowOptions Opt;
        public readonly bool IsHud, IsDialog;
        public int FocusStamp, OpenedFrame;
        public Action EscapeAction, EnterAction;

        private string _title;
        private GameObject _canvasGo;
        private Canvas _canvas;
        private RectTransform _rootRt, _titleBar, _body;
        private TextMeshProUGUI _titleText;
        private UiButton _close;
        private bool _open;
        private bool _dragging;
        private Vector2 _dragOff;
        private int _clampFrames, _relayoutFrames;
        private IAslKeybind _boundKey;
        private Action _keyHandler;

        public event Action Opened;
        public event Action Closed;

        private UiWindow(UiHost host, string key, string localId, UiWindowOptions opt, bool hud, bool dialog) : base(host)
        {
            Key = key; LocalId = localId; Opt = opt; IsHud = hud; IsDialog = dialog;
        }

        internal static UiWindow Create(UiOwner owner, string key, string localId, string title, UiWindowOptions opt, UiTheme theme, bool hud, bool dialog)
        {
            var host = new UiHost(owner, theme);
            var w = new UiWindow(host, key, localId, opt, hud, dialog) { _title = title ?? string.Empty };
            host.Window = w;
            try { w.BuildVisuals(); }
            catch (Exception ex)
            {
                // Degrade instead of throwing into the mod: log once, drop the half-built objects and build a bare
                // window (placeholder label) the mod's builder code can keep adding to.
                owner.ReportBuildFailure("window '" + localId + "'", ex);
                w.DiscardPartial();
                w.BuildFallback();
            }
            return w;
        }

        // ---- IUiWindow -----------------------------------------------------------------------

        public string WindowId => LocalId;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; if (_titleText != null) _titleText.text = _title; }
        }

        public bool IsOpen => _open;

        public override bool Visible
        {
            get => _open;
            set { if (value) Open(); else Close(); }
        }

        public void Open()
        {
            if (_open || Dead) return;
            _open = true;
            try { _canvasGo.SetActive(true); } catch (Exception ex) { Owner.Report("window open", ex); }
            OpenedFrame = Time.frameCount;
            _relayoutFrames = 3;
            _clampFrames = 3;
            Host.Dirty = true;
            Host.Repoll = 3;
            UiRuntime.EnsureEventSystem();
            UiRuntime.WindowOpening(this);   // a window opened while the F8 menu is up would sit under it: hide the menu
            UiRuntime.WindowOpened(this);
            Relayout();
            try { Opened?.Invoke(); } catch (Exception ex) { Owner.Report("window Opened", ex); }
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _dragging = false;
            Host.IsHover = false;
            BlurInputs();
            if (UiPopup.Current != null && UiPopup.Current.Dropdown != null && UiPopup.Current.Dropdown.Host == Host) UiPopup.CloseCurrent();
            try { _canvasGo.SetActive(false); } catch { }
            UiRuntime.WindowClosed(this);
            try { Closed?.Invoke(); } catch (Exception ex) { Owner.Report("window Closed", ex); }
        }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void SetPosition(UiAnchor anchor, float offsetX = 0f, float offsetY = 0f)
        {
            Opt.Anchor = anchor; Opt.OffsetX = offsetX; Opt.OffsetY = offsetY;
            ApplyAnchor();
            _clampFrames = 2;
        }

        public void BindToggleKey(IAslKeybind key)
        {
            if (_boundKey != null && _keyHandler != null) { try { _boundKey.Pressed -= _keyHandler; } catch { } }
            _boundKey = key;
            if (key == null) return;
            if (_keyHandler == null) _keyHandler = () => { if (!Dead) Toggle(); };
            key.Pressed += _keyHandler;
        }

        public void BringToFront() => UiRuntime.BringToFront(this);

        public override void Destroy() => UiRuntime.DestroyWindow(this);

        // ---- teardown (called by UiRuntime.DestroyWindow) --------------------------------------

        internal void DisposeVisuals()
        {
            if (_boundKey != null && _keyHandler != null) { try { _boundKey.Pressed -= _keyHandler; } catch { } }
            _boundKey = null;
            MarkDead();
            Owner.Dialogs.Remove(this);
            if (_close != null) _close.MarkDead();
            try { if (_canvasGo != null) UnityEngine.Object.Destroy(_canvasGo); } catch { }
        }

        private void BlurInputs()
        {
            var list = Host.Pollers;
            for (int i = 0; i < list.Count; i++) if (list[i] is UiInput inp) inp.Blur();
        }

        // ---- construction ----------------------------------------------------------------------

        internal void SetSorting(int order)
        {
            try { _canvas.sortingOrder = order; } catch { }
        }

        private static void AnchorFractions(UiAnchor a, out float fx, out float fy)
        {
            switch (a)
            {
                case UiAnchor.TopLeft: fx = 0f; fy = 1f; break;
                case UiAnchor.Top: fx = 0.5f; fy = 1f; break;
                case UiAnchor.TopRight: fx = 1f; fy = 1f; break;
                case UiAnchor.Left: fx = 0f; fy = 0.5f; break;
                case UiAnchor.Right: fx = 1f; fy = 0.5f; break;
                case UiAnchor.BottomLeft: fx = 0f; fy = 0f; break;
                case UiAnchor.Bottom: fx = 0.5f; fy = 0f; break;
                case UiAnchor.BottomRight: fx = 1f; fy = 0f; break;
                default: fx = 0.5f; fy = 0.5f; break;
            }
        }

        private void ApplyAnchor()
        {
            AnchorFractions(Opt.Anchor, out float fx, out float fy);
            var v = new Vector2(fx, fy);
            Rt.anchorMin = v; Rt.anchorMax = v; Rt.pivot = v;
            Rt.anchoredPosition = new Vector2(Opt.OffsetX, Opt.OffsetY);
        }

        // Removes whatever a failed BuildVisuals left behind (objects, polled/tipped/scroll registrations, children).
        private void DiscardPartial()
        {
            try { if (_canvasGo != null) UnityEngine.Object.Destroy(_canvasGo); } catch { }
            _canvasGo = null; _canvas = null; _rootRt = null; _titleBar = null; _body = null; _titleText = null;
            if (_close != null) _close.MarkDead();
            _close = null;
            for (int i = 0; i < Host.Pollers.Count; i++) Host.Pollers[i].MarkDead();
            Host.Pollers.Clear(); Host.Tipped.Clear(); Host.Scrolls.Clear();
            Children.Clear();
            ScrollArea = null;
            Rt = null; Go = null;
        }

        // Minimal window: a canvas, a plain panel that is also the content column, and a placeholder label.
        private void BuildFallback()
        {
            var theme = Theme;
            _canvasGo = UiRuntime.NewCanvas("ASL_Win_" + Key, IsHud ? 32000 : (IsDialog ? 32700 : 32100));
            _canvas = _canvasGo.GetComponent<Canvas>();
            _rootRt = _canvasGo.GetComponent<RectTransform>();
            var panel = UiBuild.Rect("ASL_Window", _rootRt);
            Init(panel);
            UiBuild.Img(panel, null, theme.Background, !IsHud, false);
            UiBuild.VGroup(panel.gameObject, theme.Spacing, 12, 12, 12, 12, TextAnchor.UpperLeft);
            ContentRt = panel;
            Host.HoverRect = Rt;
            Rt.sizeDelta = new Vector2(Opt.Width, Opt.Height > 0f ? Opt.Height : 200f);
            ApplyAnchor();
            _canvasGo.SetActive(false);
            Add(new UiUnavailable(Host, ContentRt, "window"));
        }

        private void BuildVisuals()
        {
            var theme = Theme;
            int sort = IsHud ? 32000 + Mathf.Clamp(Opt.SortOrder, -90, 90) : (IsDialog ? 32700 : 32100);
            _canvasGo = UiRuntime.NewCanvas("ASL_Win_" + Key, sort);
            _canvas = _canvasGo.GetComponent<Canvas>();
            _rootRt = _canvasGo.GetComponent<RectTransform>();
            if (IsHud)
            {
                var cg = _canvasGo.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            if (Opt.Modal && !IsHud)
            {
                var bd = UiBuild.Rect("ASL_Backdrop", _rootRt);
                UiBuild.Stretch(bd);
                UiBuild.Img(bd, null, new Color(0f, 0f, 0f, IsDialog ? 0.6f : 0.5f), true, false);
            }

            // Panel = border sprite; Fill = inset background.
            var panel = UiBuild.Rect("ASL_Window", _rootRt);
            Init(panel);
            float r = Mathf.Min(theme.CornerRadius, 28f);
            var border = theme.PanelBorder;
            UiBuild.Img(panel, UiShapes.Rounded(r), IsHud ? UiThemeApplier.WithAlpha(border, 0.6f) : border, true, true);
            var bg = IsHud ? UiThemeApplier.WithAlpha(theme.Panel, 0.82f) : theme.Background;
            UiBuild.ImgChild("ASL_Fill", panel, UiShapes.Rounded(Mathf.Max(1f, r - 2f)), bg, true, true, out var fillRt);
            UiBuild.Stretch(fillRt, 2f, 2f, 2f, 2f);

            float titleH = Opt.ShowTitleBar && !IsHud ? TitleH : 0f;
            if (titleH > 0f)
            {
                var tb = UiBuild.ImgChild("ASL_TitleBar", panel, UiShapes.RoundedTop(Mathf.Max(1f, r - 2f)), theme.Header, true, true, out _titleBar);
                _titleBar.anchorMin = new Vector2(0f, 1f);
                _titleBar.anchorMax = new Vector2(1f, 1f);
                _titleBar.pivot = new Vector2(0.5f, 1f);
                _titleBar.sizeDelta = new Vector2(-4f, TitleH - 2f);
                _titleBar.anchoredPosition = new Vector2(0f, -2f);

                var trt = UiBuild.Rect("ASL_Title", _titleBar);
                UiBuild.Stretch(trt, 20f, 0f, Opt.Closable ? 64f : 20f, 0f);
                _titleText = UiBuild.Text(trt, theme, _title, theme.HeaderFontSize * 0.78f, theme.Text, TextAlignmentOptions.Left);
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.enableWordWrapping = false;
                _titleText.overflowMode = TextOverflowModes.Ellipsis;

                if (Opt.Closable)
                {
                    _close = new UiButton(Host, _titleBar, string.Empty, () => Close(),
                        new UiButtonStyle { Variant = UiVariant.Ghost, Shape = UiShape.Circle, Width = 38f, Height = 38f, Icon = UiShapes.Cross(), IconOnly = true });
                    var crt = _close.Rt;
                    crt.anchorMin = new Vector2(1f, 0.5f);
                    crt.anchorMax = new Vector2(1f, 0.5f);
                    crt.pivot = new Vector2(1f, 0.5f);
                    crt.sizeDelta = new Vector2(38f, 38f);
                    crt.anchoredPosition = new Vector2(-10f, 0f);
                }
            }

            // Body (below the title bar).
            _body = UiBuild.Rect("ASL_Body", panel);
            UiBuild.Stretch(_body, 2f, 2f, 2f, 2f + titleH);

            int pad = Mathf.RoundToInt(Opt.Padding >= 0f ? Opt.Padding : theme.Padding);
            if (Opt.Scrollable && !IsHud)
            {
                ScrollArea = UiScrollArea.Create(_body, Host, true, pad, pad, pad, pad, theme.Spacing, out _);
                ContentRt = ScrollArea.Content;
            }
            else
            {
                var content = UiBuild.Rect("ASL_Content", _body);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
                UiBuild.VGroup(content.gameObject, theme.Spacing, pad, pad, pad, pad, TextAnchor.UpperLeft);
                var fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ContentRt = content;
            }

            Host.HoverRect = Rt;
            if (Opt.RememberPosition && !IsHud && !IsDialog && UiWindowStore.TryGet(Key, Opt.Anchor, out float sx, out float sy))
            { Opt.OffsetX = sx; Opt.OffsetY = sy; }
            Rt.sizeDelta = new Vector2(Opt.Width, Opt.Height > 0f ? Opt.Height : 200f);
            ApplyAnchor();
            _canvasGo.SetActive(false);
        }

        // ---- layout ----------------------------------------------------------------------------

        /// <summary>Recomputes the window height: content height up to 85% of the screen (then it scrolls).</summary>
        internal void Relayout()
        {
            if (!_open || Dead) return;
            Host.Dirty = false;
            try
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(ContentRt);
                float ch = LayoutUtility.GetPreferredHeight(ContentRt);
                float titleH = Opt.ShowTitleBar && !IsHud ? TitleH : 0f;
                float screenH = _rootRt.rect.height;
                if (screenH < 100f) screenH = 1080f;
                float maxH = screenH * 0.85f;
                float total = Opt.Height > 0f ? Opt.Height : Mathf.Min(maxH, titleH + 4f + ch);
                total = Mathf.Max(total, titleH + 24f);
                Rt.sizeDelta = new Vector2(Opt.Width, total);
            }
            catch (Exception ex) { Owner.Report("window layout", ex); }
        }

        private void ClampToScreen()
        {
            try
            {
                float s = _canvas.scaleFactor;
                if (s <= 0f) return;
                var pos = Rt.position;
                var size = Rt.rect.size;
                var pv = Rt.pivot;
                float w = size.x * s, h = size.y * s;
                float minX = 80f - (1f - pv.x) * w, maxX = Screen.width - 80f + pv.x * w;
                float minY = 60f - (1f - pv.y) * h, maxY = Screen.height - (1f - pv.y) * h;
                float nx = Mathf.Clamp(pos.x, minX, Mathf.Max(minX, maxX));
                float ny = Mathf.Clamp(pos.y, minY, Mathf.Max(minY, maxY));
                if (nx != pos.x || ny != pos.y) Rt.position = new Vector3(nx, ny, pos.z);
            }
            catch { }
        }

        // ---- per-frame -------------------------------------------------------------------------

        /// <summary>Called by <see cref="UiRuntime"/> every frame while the window is open (not for HUDs).</summary>
        internal void Tick()
        {
            if (Dead || !_open) return;
            if (Host.Dirty || _relayoutFrames > 0) { Relayout(); if (_relayoutFrames > 0) _relayoutFrames--; }

            if (_clampFrames > 0) { _clampFrames--; if (!_dragging) ClampToScreen(); }

            if (_dragging)
            {
                if (UiRuntime.MouseHeld)
                {
                    Rt.position = new Vector3(UiRuntime.Mouse.x + _dragOff.x, UiRuntime.Mouse.y + _dragOff.y, 0f);
                    ClampToScreen();
                }
                else EndDrag();
            }
            else if (UiRuntime.MouseDown && Host.IsHover && Opt.Draggable && _titleBar != null
                     && RectTransformUtility.RectangleContainsScreenPoint(_titleBar, UiRuntime.Mouse, null)
                     && (_close == null || !RectTransformUtility.RectangleContainsScreenPoint(_close.Rt, UiRuntime.Mouse, null)))
            {
                _dragging = true;
                var p = Rt.position;
                _dragOff = new Vector2(p.x - UiRuntime.Mouse.x, p.y - UiRuntime.Mouse.y);
            }

            Host.PollAll();
        }

        /// <summary>HUDs: no hover / drag, just layout + live widgets.</summary>
        internal void TickHud()
        {
            if (Dead || !_open) return;
            if (Host.Dirty || _relayoutFrames > 0) { Relayout(); if (_relayoutFrames > 0) _relayoutFrames--; }
            Host.PollAll();
        }

        private void EndDrag()
        {
            _dragging = false;
            var ap = Rt.anchoredPosition;
            Opt.OffsetX = ap.x; Opt.OffsetY = ap.y;
            if (Opt.RememberPosition && !IsDialog) UiWindowStore.Set(Key, Opt.Anchor, ap.x, ap.y);
        }

        internal bool ContainsMouse() =>
            RectTransformUtility.RectangleContainsScreenPoint(Rt, UiRuntime.Mouse, null);

        internal void RunEscape()
        {
            if (EscapeAction != null) Owner.Fire(EscapeAction, "window escape");
            else Close();
        }
    }
}
