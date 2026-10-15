// STUB: Unity.TextMeshPro (namespace TMPro).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, TopFlush = 272, TopGeoAligned = 288,
        Left = 513, Center = 514, Right = 516, Justified = 520, Flush = 528, CenterGeoAligned = 544,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, BottomFlush = 1040, BottomGeoAligned = 1056,
        BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, BaselineJustified = 2056, BaselineFlush = 2064, BaselineGeoAligned = 2080,
        MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, MidlineFlush = 4112, MidlineGeoAligned = 4128,
        CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, CaplineJustified = 8200, CaplineFlush = 8208, CaplineGeoAligned = 8224
    }
    [Flags] public enum HorizontalAlignmentOptions { Left = 1, Center = 2, Right = 4, Justified = 8, Flush = 16, Geometry = 32 }
    [Flags] public enum VerticalAlignmentOptions { Top = 256, Middle = 512, Bottom = 1024, Baseline = 2048, Geometry = 4096, Capline = 8192 }
    [Flags]
    public enum FontStyles
    {
        Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32,
        Strikethrough = 64, Superscript = 128, Subscript = 256, Highlight = 512
    }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3, ScrollRect = 4, Page = 5, Linked = 6 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum FontWeight { Thin = 100, ExtraLight = 200, Light = 300, Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, Heavy = 800, Black = 900 }

    public struct VertexGradient
    {
        public Color topLeft, topRight, bottomLeft, bottomRight;
        public VertexGradient(Color color) { topLeft = topRight = bottomLeft = bottomRight = color; }
        public VertexGradient(Color color0, Color color1, Color color2, Color color3) { topLeft = color0; topRight = color1; bottomLeft = color2; bottomRight = color3; }
    }

    public class TMP_Asset : ScriptableObject
    {
        public TMP_Asset(IntPtr pointer) : base(pointer) { }
        public int instanceID { get; }
        public int hashCode { get; set; }
        public Material material { get; set; }
    }

    public class TMP_SpriteAsset : TMP_Asset { public TMP_SpriteAsset(IntPtr pointer) : base(pointer) { } }

    public class TMP_ColorGradient : ScriptableObject
    {
        public TMP_ColorGradient(IntPtr pointer) : base(pointer) { }
        public TMP_ColorGradient() : base() { }
        public Color topLeft, topRight, bottomLeft, bottomRight;
    }

    public class TMP_FontAsset : TMP_Asset
    {
        public TMP_FontAsset(IntPtr pointer) : base(pointer) { }
        public Texture2D atlasTexture { get; }
        public Il2CppSystem.Collections.Generic.List<TMP_FontAsset> fallbackFontAssetTable { get; set; }
        public static TMP_FontAsset CreateFontAsset(Font font) => throw null;
        public bool HasCharacter(char character) => throw null;
    }

    public class TMP_Settings : ScriptableObject
    {
        public TMP_Settings(IntPtr pointer) : base(pointer) { }
        public static TMP_Settings instance { get; }
        public static TMP_FontAsset defaultFontAsset { get; }
        public static bool enableWordWrapping { get; }
        public static bool enableKerning { get; }
        public static bool enableExtraPadding { get; }
        public static bool enableTintAllSprites { get; }
        public static TMP_SpriteAsset defaultSpriteAsset { get; }
        public static bool warningsDisabled { get; }
    }

    public abstract class TMP_Text : MaskableGraphic
    {
        protected TMP_Text(IntPtr pointer) : base(pointer) { }
        public virtual string text { get; set; }
        public bool isRightToLeftText { get; set; }
        public TMP_FontAsset font { get; set; }
        public virtual Material fontSharedMaterial { get; set; }
        public virtual Material fontMaterial { get; set; }
        public Color32 faceColor { get; set; }
        public Color32 outlineColor { get; set; }
        public float outlineWidth { get; set; }
        public float fontSize { get; set; }
        public FontWeight fontWeight { get; set; }
        public bool enableAutoSizing { get; set; }
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public FontStyles fontStyle { get; set; }
        public bool isUsingBold { get; }
        public HorizontalAlignmentOptions horizontalAlignment { get; set; }
        public VerticalAlignmentOptions verticalAlignment { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public float characterSpacing { get; set; }
        public float wordSpacing { get; set; }
        public float lineSpacing { get; set; }
        public float paragraphSpacing { get; set; }
        public float characterWidthAdjustment { get; set; }
        public bool enableWordWrapping { get; set; }
        public TextWrappingModes textWrappingMode { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public bool isTextOverflowing { get; }
        public bool enableKerning { get; set; }
        public bool extraPadding { get; set; }
        public bool richText { get; set; }
        public bool parseCtrlCharacters { get; set; }
        public bool isOverlay { get; set; }
        public bool enableVertexGradient { get; set; }
        public VertexGradient colorGradient { get; set; }
        public TMP_ColorGradient colorGradientPreset { get; set; }
        public TMP_SpriteAsset spriteAsset { get; set; }
        public bool tintAllSprites { get; set; }
        public Vector4 margin { get; set; }
        public int maxVisibleCharacters { get; set; }
        public int maxVisibleLines { get; set; }
        public int firstVisibleCharacter { get; set; }
        public bool autoSizeTextContainer { get; set; }
        public Bounds textBounds { get; }
        public Bounds bounds { get; }
        public float preferredWidth { get; }
        public float preferredHeight { get; }
        public float renderedWidth { get; }
        public float renderedHeight { get; }
        public int textLength => throw null;
        public bool havePropertiesChanged { get; set; }
        public virtual void ForceMeshUpdate() => throw null;
        public virtual void ForceMeshUpdate(bool ignoreActiveState) => throw null;
        public virtual void ForceMeshUpdate(bool ignoreActiveState, bool forceTextReparsing) => throw null;
        public void SetText(string text) => throw null;
        public void SetText(string text, bool syncTextInputBox) => throw null;
        public void SetText(string text, float arg0) => throw null;
        public Vector2 GetPreferredValues() => throw null;
        public Vector2 GetPreferredValues(string text) => throw null;
        public Vector2 GetPreferredValues(float width, float height) => throw null;
        public Vector2 GetPreferredValues(string text, float width, float height) => throw null;
        public Vector2 GetRenderedValues() => throw null;
        public Vector2 GetRenderedValues(bool onlyVisibleCharacters) => throw null;
        public void ClearMesh() => throw null;
        public override void SetVerticesDirty() => throw null;
        public override void SetLayoutDirty() => throw null;
        public override void SetMaterialDirty() => throw null;
    }

    public class TextMeshProUGUI : TMP_Text, ILayoutElement
    {
        public TextMeshProUGUI(IntPtr pointer) : base(pointer) { }
        public TextMeshProUGUI() : base(IntPtr.Zero) { }
        public Vector4 maskOffset { get; set; }
        public float minWidth { get; }
        public float minHeight { get; }
        public float flexibleWidth { get; }
        public float flexibleHeight { get; }
        public int layoutPriority { get; }
        public void CalculateLayoutInputHorizontal() => throw null;
        public void CalculateLayoutInputVertical() => throw null;
    }

    /// <summary>The 3D (world-space) TextMeshPro text component.</summary>
    public class TextMeshPro : TMP_Text
    {
        public TextMeshPro(IntPtr pointer) : base(pointer) { }
        public TextMeshPro() : base(IntPtr.Zero) { }
        public Renderer renderer { get; }
        public int sortingLayerID { get; set; }
        public int sortingOrder { get; set; }
    }

    public class TMP_InputField : Selectable
    {
        public enum ContentType { Standard = 0, Autocorrected = 1, IntegerNumber = 2, DecimalNumber = 3, Alphanumeric = 4, Name = 5, EmailAddress = 6, Password = 7, Pin = 8, Custom = 9 }
        public enum InputType { Standard = 0, AutoCorrect = 1, Password = 2 }
        public enum CharacterValidation { None = 0, Digit = 1, Integer = 2, Decimal = 3, Alphanumeric = 4, Name = 5, Regex = 6, EmailAddress = 7, CustomValidator = 8 }
        public enum LineType { SingleLine = 0, MultiLineSubmit = 1, MultiLineNewline = 2 }

        public class SubmitEvent : UnityEvent<string> { public SubmitEvent(IntPtr pointer) : base(pointer) { } public SubmitEvent() : base() { } }
        public class OnChangeEvent : UnityEvent<string> { public OnChangeEvent(IntPtr pointer) : base(pointer) { } public OnChangeEvent() : base() { } }
        public class SelectionEvent : UnityEvent<string> { public SelectionEvent(IntPtr pointer) : base(pointer) { } public SelectionEvent() : base() { } }
        public class TextSelectionEvent : UnityEvent<string, int, int> { public TextSelectionEvent(IntPtr pointer) : base(pointer) { } public TextSelectionEvent() : base() { } }

        public TMP_InputField(IntPtr pointer) : base(pointer) { }
        public TMP_InputField() : base() { }
        public string text { get; set; }
        public bool isFocused { get; }
        public float caretBlinkRate { get; set; }
        public int caretWidth { get; set; }
        public RectTransform textViewport { get; set; }
        public TMP_Text textComponent { get; set; }
        public Graphic placeholder { get; set; }
        public Color caretColor { get; set; }
        public bool customCaretColor { get; set; }
        public Color selectionColor { get; set; }
        public SubmitEvent onEndEdit { get; set; }
        public SubmitEvent onSubmit { get; set; }
        public SelectionEvent onSelect { get; set; }
        public SelectionEvent onDeselect { get; set; }
        public TextSelectionEvent onTextSelection { get; set; }
        public TextSelectionEvent onEndTextSelection { get; set; }
        public OnChangeEvent onValueChanged { get; set; }
        public int characterLimit { get; set; }
        public float pointSize { get; set; }
        public TMP_FontAsset fontAsset { get; set; }
        public bool onFocusSelectAll { get; set; }
        public bool resetOnDeActivation { get; set; }
        public bool restoreOriginalTextOnEscape { get; set; }
        public bool isRichTextEditingAllowed { get; set; }
        public ContentType contentType { get; set; }
        public LineType lineType { get; set; }
        public int lineLimit { get; set; }
        public InputType inputType { get; set; }
        public CharacterValidation characterValidation { get; set; }
        public char asteriskChar { get; set; }
        public bool readOnly { get; set; }
        public bool richText { get; set; }
        public bool hideMobileInput { get; set; }
        public int caretPosition { get; set; }
        public int selectionAnchorPosition { get; set; }
        public int selectionFocusPosition { get; set; }
        public int stringPosition { get; set; }
        public void SetTextWithoutNotify(string input) => throw null;
        public void ActivateInputField() => throw null;
        public void DeactivateInputField() => throw null;
        public void DeactivateInputField(bool clearSelection) => throw null;
        public void MoveTextEnd(bool shift) => throw null;
        public void MoveTextStart(bool shift) => throw null;
        public void ForceLabelUpdate() => throw null;
        public void SelectAll() => throw null;
    }

    public class TMP_Dropdown : Selectable
    {
        public class OptionData : Il2CppSystem.Object
        {
            public OptionData(IntPtr pointer) : base(pointer) { }
            public OptionData() : base(IntPtr.Zero) { }
            public OptionData(string text) : base(IntPtr.Zero) { }
            public OptionData(Sprite image) : base(IntPtr.Zero) { }
            public OptionData(string text, Sprite image) : base(IntPtr.Zero) { }
            public string text { get; set; }
            public Sprite image { get; set; }
        }
        public class DropdownEvent : UnityEvent<int> { public DropdownEvent(IntPtr pointer) : base(pointer) { } public DropdownEvent() : base() { } }

        public TMP_Dropdown(IntPtr pointer) : base(pointer) { }
        public TMP_Dropdown() : base() { }
        public RectTransform template { get; set; }
        public TMP_Text captionText { get; set; }
        public Image captionImage { get; set; }
        public TMP_Text itemText { get; set; }
        public Image itemImage { get; set; }
        public Il2CppSystem.Collections.Generic.List<OptionData> options { get; set; }
        public DropdownEvent onValueChanged { get; set; }
        public float alphaFadeSpeed { get; set; }
        public int value { get; set; }
        public bool IsExpanded { get; }
        public void SetValueWithoutNotify(int input) => throw null;
        public void RefreshShownValue() => throw null;
        public void AddOptions(Il2CppSystem.Collections.Generic.List<OptionData> options) => throw null;
        public void AddOptions(Il2CppSystem.Collections.Generic.List<string> options) => throw null;
        public void ClearOptions() => throw null;
        public void Show() => throw null;
        public void Hide() => throw null;
    }
}
