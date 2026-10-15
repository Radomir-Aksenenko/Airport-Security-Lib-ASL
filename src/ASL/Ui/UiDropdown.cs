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
    /// Drop-down list. NOT a TMP_Dropdown: a field button shows the current option; clicking it opens a
    /// custom popup (<see cref="UiPopup"/>) of toolkit buttons on the top-most layer, so it is never clipped
    /// by scroll views and needs no Unity template objects.
    /// </summary>
    internal sealed class UiDropdown : UiElement, IUiDropdown
    {
        private readonly Action<int> _onChanged;
        private string[] _options;
        private int _index;
        private TextMeshProUGUI _valueText;
        private Image _field;
        private RectTransform _fieldRt;
        private UiHoverState _hs;
        private int _applied = -1;

        public event Action<int> Changed;

        public UiDropdown(UiHost host, RectTransform parent, string label, string[] options, int initial, Action<int> onChanged) : base(host)
        {
            _onChanged = onChanged;
            _options = options ?? new string[0];
            _index = _options.Length == 0 ? -1 : Mathf.Clamp(initial, 0, _options.Length - 1);
            Build(parent, label);
            ShowValue();
            NeedPoll();
            FlexInRow = 1f;
            FlexInColumn = 1f;
        }

        public int Index
        {
            get => _index;
            set
            {
                _index = _options.Length == 0 ? -1 : Mathf.Clamp(value, 0, _options.Length - 1);
                ShowValue();
            }
        }

        public string Value => _index >= 0 && _index < _options.Length ? _options[_index] : null;

        public void SetOptions(string[] options)
        {
            _options = options ?? new string[0];
            if (_index >= _options.Length) _index = _options.Length - 1;
            if (_index < 0 && _options.Length > 0) _index = 0;
            ShowValue();
        }

        internal string[] Options => _options;
        internal RectTransform FieldRt => _fieldRt;

        private void Build(RectTransform parent, string label)
        {
            var theme = Theme;
            var rt = UiBuild.Rect("ASL_Dropdown", parent);
            Init(rt);
            UiBuild.HGroup(Go, 14f, 8, 8, 2, 2, TextAnchor.MiddleLeft);
            var le = Le;
            le.preferredHeight = 44f; le.minHeight = 44f;

            if (!string.IsNullOrEmpty(label))
            {
                var lrt = UiBuild.Rect("ASL_Label", rt);
                var lt = UiBuild.Text(lrt, theme, label, theme.FontSize, theme.Text, TextAlignmentOptions.Left);
                lt.enableWordWrapping = false;
            }

            _fieldRt = UiBuild.Rect("ASL_Field", rt);
            float r = Mathf.Min(theme.CornerRadius, 10f);
            _field = UiBuild.Img(_fieldRt, UiShapes.Rounded(r), theme.Secondary, true, true);
            var fle = UiBuild.Le(_fieldRt.gameObject);
            fle.flexibleWidth = 1f; fle.minWidth = 160f; fle.preferredHeight = 38f; fle.minHeight = 38f;
            UiBuild.HGroup(_fieldRt.gameObject, 8f, 14, 12, 4, 4, TextAnchor.MiddleLeft);

            var btn = _fieldRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = _field;
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(OnFieldClick)));

            var vrt = UiBuild.Rect("ASL_Value", _fieldRt);
            _valueText = UiBuild.Text(vrt, theme, string.Empty, theme.FontSize, theme.Text, TextAlignmentOptions.Left);
            _valueText.enableWordWrapping = false;
            _valueText.overflowMode = TextOverflowModes.Ellipsis;
            UiBuild.Le(vrt.gameObject).flexibleWidth = 1f;

            var chev = UiBuild.ImgChild("ASL_Chevron", _fieldRt, UiShapes.Chevron(), theme.Text, false, false, out var crt);
            var cle = UiBuild.Le(crt.gameObject);
            cle.preferredWidth = 20f; cle.preferredHeight = 20f; cle.minWidth = 20f; cle.minHeight = 20f;
        }

        private void ShowValue()
        {
            _valueText.text = Value ?? string.Empty;
        }

        private void OnFieldClick()
        {
            if (Dead || !Interactable) return;
            if (UiPopup.Current != null && UiPopup.Current.Dropdown == this) { UiPopup.CloseCurrent(); return; }
            UiPopup.Open(this);
        }

        internal void Pick(int i)
        {
            UiPopup.CloseCurrent();
            if (i < 0 || i >= _options.Length || i == _index) return;
            _index = i;
            ShowValue();
            Owner.Fire(_onChanged, i, "dropdown changed");
            try { Changed?.Invoke(i); } catch (Exception ex) { Owner.Report("dropdown Changed", ex); }
        }

        internal Color FieldBase => Theme.Secondary;

        public override void Poll()
        {
            if (!_hs.Update(Host, _fieldRt, Interactable)) return;
            int state = _hs.Pressed ? 2 : (_hs.Hover ? 1 : 0);
            if (state == _applied) return;
            _applied = state;
            var c = Theme.Secondary;
            _field.color = state == 0 ? c : (state == 1 ? UiThemeApplier.Hover(c) : UiThemeApplier.Press(c));
        }

        public override void MarkDead()
        {
            base.MarkDead();
            if (UiPopup.Current != null && UiPopup.Current.Dropdown == this) UiPopup.CloseCurrent();
        }
    }

    /// <summary>
    /// The open drop-down list: its own overlay canvas above everything (even the F8 menu), a transparent
    /// full-screen blocker (so an outside click only closes the list) and a panel of toolkit buttons. Only
    /// one popup exists at a time; <see cref="UiRuntime"/> ticks it while open.
    /// </summary>
    internal sealed class UiPopup
    {
        public static UiPopup Current { get; private set; }

        public UiDropdown Dropdown;
        public UiHost Host;
        public RectTransform Panel;
        public int OpenedFrame;
        private GameObject _go;

        public static void Open(UiDropdown d)
        {
            CloseCurrent();
            var opts = d.Options;
            if (opts == null || opts.Length == 0) return;
            var p = new UiPopup { Dropdown = d, OpenedFrame = Time.frameCount };
            try { p.Build(d, opts); Current = p; }
            catch (Exception ex)
            {
                d.Host.Owner.Report("dropdown popup", ex);
                p.Destroy();
            }
        }

        public static void CloseCurrent()
        {
            var p = Current;
            Current = null;
            if (p != null) p.Destroy();
        }

        private void Build(UiDropdown d, string[] opts)
        {
            var theme = d.Host.Theme;
            _go = UiRuntime.NewCanvas("ASL_Popup", 32763);
            var layerRt = _go.GetComponent<RectTransform>();

            // Transparent blocker: swallows the click that dismisses the list.
            var blocker = UiBuild.Rect("ASL_Blocker", layerRt);
            UiBuild.Stretch(blocker);
            UiBuild.Img(blocker, null, UiThemeApplier.Clear, true, false);

            const float itemH = 38f, gap = 2f, pad = 6f;
            int n = opts.Length;
            int shown = Mathf.Min(n, 8);
            float listH = shown * itemH + (shown - 1) * gap;
            var fieldRt = d.FieldRt;
            var fr = fieldRt.rect;
            float w = Mathf.Max(fr.width, 180f);
            float h = listH + pad * 2f;

            Panel = UiBuild.Rect("ASL_PopupPanel", layerRt);
            Panel.anchorMin = Vector2.zero; Panel.anchorMax = Vector2.zero;
            Panel.sizeDelta = new Vector2(w, h);
            float r = Mathf.Min(theme.CornerRadius, 10f);
            UiBuild.Img(Panel, UiShapes.Rounded(r), theme.Panel, true, true);
            var ring = UiBuild.ImgChild("ASL_Border", Panel, UiShapes.Ring(r, 2f), theme.PanelBorder, false, true, out var ringRt);
            UiBuild.Stretch(ringRt);

            Host = new UiHost(d.Host.Owner, theme) { HoverRect = Panel };
            var body = UiBuild.Rect("ASL_PopupBody", Panel);
            UiBuild.Stretch(body, pad, pad, pad, pad);
            UiContainer list = n > 8
                ? UiContainer.CreateScroll(Host, body, listH)
                : UiContainer.CreateColumn(Host, body, gap);
            UiBuild.Stretch(list.Rt);   // fill the body (no layout group above it)

            for (int i = 0; i < n; i++)
            {
                int idx = i;
                bool sel = i == d.Index;
                var st = new UiButtonStyle
                {
                    Variant = sel ? UiVariant.Primary : UiVariant.Ghost,
                    Shape = UiShape.Rounded, Height = itemH, CornerRadius = 6f,
                };
                var b = (UiButton)list.AddButton(opts[i] ?? string.Empty, () => d.Pick(idx), st);
                b.LeftAlign();
            }

            // Place under the field (flip above when there is no room), in screen space via world coordinates.
            float s = fieldRt.lossyScale.x;
            var bl = fieldRt.TransformPoint(new Vector3(fr.xMin, fr.yMin, 0f));
            var tl = fieldRt.TransformPoint(new Vector3(fr.xMin, fr.yMax, 0f));
            float x = bl.x;
            if (x + w * s > Screen.width) x = Screen.width - w * s;
            if (x < 0f) x = 0f;
            if (bl.y - h * s - 4f * s >= 0f)
            {
                Panel.pivot = new Vector2(0f, 1f);
                Panel.position = new Vector3(x, bl.y - 4f * s, 0f);
            }
            else
            {
                Panel.pivot = new Vector2(0f, 0f);
                Panel.position = new Vector3(x, tl.y + 4f * s, 0f);
            }
            Host.Repoll = 3;
        }

        public bool PanelContainsMouse()
        {
            return Panel != null && RectTransformUtility.RectangleContainsScreenPoint(Panel, UiRuntime.Mouse, null);
        }

        /// <summary>Closes on an outside click / when the owner vanished.</summary>
        public void Tick()
        {
            if (Dropdown == null || Dropdown.Dead || !Dropdown.IsActiveInHierarchy()) { CloseCurrent(); return; }
            if (UiRuntime.MouseUp && Time.frameCount != OpenedFrame && !PanelContainsMouse()) CloseCurrent();
        }

        private void Destroy()
        {
            if (Host != null) { for (int i = 0; i < Host.Pollers.Count; i++) Host.Pollers[i].MarkDead(); }
            try { if (_go != null) UnityEngine.Object.Destroy(_go); } catch { }
            _go = null;
        }
    }
}
