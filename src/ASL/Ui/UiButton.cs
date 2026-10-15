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
    /// A button of any shape/size/variant. Procedural shapes: a Button (transition None) over an Image with a
    /// generated sprite; hover/pressed tints are applied by polling (<see cref="UiHoverState"/>). The
    /// <see cref="UiShape.Native"/> shape clones the game's own menu button (listeners stripped like
    /// NativeMenu.AddRow). Clicks use <c>Button.onClick</c> with a <c>DelegateSupport</c>-converted Action —
    /// the verified IL2CPP pattern; no Unity event interfaces are implemented.
    /// </summary>
    internal sealed class UiButton : UiElement, IUiButton
    {
        private readonly RectTransform _parent;
        private UiButtonStyle _style;
        private string _text;
        private Sprite _icon;
        private readonly Action _onClick;
        private bool _native;
        private bool _polling;
        private bool _leftAlign;

        private Button _btn;
        private Image _bg;
        private TMP_Text _label;
        private Image _iconImg;
        private LayoutElement _iconLe;
        private UiHoverState _hs;
        private Color _fill, _textColor;
        private bool _ghost;
        private int _applied = -1;     // visual state last written: 0 normal, 1 hover, 2 pressed

        public event Action Clicked;

        public UiButton(UiHost host, RectTransform parent, string text, Action onClick, UiButtonStyle style) : base(host)
        {
            _parent = parent;
            _text = text ?? string.Empty;
            _onClick = onClick;
            _style = style != null ? style.Clone() : UiButtonStyle.Primary;
            _icon = _style.Icon;
            Build();
        }

        // ---- IUiButton -----------------------------------------------------------------------

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                if (_label != null) _label.text = _text;
                Host.MarkDirty();
            }
        }

        public void SetStyle(UiButtonStyle style)
        {
            if (style == null) return;
            _style = style.Clone();
            if (style.Icon != null) _icon = style.Icon;
            if (WantsNative() != _native) Rebuild(); else Apply();
        }

        public void SetIcon(Sprite icon)
        {
            _icon = icon;
            Apply();
        }

        /// <summary>Left-align the caption (dropdown list rows).</summary>
        internal void LeftAlign()
        {
            _leftAlign = true;
            Apply();
        }

        // ---- construction --------------------------------------------------------------------

        private bool WantsNative()
        {
            bool native = _style.Shape == UiShape.Native;
            if (!native && Theme.UseGameButtons && _style.Variant == UiVariant.Primary && _style.Shape == UiShape.Rounded) native = true;
            return native && UiRuntime.NativeTemplate != null;
        }

        private void Build()
        {
            _native = WantsNative();
            if (_native) BuildNative(); else BuildProcedural();
            Apply();
            FlexInRow = 0f;
            FlexInColumn = 1f;
        }

        private void Rebuild()
        {
            int sib = 0;
            try { sib = Rt.GetSiblingIndex(); } catch { }
            try { if (Go != null) UnityEngine.Object.Destroy(Go); } catch { }
            Build();
            try { Rt.SetSiblingIndex(sib); } catch { }
            Host.MarkDirty();
        }

        private void BuildNative()
        {
            var tpl = UiRuntime.NativeTemplate;
            var go = UnityEngine.Object.Instantiate(tpl, _parent);
            go.name = "ASL_NativeButton";
            go.SetActive(true);
            Init(go.GetComponent<RectTransform>());
            Rt.localScale = Vector3.one;
            UiUtil.SetLabel(go, _text);
            _label = go.GetComponentInChildren<TMP_Text>(true);
            var icon = Rt.Find("IconHolder");
            if (icon != null) icon.gameObject.SetActive(false);
            _btn = go.GetComponent<Button>();
            _bg = null; _iconImg = null; _iconLe = null;
            if (_btn != null)
            {
                var ev = _btn.onClick;
                int pc = ev.GetPersistentEventCount();
                for (int i = 0; i < pc; i++) ev.SetPersistentListenerState(i, UnityEventCallState.Off);
                ev.RemoveAllListeners();
                ev.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnClick)));
            }
        }

        private void BuildProcedural()
        {
            var rt = UiBuild.Rect("ASL_Button", _parent);
            Init(rt);
            _bg = UiBuild.Img(rt, null, Theme.Primary, true, false);
            UiBuild.HGroup(Go, 8f, 18, 18, 6, 6, TextAnchor.MiddleCenter);

            _btn = Go.AddComponent<Button>();
            _btn.transition = Selectable.Transition.None;
            _btn.targetGraphic = _bg;
            _btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnClick)));

            var iconRt = UiBuild.Rect("ASL_Icon", rt);
            _iconImg = UiBuild.Img(iconRt, null, Color.white, false, false);
            _iconImg.preserveAspect = true;
            _iconLe = UiBuild.Le(iconRt.gameObject);
            iconRt.gameObject.SetActive(false);

            var lrt = UiBuild.Rect("ASL_Label", rt);
            var tmp = UiBuild.Text(lrt, Theme, _text, Theme.FontSize, Theme.Text, TextAlignmentOptions.Center);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            _label = tmp;

            if (!_polling) { _polling = true; NeedPoll(); }
        }

        // ---- appearance ----------------------------------------------------------------------

        private void Apply()
        {
            float h = _style.ResolvedHeight;
            var le = Le;
            le.preferredHeight = h;
            le.minHeight = h;
            bool square = _style.Shape == UiShape.Circle || _style.Shape == UiShape.Square;
            float w = _style.Width > 0f ? _style.Width : (square ? h : -1f);
            le.preferredWidth = w;
            le.minWidth = w > 0f ? w : -1f;
            if (w > 0f) le.flexibleWidth = 0f;
            FlexLocked = w > 0f;

            if (_native)
            {
                if (_label != null && _style.FontSize > 0f) _label.fontSize = _style.FontSize;
                _applied = -1;
                Host.MarkDirty();
                return;
            }

            var theme = Theme;
            var hlg = Go.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                int padX = square ? 4 : (_leftAlign ? 14 : 18);
                hlg.padding = new RectOffset(padX, padX, 4, 4);
                hlg.childAlignment = _leftAlign ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            }
            var v = _style.Variant;
            _ghost = v == UiVariant.Ghost;
            _fill = _style.Background.HasValue ? _style.Background.Value : UiThemeApplier.VariantColor(theme, v);
            if (_style.TextColor.HasValue) _textColor = _style.TextColor.Value;
            else if (v == UiVariant.Ghost) _textColor = theme.Text;
            else if (v == UiVariant.Outline) _textColor = _fill;
            else _textColor = UiThemeApplier.ReadableOn(_fill);

            float radius = UiThemeApplier.Radius(theme, _style.CornerRadius);
            radius = Mathf.Min(radius, h * 0.5f);
            Sprite sprite = null;
            bool sliced = false;
            switch (_style.Shape)
            {
                case UiShape.Pill: radius = h * 0.5f; sprite = UiShapes.Rounded(radius); sliced = true; break;
                case UiShape.Circle: radius = h * 0.5f; sprite = UiShapes.Circle(); break;
                case UiShape.Rounded: sprite = UiShapes.Rounded(radius); sliced = true; break;
                default: break;   // Rect, Square: sharp corners
            }
            if (v == UiVariant.Outline)
            {
                float r = _style.Shape == UiShape.Circle ? h * 0.5f : (_style.Shape == UiShape.Pill ? h * 0.5f : Mathf.Max(1f, radius));
                sprite = UiShapes.Ring(r, 2f);
                sliced = true;
            }
            _bg.sprite = sprite;
            _bg.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            _bg.preserveAspect = false;

            var lbl = _label;
            lbl.color = _textColor;
            lbl.fontSize = _style.FontSize > 0f ? _style.FontSize : UiThemeApplier.FontSizeFor(theme, _style.Size);
            lbl.alignment = _leftAlign ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            lbl.text = _text;
            lbl.gameObject.SetActive(!_style.IconOnly);

            bool hasIcon = _icon != null;
            _iconImg.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                _iconImg.sprite = _icon;
                _iconImg.color = _textColor;
                float isz = Mathf.Max(12f, h * (_style.IconOnly ? 0.5f : 0.55f));
                _iconLe.preferredWidth = isz;
                _iconLe.preferredHeight = isz;
                _iconLe.minWidth = isz;
                _iconLe.minHeight = isz;
            }

            _applied = -1;
            WriteVisual(0);
            Host.MarkDirty();
        }

        private void WriteVisual(int state)
        {
            if (_native || _bg == null || state == _applied) return;
            _applied = state;
            Color c;
            if (_ghost)
            {
                float a = state == 0 ? 0f : (state == 1 ? 0.12f : 0.22f);
                c = UiThemeApplier.WithAlpha(_textColor, a);
            }
            else c = state == 0 ? _fill : (state == 1 ? UiThemeApplier.Hover(_fill) : UiThemeApplier.Press(_fill));
            _bg.color = c;
        }

        // ---- polling / events ----------------------------------------------------------------

        public override void Poll()
        {
            if (_native || _bg == null) return;
            if (_hs.Update(Host, Rt, Interactable))
                WriteVisual(_hs.Pressed ? 2 : (_hs.Hover ? 1 : 0));
        }

        private void OnClick()
        {
            if (Dead) return;
            Owner.Fire(_onClick, "button click");
            try { Clicked?.Invoke(); } catch (Exception ex) { Owner.Report("button Clicked", ex); }
        }
    }
}
