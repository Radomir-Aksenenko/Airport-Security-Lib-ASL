using System;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Hover tooltips (0.5 s), done by polling: when the pointer rests on a tooltip-bearing element of the
    /// hovered host the single shared tooltip panel (top-most canvas, never blocks clicks) is shown next to
    /// the pointer. Finding the target costs a few rect tests and only runs when the pointer moved.
    /// </summary>
    internal static class UiTooltips
    {
        private const float Delay = 0.5f;

        private static UiElement _target;
        private static float _timer;
        private static bool _shown;
        private static Vector2 _lastMouse;

        private static GameObject _go;
        private static RectTransform _panel;
        private static TextMeshProUGUI _text;
        private static LayoutElement _textLe;
        private static Image _bg, _ring;
        private static Canvas _canvas;

        public static void Tick(UiHost hover)
        {
            if (hover == null || hover.Tipped.Count == 0)
            {
                if (_shown || _target != null) Hide();
                return;
            }
            if (_target != null && _target.Dead) Hide();

            float dx = UiRuntime.Mouse.x - _lastMouse.x, dy = UiRuntime.Mouse.y - _lastMouse.y;
            bool moved = dx > 1.5f || dx < -1.5f || dy > 1.5f || dy < -1.5f;
            if (moved)
            {
                _lastMouse = UiRuntime.Mouse;
                var t = Find(hover);
                if (t != _target) { Hide(); _target = t; _timer = 0f; }
            }
            else if (_target != null && !_shown)
            {
                _timer += UiRuntime.Dt;
                if (_timer >= Delay) Show(_target, hover.Theme);
            }
            if (_shown) Follow();
        }

        private static UiElement Find(UiHost host)
        {
            var list = host.Tipped;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e == null || e.Dead) { list.RemoveAt(i); continue; }
                if (string.IsNullOrEmpty(e.Tooltip) || !e.IsActiveInHierarchy()) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(e.Rt, UiRuntime.Mouse, null)) return e;
            }
            return null;
        }

        private static void Create(UiTheme theme)
        {
            _go = UiRuntime.NewCanvas("ASL_Tooltip", 32766);
            _canvas = _go.GetComponent<Canvas>();
            var cg = _go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            _panel = UiBuild.Rect("ASL_TooltipPanel", _go.transform);
            _panel.anchorMin = Vector2.zero; _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0f, 1f);
            _bg = UiBuild.Img(_panel, UiShapes.Rounded(8f), theme.Panel, false, true);
            UiBuild.HGroup(_panel.gameObject, 0f, 12, 12, 8, 8, TextAnchor.MiddleLeft);
            var fit = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _ring = UiBuild.ImgChild("ASL_TooltipBorder", _panel, UiShapes.Ring(8f, 2f), theme.PanelBorder, false, true, out var rrt);
            UiBuild.Stretch(rrt);
            UiBuild.Le(rrt.gameObject).ignoreLayout = true;

            var trt = UiBuild.Rect("ASL_TooltipText", _panel);
            _text = UiBuild.Text(trt, theme, string.Empty, theme.FontSize * 0.88f, theme.Text, TextAlignmentOptions.Left);
            _textLe = UiBuild.Le(trt.gameObject);
        }

        private static void Show(UiElement target, UiTheme theme)
        {
            try
            {
                if (_go == null) Create(theme);
                _bg.color = theme.Panel;
                _ring.color = theme.PanelBorder;
                _text.color = theme.Text;
                _text.fontSize = theme.FontSize * 0.88f;
                string s = target.Tooltip;
                _text.text = s;
                float pw = _text.GetPreferredValues(s).x;
                _textLe.preferredWidth = Mathf.Min(460f, pw + 2f);
                _go.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
                _shown = true;
                Follow();
            }
            catch (Exception ex) { target.Host.Owner.Report("tooltip", ex); _timer = -9999f; }
        }

        private static void Follow()
        {
            try
            {
                float s = _canvas.scaleFactor;
                if (s <= 0f) s = 1f;
                var size = _panel.rect.size;
                float w = size.x * s, h = size.y * s;
                float x = UiRuntime.Mouse.x + 16f * s;
                float y = UiRuntime.Mouse.y - 22f * s;
                if (x + w > Screen.width) x = UiRuntime.Mouse.x - 12f * s - w;
                if (x < 0f) x = 0f;
                if (y - h < 0f) y = UiRuntime.Mouse.y + 24f * s + h;
                _panel.position = new Vector3(x, y, 0f);
            }
            catch { }
        }

        private static void Hide()
        {
            _target = null;
            _timer = 0f;
            if (!_shown) return;
            _shown = false;
            try { if (_go != null) _go.SetActive(false); } catch { }
        }
    }
}
