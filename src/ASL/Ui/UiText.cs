using System;
using ASL.Api;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>A TextMeshPro label (static, or "live": re-evaluated on a throttle while visible).</summary>
    internal sealed class UiText : UiElement, IUiText
    {
        private readonly TextMeshProUGUI _tmp;
        private readonly bool _mono;
        private string _raw;
        private UiTextAlign _align;
        private Func<string> _provider;
        private float _interval, _accum;
        private int _lastLines;

        public UiText(UiHost host, RectTransform parent, string text, UiTextStyle style) : base(host)
        {
            var rt = UiBuild.Rect("ASL_Text", parent);
            Init(rt);
            _tmp = UiBuild.Text(rt, host.Theme, string.Empty, host.Theme.FontSize, host.Theme.Text, TextAlignmentOptions.Left);
            UiThemeApplier.ApplyTextStyle(_tmp, host.Theme, style);
            _mono = style == UiTextStyle.Mono;
            FlexInRow = 0f;
            FlexInColumn = 1f;
            Text = text;
        }

        /// <summary>Turns this label into a live label.</summary>
        public void MakeLive(Func<string> provider, float refreshSeconds)
        {
            _provider = provider;
            _interval = Mathf.Max(0.05f, refreshSeconds);
            _accum = _interval;                  // evaluate on the first poll
            _tmp.enableWordWrapping = false;     // live text must not change the window height every refresh
            _tmp.overflowMode = TextOverflowModes.Overflow;
            NeedPoll();
        }

        public string Text
        {
            get => _raw;
            set
            {
                value = value ?? string.Empty;
                if (value == _raw) return;
                _raw = value;
                _tmp.text = _mono ? "<mspace=0.55em>" + value + "</mspace>" : value;
                if (_provider == null) Host.MarkDirty();   // wrapped static text may change height
            }
        }

        public Color Color { get => _tmp.color; set => _tmp.color = value; }

        public float FontSize
        {
            get => _tmp.fontSize;
            set
            {
                if (Mathf.Approximately(_tmp.fontSize, value)) return;
                _tmp.fontSize = value;
                Host.MarkDirty();   // a different size always changes the label's height
            }
        }

        public UiTextAlign Align
        {
            get => _align;
            set { _align = value; _tmp.alignment = UiBuild.ToTmp(value); }
        }

        internal TextMeshProUGUI Tmp => _tmp;

        public override void Poll()
        {
            if (_provider == null) return;
            _accum += UiRuntime.Dt;
            if (_accum < _interval) return;
            _accum = 0f;
            if (!IsActiveInHierarchy()) return;
            string s = Owner.Eval(_provider, _raw, "live label");
            if (s == _raw) return;
            Text = s;
            int lines = 1;
            if (s != null) for (int i = 0; i < s.Length; i++) if (s[i] == '\n') lines++;
            if (lines != _lastLines) { _lastLines = lines; Host.MarkDirty(); }
        }
    }
}
