using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>Progress bar: a pill track with a pill fill whose right anchor is the value (no per-frame work).</summary>
    internal sealed class UiProgress : UiElement, IUiProgress
    {
        private readonly RectTransform _fillRt;
        private readonly Image _fill;
        private readonly TextMeshProUGUI _text;
        private float _value;
        private string _str;

        public UiProgress(UiHost host, RectTransform parent, float initial, string text) : base(host)
        {
            var theme = host.Theme;
            var rt = UiBuild.Rect("ASL_Progress", parent);
            Init(rt);
            UiBuild.Img(rt, UiShapes.Rounded(15f), theme.InputBackground, false, true);
            var le = Le;
            le.preferredHeight = 30f; le.minHeight = 30f;

            _fill = UiBuild.ImgChild("ASL_Fill", rt, UiShapes.Rounded(15f), theme.Primary, false, true, out _fillRt);
            _fillRt.anchorMin = new Vector2(0f, 0f);
            _fillRt.anchorMax = new Vector2(0f, 1f);
            _fillRt.offsetMin = Vector2.zero;
            _fillRt.offsetMax = Vector2.zero;

            var trt = UiBuild.Rect("ASL_Text", rt);
            UiBuild.Stretch(trt);
            _text = UiBuild.Text(trt, theme, text ?? string.Empty, theme.FontSize * 0.82f, theme.Text, TextAlignmentOptions.Center);
            _text.enableWordWrapping = false;
            _str = text;

            FlexInRow = 1f;
            FlexInColumn = 1f;
            Value = initial;
        }

        public float Value
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp01(value);
                _fillRt.anchorMax = new Vector2(_value, 1f);
                _fillRt.gameObject.SetActive(_value > 0.001f);
            }
        }

        public Color FillColor { get => _fill.color; set => _fill.color = value; }

        public string Text
        {
            get => _str;
            set { _str = value; _text.text = value ?? string.Empty; }
        }
    }
}
