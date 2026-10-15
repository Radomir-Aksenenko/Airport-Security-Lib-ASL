// STUB: UnityEngine.UIModule (Canvas, CanvasGroup, RectTransformUtility ...).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public enum RenderMode { ScreenSpaceOverlay = 0, ScreenSpaceCamera = 1, WorldSpace = 2 }
    [Flags] public enum AdditionalCanvasShaderChannels { None = 0, TexCoord1 = 1, TexCoord2 = 2, TexCoord3 = 4, Normal = 8, Tangent = 16 }

    public class Canvas : Behaviour
    {
        public Canvas(IntPtr pointer) : base(pointer) { }
        public Canvas() : base() { }
        public RenderMode renderMode { get; set; }
        public bool isRootCanvas { get; }
        public Canvas rootCanvas { get; }
        public Camera worldCamera { get; set; }
        public float scaleFactor { get; set; }
        public float referencePixelsPerUnit { get; set; }
        public bool overridePixelPerfect { get; set; }
        public bool pixelPerfect { get; set; }
        public float planeDistance { get; set; }
        public int sortingOrder { get; set; }
        public bool overrideSorting { get; set; }
        public int sortingLayerID { get; set; }
        public string sortingLayerName { get; set; }
        public int renderOrder { get; }
        public int targetDisplay { get; set; }
        public bool additionalShaderChannels { get; set; }
        public Rect pixelRect { get; }
        public static void ForceUpdateCanvases() => throw null;
        public static Material GetDefaultCanvasMaterial() => throw null;
    }

    public class CanvasGroup : Behaviour
    {
        public CanvasGroup(IntPtr pointer) : base(pointer) { }
        public CanvasGroup() : base() { }
        public float alpha { get; set; }
        public bool interactable { get; set; }
        public bool blocksRaycasts { get; set; }
        public bool ignoreParentGroups { get; set; }
    }

    public class CanvasRenderer : Component
    {
        public CanvasRenderer(IntPtr pointer) : base(pointer) { }
        public bool cull { get; set; }
        public void SetColor(Color color) => throw null;
        public Color GetColor() => throw null;
        public void SetAlpha(float alpha) => throw null;
        public float GetAlpha() => throw null;
        public void SetMaterial(Material material, int index) => throw null;
        public void Clear() => throw null;
    }

    public static class RectTransformUtility
    {
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint) => throw null;
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => throw null;
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint) => throw null;
        public static bool ScreenPointToWorldPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector3 worldPoint) => throw null;
        public static Vector2 WorldToScreenPoint(Camera cam, Vector3 worldPoint) => throw null;
        public static Ray ScreenPointToRay(Camera cam, Vector2 screenPos) => throw null;
        public static Rect PixelAdjustRect(RectTransform rectTransform, Canvas canvas) => throw null;
        public static Vector2 PixelAdjustPoint(Vector2 point, Transform elementTransform, Canvas canvas) => throw null;
        public static Bounds CalculateRelativeRectTransformBounds(Transform root, Transform child) => throw null;
        public static void FlipLayoutOnAxis(RectTransform rect, int axis, bool keepPositioning, bool recursive) => throw null;
    }
}
