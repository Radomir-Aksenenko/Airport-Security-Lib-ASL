// STUB: UnityEngine.TextRenderingModule (TextAnchor, FontStyle, Font ...).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace UnityEngine
{
    public enum TextAnchor { UpperLeft = 0, UpperCenter = 1, UpperRight = 2, MiddleLeft = 3, MiddleCenter = 4, MiddleRight = 5, LowerLeft = 6, LowerCenter = 7, LowerRight = 8 }
    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }
    public enum HorizontalWrapMode { Wrap = 0, Overflow = 1 }
    public enum VerticalWrapMode { Truncate = 0, Overflow = 1 }
    public enum TextGenerationError { None = 0, CustomSizeOnNonDynamicFont = 1, CustomStyleOnNonDynamicFont = 2 }

    public class Font : Object
    {
        public Font(IntPtr pointer) : base(pointer) { }
        public Font(string name) : base(IntPtr.Zero) { }
        public static Font CreateDynamicFontFromOSFont(string fontname, int size) => throw null;
        public static Il2CppStringArray GetOSInstalledFontNames() => throw null;
        public Material material { get; set; }
        public int fontSize { get; }
        public bool dynamic { get; }
    }
}
