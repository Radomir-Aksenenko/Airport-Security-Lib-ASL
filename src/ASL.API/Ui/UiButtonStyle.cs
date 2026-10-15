using UnityEngine;

namespace ASL.Api
{
    /// <summary>
    /// Describes how a button looks: colour role, shape, size and optional overrides. Plain data —
    /// build one with the static presets and tweak with <see cref="With"/>.
    /// <code>
    /// row.AddButton("Save",  Save,  UiButtonStyle.Success);
    /// row.AddButton("Quit",  Quit,  UiButtonStyle.Pill(UiVariant.Danger).With(size: UiSize.Large));
    /// row.AddIconButton(icon, Help, 48f, UiShape.Circle);
    /// </code>
    /// Presets return a fresh instance each time, so mutating the result never affects other buttons.
    /// </summary>
    public sealed class UiButtonStyle
    {
        /// <summary>Colour role. Default <see cref="UiVariant.Primary"/>.</summary>
        public UiVariant Variant { get; set; } = UiVariant.Primary;

        /// <summary>Silhouette. Default <see cref="UiShape.Rounded"/>.</summary>
        public UiShape Shape { get; set; } = UiShape.Rounded;

        /// <summary>Preset height. Default <see cref="UiSize.Medium"/>.</summary>
        public UiSize Size { get; set; } = UiSize.Medium;

        /// <summary>Explicit width in pixels; 0 = automatic (fit text / stretch).</summary>
        public float Width { get; set; } = 0f;

        /// <summary>Explicit height in pixels; 0 = take it from <see cref="Size"/>.</summary>
        public float Height { get; set; } = 0f;

        /// <summary>Corner radius in pixels; -1 = use the theme's CornerRadius.</summary>
        public float CornerRadius { get; set; } = -1f;

        /// <summary>Background colour override; null = from the theme/variant.</summary>
        public Color? Background { get; set; }

        /// <summary>Text colour override; null = from the theme/variant.</summary>
        public Color? TextColor { get; set; }

        /// <summary>Font size override; 0 = automatic (from the theme and <see cref="Size"/>).</summary>
        public float FontSize { get; set; } = 0f;

        /// <summary>Optional icon drawn on the button.</summary>
        public Sprite Icon { get; set; }

        /// <summary>If true, only <see cref="Icon"/> is shown (no text).</summary>
        public bool IconOnly { get; set; }

        // ---- Presets (fresh instance each time) -----------------------------------------

        /// <summary>Primary call-to-action button.</summary>
        public static UiButtonStyle Primary => new UiButtonStyle { Variant = UiVariant.Primary };

        /// <summary>Neutral secondary button.</summary>
        public static UiButtonStyle Secondary => new UiButtonStyle { Variant = UiVariant.Secondary };

        /// <summary>Green "confirm" button.</summary>
        public static UiButtonStyle Success => new UiButtonStyle { Variant = UiVariant.Success };

        /// <summary>Red "destructive" button.</summary>
        public static UiButtonStyle Danger => new UiButtonStyle { Variant = UiVariant.Danger };

        /// <summary>Amber "careful" button.</summary>
        public static UiButtonStyle Warning => new UiButtonStyle { Variant = UiVariant.Warning };

        /// <summary>Text-only button with a hover highlight.</summary>
        public static UiButtonStyle Ghost => new UiButtonStyle { Variant = UiVariant.Ghost };

        /// <summary>Transparent button with a coloured outline ring.</summary>
        public static UiButtonStyle Outline => new UiButtonStyle { Variant = UiVariant.Outline };

        /// <summary>A clone of the game's own menu button (matches the game's look).</summary>
        public static UiButtonStyle Native => new UiButtonStyle { Shape = UiShape.Native };

        /// <summary>A capsule-shaped button of the given colour role.</summary>
        public static UiButtonStyle Pill(UiVariant v = UiVariant.Primary) => new UiButtonStyle { Variant = v, Shape = UiShape.Pill };

        /// <summary>A circular button of the given diameter (width = height = <paramref name="diameter"/>).</summary>
        public static UiButtonStyle Circle(float diameter, UiVariant v = UiVariant.Primary) =>
            new UiButtonStyle { Variant = v, Shape = UiShape.Circle, Width = diameter, Height = diameter };

        /// <summary>
        /// Returns a copy with the given fields replaced (null = keep). Does not modify this instance.
        /// </summary>
        public UiButtonStyle With(UiShape? shape = null, UiSize? size = null, UiVariant? variant = null, float? width = null, float? height = null)
        {
            var c = Clone();
            if (shape.HasValue) c.Shape = shape.Value;
            if (size.HasValue) c.Size = size.Value;
            if (variant.HasValue) c.Variant = variant.Value;
            if (width.HasValue) c.Width = width.Value;
            if (height.HasValue) c.Height = height.Value;
            return c;
        }

        /// <summary>A field-by-field copy of this style.</summary>
        public UiButtonStyle Clone() => new UiButtonStyle
        {
            Variant = Variant, Shape = Shape, Size = Size, Width = Width, Height = Height,
            CornerRadius = CornerRadius, Background = Background, TextColor = TextColor,
            FontSize = FontSize, Icon = Icon, IconOnly = IconOnly,
        };

        /// <summary>The pixel height of a <see cref="UiSize"/> preset: 32 / 44 / 60 / 76.</summary>
        public static float HeightFor(UiSize size)
        {
            switch (size)
            {
                case UiSize.Small: return 32f;
                case UiSize.Large: return 60f;
                case UiSize.XLarge: return 76f;
                default: return 44f;
            }
        }

        /// <summary>The height this style resolves to: <see cref="Height"/> if set, else the <see cref="Size"/> preset.</summary>
        public float ResolvedHeight => Height > 0f ? Height : HeightFor(Size);
    }
}
