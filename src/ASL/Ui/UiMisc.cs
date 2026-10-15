using System;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>Invisible gap (height in a column, width in a row).</summary>
    internal sealed class UiSpacer : UiElement
    {
        public UiSpacer(UiHost host, RectTransform parent, float size) : base(host)
        {
            var rt = UiBuild.Rect("ASL_Spacer", parent);
            Init(rt);
            var le = Le;
            le.preferredHeight = size; le.minHeight = size;
            le.preferredWidth = size; le.minWidth = 0f;
            FlexInRow = 0f;
            FlexInColumn = 1f;
        }
    }

    /// <summary>Thin divider line.</summary>
    internal sealed class UiSeparator : UiElement
    {
        public UiSeparator(UiHost host, RectTransform parent) : base(host)
        {
            var rt = UiBuild.Rect("ASL_Separator", parent);
            Init(rt);
            UiBuild.Img(rt, null, UiThemeApplier.WithAlpha(host.Theme.PanelBorder, 0.9f), false, false);
            var le = Le;
            le.preferredHeight = 2f; le.minHeight = 2f;
            FlexInRow = 0f;
            FlexInColumn = 1f;
        }
    }

    /// <summary>
    /// Click-to-rebind row for a mod keybind: the caption plus a button showing the current key. Reuses the
    /// existing <see cref="KeybindManager"/> rebind flow (same as the native F8 page).
    /// </summary>
    internal sealed class UiKeybind : UiElement
    {
        private readonly AslKeybind _bind;
        private readonly UiButton _button;

        public UiKeybind(UiHost host, RectTransform parent, IAslKeybind keybind) : base(host)
        {
            _bind = keybind as AslKeybind;
            var theme = host.Theme;
            var rt = UiBuild.Rect("ASL_Keybind", parent);
            Init(rt);
            UiBuild.HGroup(Go, 14f, 8, 8, 2, 2, TextAnchor.MiddleLeft);
            var le = Le;
            le.preferredHeight = 44f; le.minHeight = 44f;

            var lrt = UiBuild.Rect("ASL_Label", rt);
            var lt = UiBuild.Text(lrt, theme, keybind != null ? keybind.DisplayName : "?", theme.FontSize, theme.Text, TextAlignmentOptions.Left);
            lt.enableWordWrapping = false;
            UiBuild.Le(lrt.gameObject).flexibleWidth = 1f;

            _button = new UiButton(host, rt, KeyText(), OnClick,
                new UiButtonStyle { Variant = UiVariant.Secondary, Shape = UiShape.Rounded, Size = UiSize.Small, Width = 170f });
            FlexInRow = 1f;
            FlexInColumn = 1f;
        }

        private string KeyText()
        {
            if (_bind == null) return "-";
            string key = _bind.Key == KeyCode.None ? "-" : _bind.Key.ToString();
            return _bind.HasConflict ? key + " (!)" : key;
        }

        private void OnClick()
        {
            var keys = AslPlugin.Keybinds;
            if (keys == null || _bind == null) return;
            _button.Text = "press a key...";
            keys.BeginRebind(_bind, () => { if (!Dead) _button.Text = KeyText(); });
        }

        public override void MarkDead()
        {
            base.MarkDead();
            _button.MarkDead();
        }
    }
}
