using ASL.Api;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>Maps theme + variant/size/text-style onto concrete colours and sizes. Pure maths, no Unity objects kept.</summary>
    internal static class UiThemeApplier
    {
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        public static Color VariantColor(UiTheme t, UiVariant v)
        {
            switch (v)
            {
                case UiVariant.Secondary: return t.Secondary;
                case UiVariant.Success: return t.Success;
                case UiVariant.Danger: return t.Danger;
                case UiVariant.Warning: return t.Warning;
                case UiVariant.Ghost: return Clear;
                default: return t.Primary;   // Primary, Outline
            }
        }

        /// <summary>Dark or light text, whichever reads on <paramref name="bg"/>.</summary>
        public static Color ReadableOn(Color bg)
        {
            float l = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return l > 0.62f ? new Color(0.08f, 0.09f, 0.11f, 1f) : new Color(1f, 1f, 1f, 1f);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a);
        }

        public static Color Hover(Color c) => Mix(c, new Color(1f, 1f, 1f, 1f), 0.16f);
        public static Color Press(Color c) => Mix(c, new Color(0f, 0f, 0f, 1f), 0.22f);
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static float FontSizeFor(UiTheme t, UiSize s)
        {
            switch (s)
            {
                case UiSize.Small: return t.FontSize * 0.82f;
                case UiSize.Large: return t.FontSize * 1.18f;
                case UiSize.XLarge: return t.FontSize * 1.45f;
                default: return t.FontSize;
            }
        }

        public static float Radius(UiTheme t, float styleRadius) => styleRadius >= 0f ? styleRadius : t.CornerRadius;

        /// <summary>Configures a TMP label for a text style.</summary>
        public static void ApplyTextStyle(TMP_Text tmp, UiTheme t, UiTextStyle s)
        {
            tmp.color = t.Text;
            tmp.fontStyle = FontStyles.Normal;
            switch (s)
            {
                case UiTextStyle.Small: tmp.fontSize = t.FontSize * 0.82f; break;
                case UiTextStyle.Header: tmp.fontSize = t.HeaderFontSize; tmp.fontStyle = FontStyles.Bold; break;
                case UiTextStyle.Title: tmp.fontSize = t.HeaderFontSize * 1.25f; tmp.fontStyle = FontStyles.Bold; break;
                case UiTextStyle.Muted: tmp.fontSize = t.FontSize * 0.92f; tmp.color = t.TextMuted; break;
                case UiTextStyle.Mono: tmp.fontSize = t.FontSize * 0.92f; break;
                default: tmp.fontSize = t.FontSize; break;
            }
        }

        public static Color ToastColor(UiTheme t, UiToastKind k)
        {
            switch (k)
            {
                case UiToastKind.Success: return t.Success;
                case UiToastKind.Warning: return t.Warning;
                case UiToastKind.Error: return t.Danger;
                default: return t.Accent;
            }
        }
    }
}
