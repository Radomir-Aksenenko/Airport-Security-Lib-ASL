using System;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Single-line text field: a <c>TMP_InputField</c> assembled from code (background, masked viewport, text,
    /// placeholder, focus ring). Text edits / focus are detected by POLLING (no UnityEvent&lt;string&gt;
    /// listeners): <c>isFocused</c> every tick, <c>text</c> only while focused (every other frame) and once
    /// after focus is lost. Enter fires <see cref="Submitted"/> via <c>Input.GetKeyDown</c>.
    /// </summary>
    internal sealed class UiInput : UiElement, IUiInput
    {
        private readonly Action<string> _onSubmit, _onChanged;
        private TMP_InputField _field;
        private TextMeshProUGUI _text, _placeholder;
        private Image _bg, _ring;
        private string _last = string.Empty;
        private bool _wasFocused;
        private int _skip;
        private int _focusRequests;

        public event Action<string> Changed;
        public event Action<string> Submitted;

        public UiInput(UiHost host, RectTransform parent, string placeholder, string initial, Action<string> onSubmit, Action<string> onChanged) : base(host)
        {
            _onSubmit = onSubmit; _onChanged = onChanged;
            Build(parent, placeholder, initial ?? string.Empty);
            FlexInRow = 1f;
            FlexInColumn = 1f;
            NeedPoll();
        }

        public string Text
        {
            get => _field.text;
            set { value = value ?? string.Empty; _field.SetTextWithoutNotify(value); _last = value; }
        }

        public string Placeholder
        {
            get => _placeholder.text;
            set => _placeholder.text = value ?? string.Empty;
        }

        public int CharacterLimit { get => _field.characterLimit; set => _field.characterLimit = Mathf.Max(0, value); }

        public bool IsFocused { get { try { return _field.isFocused; } catch { return false; } } }

        public void Focus()
        {
            _focusRequests = 6;
            TryActivate();
        }

        /// <summary>Drop keyboard focus (called when the owning window closes).</summary>
        public void Blur()
        {
            _focusRequests = 0;
            try { if (_field != null && _field.isFocused) _field.DeactivateInputField(); } catch { }
        }

        private void TryActivate()
        {
            try { if (IsActiveInHierarchy() && Interactable) _field.ActivateInputField(); } catch (Exception ex) { Owner.Report("input focus", ex); }
        }

        private void Build(RectTransform parent, string placeholder, string initial)
        {
            var theme = Theme;
            var rt = UiBuild.Rect("ASL_Input", parent);
            Init(rt);
            Go.SetActive(false);   // finish wiring before TMP_InputField.OnEnable runs

            _bg = UiBuild.Img(rt, UiShapes.Rounded(Mathf.Min(theme.CornerRadius, 10f)), theme.InputBackground, true, true);
            var le = Le;
            le.preferredHeight = 44f; le.minHeight = 44f; le.flexibleWidth = 1f; le.minWidth = 120f;

            _ring = UiBuild.ImgChild("ASL_Focus", rt, UiShapes.Ring(Mathf.Min(theme.CornerRadius, 10f), 2f), theme.Accent, false, true, out var ringRt);
            UiBuild.Stretch(ringRt);
            _ring.gameObject.SetActive(false);

            var vp = UiBuild.Rect("ASL_TextArea", rt);
            UiBuild.Stretch(vp, 12f, 4f, 12f, 4f);
            vp.gameObject.AddComponent<RectMask2D>();

            var phRt = UiBuild.Rect("ASL_Placeholder", vp);
            UiBuild.Stretch(phRt);
            _placeholder = UiBuild.Text(phRt, theme, placeholder ?? string.Empty, theme.FontSize, UiThemeApplier.WithAlpha(theme.TextMuted, 0.8f), TextAlignmentOptions.Left);
            _placeholder.fontStyle = FontStyles.Italic;
            _placeholder.enableWordWrapping = false;

            var txRt = UiBuild.Rect("ASL_Text", vp);
            UiBuild.Stretch(txRt);
            _text = UiBuild.Text(txRt, theme, string.Empty, theme.FontSize, theme.Text, TextAlignmentOptions.Left);
            _text.enableWordWrapping = false;

            _field = Go.AddComponent<TMP_InputField>();
            _field.textViewport = vp;
            _field.textComponent = _text;
            _field.placeholder = _placeholder;
            _field.targetGraphic = _bg;
            _field.transition = Selectable.Transition.None;
            _field.lineType = TMP_InputField.LineType.SingleLine;
            _field.contentType = TMP_InputField.ContentType.Standard;
            _field.richText = false;
            _field.pointSize = theme.FontSize;
            _field.customCaretColor = true;
            _field.caretColor = theme.Text;
            _field.caretWidth = 2;
            _field.selectionColor = UiThemeApplier.WithAlpha(theme.Accent, 0.4f);
            _field.onFocusSelectAll = false;
            _field.restoreOriginalTextOnEscape = true;
            var font = _text.font;
            if (font != null) _field.fontAsset = font;
            _field.text = initial;
            _last = initial;

            Go.SetActive(true);
        }

        protected override void OnInteractableChanged()
        {
            try { _field.interactable = Interactable; } catch { }
        }

        public override void Poll()
        {
            bool f = IsFocused;
            if (f) UiRuntime.NoteTextFocused();

            if (_focusRequests > 0)
            {
                if (f) _focusRequests = 0;
                else { _focusRequests--; TryActivate(); }
            }

            if (f != _wasFocused) _ring.gameObject.SetActive(f);

            if (f || _wasFocused)
            {
                if (!f || (++_skip & 1) == 0)
                {
                    string t = _field.text;
                    if (!string.Equals(t, _last, StringComparison.Ordinal))
                    {
                        _last = t;
                        Owner.Fire(_onChanged, t, "input changed");
                        try { Changed?.Invoke(t); } catch (Exception ex) { Owner.Report("input Changed", ex); }
                    }
                }
                if (UiRuntime.EnterPressed)
                {
                    string t = _field.text;
                    if (!string.Equals(t, _last, StringComparison.Ordinal))
                    {
                        _last = t;
                        Owner.Fire(_onChanged, t, "input changed");
                        try { Changed?.Invoke(t); } catch (Exception ex) { Owner.Report("input Changed", ex); }
                    }
                    Owner.Fire(_onSubmit, t, "input submitted");
                    try { Submitted?.Invoke(t); } catch (Exception ex) { Owner.Report("input Submitted", ex); }
                }
            }
            _wasFocused = f;
        }
    }
}
