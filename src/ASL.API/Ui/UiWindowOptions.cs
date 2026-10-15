namespace ASL.Api
{
    /// <summary>
    /// Options for <see cref="IAslUi.CreateWindow"/> / <see cref="IAslUi.CreateHud"/>. Every field has a
    /// sensible default, so <c>null</c> (or <c>new UiWindowOptions()</c>) gives a centred, draggable,
    /// 640-px-wide window.
    /// <code>
    /// var win = ctx.Ui.CreateWindow("main", "My Mod", new UiWindowOptions { Width = 520, Anchor = UiAnchor.Right });
    /// </code>
    /// </summary>
    public sealed class UiWindowOptions
    {
        /// <summary>Window width in pixels (reference resolution 1920x1080). Default 640.</summary>
        public float Width { get; set; } = 640f;

        /// <summary>Fixed height in pixels; 0 = auto-fit content up to 85% of the screen, then scroll.</summary>
        public float Height { get; set; } = 0f;

        /// <summary>Where on screen the window sits. Default <see cref="UiAnchor.Center"/>.</summary>
        public UiAnchor Anchor { get; set; } = UiAnchor.Center;

        /// <summary>Horizontal offset from the anchor, in pixels (positive = right).</summary>
        public float OffsetX { get; set; }

        /// <summary>Vertical offset from the anchor, in pixels (positive = up).</summary>
        public float OffsetY { get; set; }

        /// <summary>The player can drag the window by its title bar. Default true.</summary>
        public bool Draggable { get; set; } = true;

        /// <summary>The title bar shows a close button. Default true.</summary>
        public bool Closable { get; set; } = true;

        /// <summary>A dim backdrop blocks clicks outside the window. Default false.</summary>
        public bool Modal { get; set; } = false;

        /// <summary>While open, the mouse cursor is freed for the window. Default true. Set false for pure HUDs.</summary>
        public bool CaptureCursor { get; set; } = true;

        /// <summary>While open, the local player's look/move input is blocked. Default true.</summary>
        public bool BlockGameInput { get; set; } = true;

        /// <summary>Show the title bar. Default true.</summary>
        public bool ShowTitleBar { get; set; } = true;

        /// <summary>Content scrolls when it is taller than the window. Default true.</summary>
        public bool Scrollable { get; set; } = true;

        /// <summary>Remember where the player dragged the window across sessions. Default true.</summary>
        public bool RememberPosition { get; set; } = true;

        /// <summary>Pressing Escape closes the window (topmost first). Default true.</summary>
        public bool CloseOnEscape { get; set; } = true;

        /// <summary>Theme for this window; null = the mod's <see cref="IAslUi.Theme"/>.</summary>
        public UiTheme Theme { get; set; } = null;

        /// <summary>Inner padding in pixels; -1 = the theme's Padding.</summary>
        public float Padding { get; set; } = -1f;

        /// <summary>Layer order relative to other windows; higher draws on top. Default 0.</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>A field-by-field copy.</summary>
        public UiWindowOptions Clone() => (UiWindowOptions)MemberwiseClone();
    }
}
