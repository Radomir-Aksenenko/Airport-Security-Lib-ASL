using System;
using System.Globalization;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Labelled slider. Uses Unity's own <c>Slider</c> (dragging works natively through the EventSystem); we
    /// build its Background / Fill / Handle hierarchy and detect changes by POLLING <c>Slider.value</c> (no
    /// UnityEvent&lt;float&gt; listener). Step snapping and the value read-out are done on change only.
    /// </summary>
    internal sealed class UiSlider : UiElement, IUiSlider
    {
        private readonly Action<float> _onChanged;
        private readonly string _format;
        private Slider _slider;
        private TextMeshProUGUI _valueText;
        private float _min, _max, _step, _value, _lastRaw;

        public event Action<float> Changed;

        public UiSlider(UiHost host, RectTransform parent, string label, float min, float max, float initial, Action<float> onChanged, float step, string format) : base(host)
        {
            _onChanged = onChanged;
            _format = string.IsNullOrEmpty(format) ? "0.##" : format;
            _min = min; _max = max > min ? max : min + 1f; _step = Mathf.Max(0f, step);
            Build(parent, label);
            FlexInRow = 1f;
            FlexInColumn = 1f;
            _value = Snap(Mathf.Clamp(initial, _min, _max));
            _slider.SetValueWithoutNotify(_value);
            _lastRaw = _value;
            UpdateText();
            NeedPoll();
        }

        public float Value
        {
            get => _value;
            set
            {
                _value = Snap(Mathf.Clamp(value, _min, _max));
                _slider.SetValueWithoutNotify(_value);
                _lastRaw = _value;
                UpdateText();
            }
        }

        public float Min { get => _min; set { _min = value; _slider.minValue = value; Value = _value; } }
        public float Max { get => _max; set { _max = value; _slider.maxValue = value; Value = _value; } }
        public float Step { get => _step; set { _step = Mathf.Max(0f, value); Value = _value; } }

        private void Build(RectTransform parent, string label)
        {
            var theme = Theme;
            var rt = UiBuild.Rect("ASL_SliderRow", parent);
            Init(rt);
            UiBuild.HGroup(Go, 14f, 8, 8, 2, 2, TextAnchor.MiddleLeft);
            var le = Le;
            le.preferredHeight = 40f; le.minHeight = 40f;

            if (!string.IsNullOrEmpty(label))
            {
                var lrt = UiBuild.Rect("ASL_Label", rt);
                var lt = UiBuild.Text(lrt, theme, label, theme.FontSize, theme.Text, TextAlignmentOptions.Left);
                lt.enableWordWrapping = false;
                UiBuild.Le(lrt.gameObject).flexibleWidth = 0f;
            }

            // --- the Slider itself
            var srt = UiBuild.Rect("ASL_Slider", rt);
            var sle = UiBuild.Le(srt.gameObject);
            sle.flexibleWidth = 1f; sle.minWidth = 140f; sle.preferredHeight = 28f; sle.minHeight = 28f;

            var bgRt = UiBuild.Rect("ASL_Background", srt);
            bgRt.anchorMin = new Vector2(0f, 0.5f); bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 8f);
            var bg = UiBuild.Img(bgRt, UiShapes.Rounded(4f), theme.InputBackground, true, true);

            var fillArea = UiBuild.Rect("ASL_FillArea", srt);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-24f, 8f);
            var fillRt = UiBuild.Rect("ASL_Fill", fillArea);
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(12f, 0f);
            UiBuild.Img(fillRt, UiShapes.Rounded(4f), theme.Primary, true, true);

            var slide = UiBuild.Rect("ASL_HandleArea", srt);
            UiBuild.Stretch(slide, 12f, 0f, 12f, 0f);
            var handleRt = UiBuild.Rect("ASL_Handle", slide);
            handleRt.anchorMin = new Vector2(0f, 0.5f); handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.sizeDelta = new Vector2(24f, 24f);
            var handle = UiBuild.Img(handleRt, UiShapes.Circle(), theme.Text, true, false);

            _slider = srt.gameObject.AddComponent<Slider>();
            _slider.fillRect = fillRt;
            _slider.handleRect = handleRt;
            _slider.targetGraphic = handle;
            _slider.direction = Slider.Direction.LeftToRight;
            _slider.transition = Selectable.Transition.None;
            _slider.wholeNumbers = false;
            _slider.minValue = _min;
            _slider.maxValue = _max;

            var vrt = UiBuild.Rect("ASL_Value", rt);
            _valueText = UiBuild.Text(vrt, theme, string.Empty, theme.FontSize * 0.92f, theme.TextMuted, TextAlignmentOptions.Right);
            _valueText.enableWordWrapping = false;
            var vle = UiBuild.Le(vrt.gameObject);
            vle.minWidth = 76f; vle.preferredWidth = 76f;
        }

        private float Snap(float v)
        {
            if (_step <= 0f) return v;
            float s = _min + Mathf.Round((v - _min) / _step) * _step;
            return Mathf.Clamp(s, _min, _max);
        }

        private void UpdateText()
        {
            string s;
            try { s = _value.ToString(_format, CultureInfo.InvariantCulture); }
            catch { s = _value.ToString(CultureInfo.InvariantCulture); }
            _valueText.text = s;
        }

        public override void Poll()
        {
            float v = _slider.value;
            if (v == _lastRaw) return;
            if (_step > 0f)
            {
                float snapped = Snap(v);
                if (snapped != v) { _slider.SetValueWithoutNotify(snapped); v = snapped; }
            }
            _lastRaw = v;
            if (v == _value) return;
            _value = v;
            UpdateText();
            Owner.Fire(_onChanged, v, "slider changed");
            try { Changed?.Invoke(v); } catch (Exception ex) { Owner.Report("slider Changed", ex); }
        }
    }
}
