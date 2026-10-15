using UnityEngine;

namespace ASL.Api
{
    /// <summary>
    /// Colours, fonts and spacing used by the UI toolkit. Plain data. Start from a preset
    /// (<see cref="Default"/>, <see cref="Dark"/>, <see cref="Light"/>, <see cref="Game"/>,
    /// <see cref="HighContrast"/>) and tweak:
    /// <code>
    /// var t = UiTheme.Dark; t.Primary = new Color(0.9f, 0.3f, 0.5f);
    /// ctx.Ui.Theme = t;   // all this mod's new windows use it
    /// </code>
    /// Each preset getter returns a fresh copy, so editing one never affects another.
    /// </summary>
    public sealed class UiTheme
    {
        /// <summary>Window background.</summary>
        public Color Background { get; set; }
        /// <summary>Panels / groups inside a window.</summary>
        public Color Panel { get; set; }
        /// <summary>Outline of panels and windows.</summary>
        public Color PanelBorder { get; set; }
        /// <summary>Title bar background.</summary>
        public Color Header { get; set; }
        /// <summary>Primary buttons / accents.</summary>
        public Color Primary { get; set; }
        /// <summary>Secondary (neutral) buttons.</summary>
        public Color Secondary { get; set; }
        /// <summary>Success / positive colour.</summary>
        public Color Success { get; set; }
        /// <summary>Danger / destructive colour.</summary>
        public Color Danger { get; set; }
        /// <summary>Warning colour.</summary>
        public Color Warning { get; set; }
        /// <summary>Normal text.</summary>
        public Color Text { get; set; }
        /// <summary>Hint / caption text.</summary>
        public Color TextMuted { get; set; }
        /// <summary>Highlight colour (focus rings, slider fill, selected tab).</summary>
        public Color Accent { get; set; }
        /// <summary>Background of text inputs and dropdowns.</summary>
        public Color InputBackground { get; set; }

        /// <summary>Body font size in pixels. Default 22.</summary>
        public float FontSize { get; set; } = 22f;
        /// <summary>Header font size in pixels. Default 30.</summary>
        public float HeaderFontSize { get; set; } = 30f;
        /// <summary>Default corner radius in pixels. Default 12.</summary>
        public float CornerRadius { get; set; } = 12f;
        /// <summary>Default gap between layout children. Default 8.</summary>
        public float Spacing { get; set; } = 8f;
        /// <summary>Default inner padding of windows and groups. Default 16.</summary>
        public float Padding { get; set; } = 16f;

        /// <summary>Use the game's own TMP font (default true).</summary>
        public bool UseGameFont { get; set; } = true;

        /// <summary>If true, Primary buttons use the Native clone look (the game's own button).</summary>
        public bool UseGameButtons { get; set; } = false;

        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        /// <summary>The default theme: dark navy panels with saturated blue accents.</summary>
        public static UiTheme Default => new UiTheme
        {
            Background = C(0.07f, 0.08f, 0.11f, 0.97f),
            Panel = C(0.12f, 0.14f, 0.19f, 1f),
            PanelBorder = C(0.24f, 0.28f, 0.37f, 1f),
            Header = C(0.15f, 0.18f, 0.26f, 1f),
            Primary = C(0.20f, 0.48f, 0.96f, 1f),
            Secondary = C(0.30f, 0.34f, 0.43f, 1f),
            Success = C(0.16f, 0.68f, 0.36f, 1f),
            Danger = C(0.88f, 0.25f, 0.27f, 1f),
            Warning = C(0.97f, 0.69f, 0.13f, 1f),
            Text = C(0.93f, 0.95f, 0.98f, 1f),
            TextMuted = C(0.60f, 0.65f, 0.74f, 1f),
            Accent = C(0.36f, 0.78f, 1.00f, 1f),
            InputBackground = C(0.06f, 0.07f, 0.10f, 1f),
        };

        /// <summary>A neutral dark-grey theme.</summary>
        public static UiTheme Dark => new UiTheme
        {
            Background = C(0.09f, 0.09f, 0.10f, 0.97f),
            Panel = C(0.14f, 0.14f, 0.16f, 1f),
            PanelBorder = C(0.28f, 0.28f, 0.32f, 1f),
            Header = C(0.18f, 0.18f, 0.21f, 1f),
            Primary = C(0.25f, 0.52f, 0.96f, 1f),
            Secondary = C(0.32f, 0.32f, 0.37f, 1f),
            Success = C(0.20f, 0.70f, 0.38f, 1f),
            Danger = C(0.88f, 0.28f, 0.28f, 1f),
            Warning = C(0.96f, 0.68f, 0.14f, 1f),
            Text = C(0.94f, 0.94f, 0.95f, 1f),
            TextMuted = C(0.62f, 0.62f, 0.67f, 1f),
            Accent = C(0.40f, 0.72f, 1.00f, 1f),
            InputBackground = C(0.07f, 0.07f, 0.08f, 1f),
        };

        /// <summary>A light theme (dark text on pale panels).</summary>
        public static UiTheme Light => new UiTheme
        {
            Background = C(0.94f, 0.95f, 0.97f, 0.98f),
            Panel = C(1.00f, 1.00f, 1.00f, 1f),
            PanelBorder = C(0.78f, 0.80f, 0.86f, 1f),
            Header = C(0.86f, 0.89f, 0.95f, 1f),
            Primary = C(0.16f, 0.43f, 0.90f, 1f),
            Secondary = C(0.62f, 0.66f, 0.74f, 1f),
            Success = C(0.13f, 0.60f, 0.30f, 1f),
            Danger = C(0.82f, 0.20f, 0.22f, 1f),
            Warning = C(0.90f, 0.58f, 0.05f, 1f),
            Text = C(0.10f, 0.12f, 0.16f, 1f),
            TextMuted = C(0.40f, 0.44f, 0.52f, 1f),
            Accent = C(0.10f, 0.55f, 0.85f, 1f),
            InputBackground = C(0.93f, 0.94f, 0.97f, 1f),
        };

        /// <summary>The default look, but Primary buttons use the game's own button clone.</summary>
        public static UiTheme Game
        {
            get { var t = Default; t.UseGameButtons = true; return t; }
        }

        /// <summary>Maximum-contrast black and white with yellow accents (accessibility).</summary>
        public static UiTheme HighContrast => new UiTheme
        {
            Background = C(0f, 0f, 0f, 1f),
            Panel = C(0.05f, 0.05f, 0.05f, 1f),
            PanelBorder = C(1f, 1f, 1f, 1f),
            Header = C(0.10f, 0.10f, 0.10f, 1f),
            Primary = C(1.00f, 0.90f, 0.10f, 1f),
            Secondary = C(0.35f, 0.35f, 0.35f, 1f),
            Success = C(0.20f, 1.00f, 0.30f, 1f),
            Danger = C(1.00f, 0.25f, 0.25f, 1f),
            Warning = C(1.00f, 0.65f, 0.00f, 1f),
            Text = C(1f, 1f, 1f, 1f),
            TextMuted = C(0.80f, 0.80f, 0.80f, 1f),
            Accent = C(0.20f, 0.90f, 1.00f, 1f),
            InputBackground = C(0f, 0f, 0f, 1f),
            FontSize = 24f,
            HeaderFontSize = 32f,
        };

        /// <summary>A field-by-field copy of this theme.</summary>
        public UiTheme Clone() => (UiTheme)MemberwiseClone();
    }
}
