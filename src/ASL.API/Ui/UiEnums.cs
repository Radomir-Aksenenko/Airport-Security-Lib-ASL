namespace ASL.Api
{
    /// <summary>The silhouette of a button (or other framed widget). Shapes are drawn procedurally by ASL.</summary>
    public enum UiShape
    {
        /// <summary>Sharp-cornered rectangle.</summary>
        Rect,
        /// <summary>Rectangle with rounded corners (radius from the theme or <see cref="UiButtonStyle.CornerRadius"/>). The default.</summary>
        Rounded,
        /// <summary>Fully rounded ends (capsule).</summary>
        Pill,
        /// <summary>A circle; width and height both use the button height.</summary>
        Circle,
        /// <summary>A square with a small corner radius; width and height both use the button height.</summary>
        Square,
        /// <summary>A clone of the game's own menu button, so it matches the game's look exactly.</summary>
        Native,
    }

    /// <summary>The colour role of a button. The actual colours come from the active <see cref="UiTheme"/>.</summary>
    public enum UiVariant
    {
        /// <summary>Main call-to-action (theme Primary colour).</summary>
        Primary,
        /// <summary>Neutral secondary action.</summary>
        Secondary,
        /// <summary>Positive / confirming action (green).</summary>
        Success,
        /// <summary>Destructive action (red).</summary>
        Danger,
        /// <summary>Cautionary action (amber).</summary>
        Warning,
        /// <summary>No background until hovered; text only.</summary>
        Ghost,
        /// <summary>Transparent with a coloured border ring.</summary>
        Outline,
    }

    /// <summary>Preset button heights. Square and Circle shapes use the height for both dimensions.</summary>
    public enum UiSize
    {
        /// <summary>32 px tall.</summary>
        Small,
        /// <summary>44 px tall (default).</summary>
        Medium,
        /// <summary>60 px tall.</summary>
        Large,
        /// <summary>76 px tall.</summary>
        XLarge,
    }

    /// <summary>Preset text looks for labels.</summary>
    public enum UiTextStyle
    {
        /// <summary>Normal body text (theme FontSize).</summary>
        Body,
        /// <summary>Smaller text.</summary>
        Small,
        /// <summary>Section header (theme HeaderFontSize).</summary>
        Header,
        /// <summary>Largest text, for window/page titles.</summary>
        Title,
        /// <summary>Body text in the muted colour (hints, captions).</summary>
        Muted,
        /// <summary>Monospace-looking text, for console-like output.</summary>
        Mono,
    }

    /// <summary>Horizontal text alignment.</summary>
    public enum UiTextAlign
    {
        /// <summary>Align to the left edge.</summary>
        Left,
        /// <summary>Centre.</summary>
        Center,
        /// <summary>Align to the right edge.</summary>
        Right,
    }

    /// <summary>How a boolean toggle is drawn.</summary>
    public enum UiToggleStyle
    {
        /// <summary>An on/off switch (default).</summary>
        Switch,
        /// <summary>A tick box.</summary>
        Checkbox,
        /// <summary>A button that stays pressed while on.</summary>
        Button,
    }

    /// <summary>Severity/colour of a toast notification.</summary>
    public enum UiToastKind
    {
        /// <summary>Neutral information.</summary>
        Info,
        /// <summary>Something succeeded.</summary>
        Success,
        /// <summary>Something needs attention.</summary>
        Warning,
        /// <summary>Something failed.</summary>
        Error,
    }

    /// <summary>A screen anchor point for windows and HUDs.</summary>
    public enum UiAnchor
    {
        /// <summary>Top-left corner.</summary>
        TopLeft,
        /// <summary>Top edge, centred.</summary>
        Top,
        /// <summary>Top-right corner.</summary>
        TopRight,
        /// <summary>Left edge, centred vertically.</summary>
        Left,
        /// <summary>Screen centre.</summary>
        Center,
        /// <summary>Right edge, centred vertically.</summary>
        Right,
        /// <summary>Bottom-left corner.</summary>
        BottomLeft,
        /// <summary>Bottom edge, centred.</summary>
        Bottom,
        /// <summary>Bottom-right corner.</summary>
        BottomRight,
    }
}
