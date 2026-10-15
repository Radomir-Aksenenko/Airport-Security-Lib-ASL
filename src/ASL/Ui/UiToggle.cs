using System;
using ASL.Api;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// On/off toggle in three looks (Switch, Checkbox, Button). Built from a plain <c>Button</c> that flips a
    /// managed bool (no Unity Toggle, no UnityEvent&lt;bool&gt; listener); the knob slide / colour fade is a tiny
    /// polled animation that only does work while it is running.
    /// </summary>
    internal sealed class UiToggle : UiElement, IUiToggle
    {
        private const float TrackW = 56f, TrackH = 30f, KnobD = 24f, AnimSeconds = 0.12f;

        private readonly UiToggleStyle _style;
        private readonly Action<bool> _onChanged;
        private bool _value;
        private float _anim;                  // 0 = off .. 1 = on (current visual)
        private UiHoverState _hs;
        private int _hoverApplied = -1;

        private Image _root;                  // hover background (switch/checkbox) or the button body
        private TextMeshProUGUI _label;
        private Image _track, _knob, _boxRing, _check;
        private RectTransform _knobRt;
        private string _text;

        public event Action<bool> Changed;

        public UiToggle(UiHost host, RectTransform parent, string label, bool initial, Action<bool> onChanged, UiToggleStyle style) : base(host)
        {
            _style = style;
            _onChanged = onChanged;
            _value = initial;
            _anim = initial ? 1f : 0f;
            _text = label ?? string.Empty;
            Build(parent);
            ApplyVisual();
            NeedPoll();
        }

        public bool Value
        {
            get => _value;
            set { _value = value; _anim = value ? 1f : 0f; ApplyVisual(); }
        }

        public string Label
        {
            get => _text;
            set
            {
                value = value ?? string.Empty;
                if (value == _text) return;
                string old = _text;
                _text = value;
                _label.text = _text;
                // Switch / checkbox captions fill the row (one line either way); only the button look sizes
                // to its text, and a long or multi-line caption may wrap to another line.
                if (_style == UiToggleStyle.Button || old.Length > 30 || value.Length > 30
                    || old.IndexOf('\n') >= 0 || value.IndexOf('\n') >= 0) Host.MarkDirty();
            }
        }

        private void Build(RectTransform parent)
        {
            var theme = Theme;
            var rt = UiBuild.Rect("ASL_Toggle", parent);
            Init(rt);
            bool buttonStyle = _style == UiToggleStyle.Button;
            _root = UiBuild.Img(rt, UiShapes.Rounded(buttonStyle ? theme.CornerRadius : 8f), UiThemeApplier.Clear, true, true);
            var btn = Go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = _root;
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnClick)));

            var le = Le;
            le.preferredHeight = buttonStyle ? 44f : 40f;
            le.minHeight = le.preferredHeight;

            if (buttonStyle)
            {
                UiBuild.HGroup(Go, 8f, 18, 18, 6, 6, TextAnchor.MiddleCenter);
                var lrt = UiBuild.Rect("ASL_Label", rt);
                _label = UiBuild.Text(lrt, theme, _text, theme.FontSize, theme.Text, TextAlignmentOptions.Center);
                _label.enableWordWrapping = false;
                return;
            }

            UiBuild.HGroup(Go, 14f, 8, 8, 4, 4, TextAnchor.MiddleLeft);
            var labelRt = UiBuild.Rect("ASL_Label", rt);
            _label = UiBuild.Text(labelRt, theme, _text, theme.FontSize, theme.Text, TextAlignmentOptions.Left);
            UiBuild.Le(labelRt.gameObject).flexibleWidth = 1f;

            if (_style == UiToggleStyle.Switch)
            {
                var trackRt = UiBuild.Rect("ASL_Track", rt);
                _track = UiBuild.Img(trackRt, UiShapes.Rounded(TrackH * 0.5f), theme.Secondary, false, true);
                var tle = UiBuild.Le(trackRt.gameObject);
                tle.preferredWidth = TrackW; tle.minWidth = TrackW; tle.preferredHeight = TrackH; tle.minHeight = TrackH;
                _knob = UiBuild.ImgChild("ASL_Knob", trackRt, UiShapes.Circle(), Color.white, false, false, out _knobRt);
                _knobRt.anchorMin = new Vector2(0f, 0.5f);
                _knobRt.anchorMax = new Vector2(0f, 0.5f);
                _knobRt.pivot = new Vector2(0.5f, 0.5f);
                _knobRt.sizeDelta = new Vector2(KnobD, KnobD);
            }
            else
            {
                var boxRt = UiBuild.Rect("ASL_Box", rt);
                _track = UiBuild.Img(boxRt, UiShapes.Rounded(7f), theme.InputBackground, false, true);
                var ble = UiBuild.Le(boxRt.gameObject);
                ble.preferredWidth = 30f; ble.minWidth = 30f; ble.preferredHeight = 30f; ble.minHeight = 30f;
                _boxRing = UiBuild.ImgChild("ASL_Ring", boxRt, UiShapes.Ring(7f, 2f), theme.PanelBorder, false, true, out var ringRt);
                UiBuild.Stretch(ringRt);
                _check = UiBuild.ImgChild("ASL_Check", boxRt, UiShapes.Rounded(4f), theme.Primary, false, true, out var chkRt);
                UiBuild.Stretch(chkRt, 7f, 7f, 7f, 7f);
            }
        }

        private void ApplyVisual()
        {
            var theme = Theme;
            float t = _anim;
            switch (_style)
            {
                case UiToggleStyle.Switch:
                    _track.color = UiThemeApplier.Mix(theme.Secondary, theme.Primary, t);
                    _knobRt.anchoredPosition = new Vector2(Mathf.Lerp(3f + KnobD * 0.5f, TrackW - 3f - KnobD * 0.5f, t), 0f);
                    break;
                case UiToggleStyle.Checkbox:
                    _check.gameObject.SetActive(t > 0.5f);
                    _boxRing.color = t > 0.5f ? theme.Primary : theme.PanelBorder;
                    break;
                default:   // Button
                    WriteButtonBody();
                    break;
            }
        }

        private void WriteButtonBody()
        {
            var theme = Theme;
            var baseCol = UiThemeApplier.Mix(theme.Secondary, theme.Primary, _anim);
            var col = _hs.Pressed ? UiThemeApplier.Press(baseCol) : (_hs.Hover ? UiThemeApplier.Hover(baseCol) : baseCol);
            _root.color = col;
            _label.color = UiThemeApplier.ReadableOn(baseCol);
        }

        private void WriteHover()
        {
            int state = _hs.Pressed ? 2 : (_hs.Hover ? 1 : 0);
            if (state == _hoverApplied) return;
            _hoverApplied = state;
            if (_style == UiToggleStyle.Button) { WriteButtonBody(); return; }
            _root.color = state == 0 ? UiThemeApplier.Clear : UiThemeApplier.WithAlpha(Theme.Text, state == 1 ? 0.07f : 0.12f);
        }

        public override void Poll()
        {
            if (_hs.Update(Host, this, Rt, Interactable)) WriteHover();

            float target = _value ? 1f : 0f;
            if (_anim != target)
            {
                float step = UiRuntime.Dt / AnimSeconds;
                _anim = _anim < target ? Mathf.Min(target, _anim + step) : Mathf.Max(target, _anim - step);
                ApplyVisual();
            }
        }

        private void OnClick()
        {
            if (Dead) return;
            _value = !_value;
            if (_style == UiToggleStyle.Button) { _anim = _value ? 1f : 0f; ApplyVisual(); }
            Owner.Fire(_onChanged, _value, "toggle changed");
            try { Changed?.Invoke(_value); } catch (Exception ex) { Owner.Report("toggle Changed", ex); }
        }
    }
}
