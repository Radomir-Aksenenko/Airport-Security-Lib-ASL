// STUB: UnityEngine.IMGUIModule (RectOffset lives here in the real module too).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public enum EventType { MouseDown = 0, MouseUp = 1, MouseMove = 2, MouseDrag = 3, KeyDown = 4, KeyUp = 5, ScrollWheel = 6, Repaint = 7, Layout = 8, DragUpdated = 9, DragPerform = 10, Ignore = 11, Used = 12 }
    public enum ImagePosition { ImageLeft = 0, ImageAbove = 1, ImageOnly = 2, TextOnly = 3 }

    public class RectOffset : Il2CppSystem.Object
    {
        public RectOffset(IntPtr pointer) : base(pointer) { }
        public RectOffset() : base(IntPtr.Zero) { }
        public RectOffset(int left, int right, int top, int bottom) : base(IntPtr.Zero) { }
        public int left { get; set; }
        public int right { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
        public int horizontal { get; }
        public int vertical { get; }
        public Rect Add(Rect rect) => throw null;
        public Rect Remove(Rect rect) => throw null;
    }

    public class GUIContent : Il2CppSystem.Object
    {
        public GUIContent(IntPtr pointer) : base(pointer) { }
        public GUIContent() : base(IntPtr.Zero) { }
        public GUIContent(string text) : base(IntPtr.Zero) { }
        public GUIContent(string text, string tooltip) : base(IntPtr.Zero) { }
        public GUIContent(Texture image) : base(IntPtr.Zero) { }
        public string text { get; set; }
        public string tooltip { get; set; }
        public Texture image { get; set; }
    }

    public class GUIStyleState : Il2CppSystem.Object
    {
        public GUIStyleState(IntPtr pointer) : base(pointer) { }
        public Color textColor { get; set; }
        public Texture2D background { get; set; }
    }

    public class GUIStyle : Il2CppSystem.Object
    {
        public GUIStyle(IntPtr pointer) : base(pointer) { }
        public GUIStyle() : base(IntPtr.Zero) { }
        public GUIStyle(GUIStyle other) : base(IntPtr.Zero) { }
        public string name { get; set; }
        public GUIStyleState normal { get; set; }
        public GUIStyleState hover { get; set; }
        public GUIStyleState active { get; set; }
        public RectOffset padding { get; set; }
        public RectOffset margin { get; set; }
        public RectOffset border { get; set; }
        public TextAnchor alignment { get; set; }
        public bool wordWrap { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public float fixedWidth { get; set; }
        public float fixedHeight { get; set; }
        public bool stretchWidth { get; set; }
        public bool stretchHeight { get; set; }
        public Font font { get; set; }
        public bool richText { get; set; }
        public Vector2 CalcSize(GUIContent content) => throw null;
    }

    public class GUISkin : ScriptableObject
    {
        public GUISkin(IntPtr pointer) : base(pointer) { }
        public GUIStyle label { get; set; }
        public GUIStyle button { get; set; }
        public GUIStyle box { get; set; }
        public GUIStyle window { get; set; }
        public GUIStyle textField { get; set; }
        public GUIStyle toggle { get; set; }
    }

    public class GUILayoutOption : Il2CppSystem.Object { public GUILayoutOption(IntPtr pointer) : base(pointer) { } }

    public class Event : Il2CppSystem.Object
    {
        public Event(IntPtr pointer) : base(pointer) { }
        public static Event current { get; }
        public EventType type { get; set; }
        public KeyCode keyCode { get; set; }
        public char character { get; set; }
        public Vector2 mousePosition { get; set; }
        public int button { get; set; }
        public bool shift { get; }
        public bool control { get; }
        public bool alt { get; }
        public bool isMouse { get; }
        public bool isKey { get; }
        public void Use() => throw null;
    }

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; }
        public static int hotControl { get; set; }
        public static int GetControlID(FocusType focus) => throw null;
    }
    public enum FocusType { Native = 0, Keyboard = 1, Passive = 2 }

    public static class GUI
    {
        public delegate void WindowFunctionDelegate(int id);
        public static Color color { get; set; }
        public static Color backgroundColor { get; set; }
        public static Color contentColor { get; set; }
        public static bool enabled { get; set; }
        public static int depth { get; set; }
        public static GUISkin skin { get; set; }
        public static string tooltip { get; set; }
        public static void Label(Rect position, string text) => throw null;
        public static void Label(Rect position, GUIContent content) => throw null;
        public static void Label(Rect position, string text, GUIStyle style) => throw null;
        public static void Box(Rect position, string text) => throw null;
        public static void Box(Rect position, GUIContent content) => throw null;
        public static bool Button(Rect position, string text) => throw null;
        public static bool Button(Rect position, string text, GUIStyle style) => throw null;
        public static bool Button(Rect position, GUIContent content) => throw null;
        public static bool Toggle(Rect position, bool value, string text) => throw null;
        public static string TextField(Rect position, string text) => throw null;
        public static string TextArea(Rect position, string text) => throw null;
        public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue) => throw null;
        public static void DrawTexture(Rect position, Texture image) => throw null;
        public static void BeginGroup(Rect position) => throw null;
        public static void EndGroup() => throw null;
        public static void FocusControl(string name) => throw null;
        public static void SetNextControlName(string name) => throw null;
        public static string GetNameOfFocusedControl() => throw null;
    }

    public static class GUILayout
    {
        public static void Label(string text, Il2CppReferenceArray<GUILayoutOption> options) => throw null;
        public static bool Button(string text, Il2CppReferenceArray<GUILayoutOption> options) => throw null;
        public static void BeginHorizontal(Il2CppReferenceArray<GUILayoutOption> options) => throw null;
        public static void EndHorizontal() => throw null;
        public static void BeginVertical(Il2CppReferenceArray<GUILayoutOption> options) => throw null;
        public static void EndVertical() => throw null;
        public static void Space(float pixels) => throw null;
        public static void FlexibleSpace() => throw null;
        public static GUILayoutOption Width(float width) => throw null;
        public static GUILayoutOption Height(float height) => throw null;
        public static GUILayoutOption ExpandWidth(bool expand) => throw null;
        public static GUILayoutOption ExpandHeight(bool expand) => throw null;
    }
}
