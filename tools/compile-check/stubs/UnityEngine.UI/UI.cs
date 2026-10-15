// STUB: UnityEngine.UI (uGUI) - Graphic / Image / Button / layout groups / ScrollRect ...
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    // ------------------------------------------------------------------ graphics

    public abstract class Graphic : UIBehaviour
    {
        protected Graphic(IntPtr pointer) : base(pointer) { }
        protected Graphic() : base() { }
        public static Material defaultGraphicMaterial { get; }
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public Vector4 raycastPadding { get; set; }
        public int depth { get; }
        public RectTransform rectTransform { get; }
        public Canvas canvas { get; }
        public CanvasRenderer canvasRenderer { get; }
        public virtual Material defaultMaterial { get; }
        public virtual Material material { get; set; }
        public virtual Material materialForRendering { get; }
        public virtual Texture mainTexture { get; }
        public virtual void SetAllDirty() => throw null;
        public virtual void SetLayoutDirty() => throw null;
        public virtual void SetVerticesDirty() => throw null;
        public virtual void SetMaterialDirty() => throw null;
        public virtual void Rebuild(CanvasUpdate update) => throw null;
        public virtual void SetNativeSize() => throw null;
        public virtual bool Raycast(Vector2 sp, Camera eventCamera) => throw null;
        public Vector2 PixelAdjustPoint(Vector2 point) => throw null;
        public Rect GetPixelAdjustedRect() => throw null;
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha) => throw null;
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) => throw null;
        public void RegisterDirtyLayoutCallback(UnityAction action) => throw null;
        public void UnregisterDirtyLayoutCallback(UnityAction action) => throw null;
    }

    public enum CanvasUpdate { Prelayout = 0, Layout = 1, PostLayout = 2, PreRender = 3, LatePreRender = 4, MaxUpdateValue = 5 }

    public abstract class MaskableGraphic : Graphic
    {
        public class CullStateChangedEvent : UnityEvent<bool>
        {
            public CullStateChangedEvent(IntPtr pointer) : base(pointer) { }
            public CullStateChangedEvent() : base() { }
        }
        protected MaskableGraphic(IntPtr pointer) : base(pointer) { }
        protected MaskableGraphic() : base() { }
        public CullStateChangedEvent onCullStateChanged { get; set; }
        public bool maskable { get; set; }
        public bool isMaskingGraphic { get; set; }
        public virtual void RecalculateClipping() => throw null;
        public virtual void RecalculateMasking() => throw null;
        public virtual void Cull(Rect clipRect, bool validRect) => throw null;
        public virtual void SetClipRect(Rect clipRect, bool validRect) => throw null;
    }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple = 0, Sliced = 1, Tiled = 2, Filled = 3 }
        public enum FillMethod { Horizontal = 0, Vertical = 1, Radial90 = 2, Radial180 = 3, Radial360 = 4 }
        public enum OriginHorizontal { Left = 0, Right = 1 }
        public enum OriginVertical { Bottom = 0, Top = 1 }
        public enum Origin90 { BottomLeft = 0, TopLeft = 1, TopRight = 2, BottomRight = 3 }
        public enum Origin180 { Bottom = 0, Left = 1, Top = 2, Right = 3 }
        public enum Origin360 { Bottom = 0, Right = 1, Top = 2, Left = 3 }

        public Image(IntPtr pointer) : base(pointer) { }
        public Image() : base() { }
        public Sprite sprite { get; set; }
        public Sprite overrideSprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public bool fillCenter { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public bool fillClockwise { get; set; }
        public int fillOrigin { get; set; }
        public float eventAlphaThreshold { get; set; }
        public float alphaHitTestMinimumThreshold { get; set; }
        public bool useSpriteMesh { get; set; }
        public float pixelsPerUnit { get; }
        public float pixelsPerUnitMultiplier { get; set; }
        public bool hasBorder { get; }
        public float flexibleWidth { get; }
        public float flexibleHeight { get; }
        public float minWidth { get; }
        public float minHeight { get; }
        public float preferredWidth { get; }
        public float preferredHeight { get; }
        public static Material defaultETC1GraphicMaterial { get; }
        public override void SetNativeSize() => throw null;
        public void OnBeforeSerialize() => throw null;
        public void OnAfterDeserialize() => throw null;
    }

    public class RawImage : MaskableGraphic
    {
        public RawImage(IntPtr pointer) : base(pointer) { }
        public RawImage() : base() { }
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
        public override void SetNativeSize() => throw null;
    }

    /// <summary>Legacy (non-TMP) UI text.</summary>
    public class Text : MaskableGraphic
    {
        public Text(IntPtr pointer) : base(pointer) { }
        public Text() : base() { }
        public virtual string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
        public float lineSpacing { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public float pixelsPerUnit { get; }
        public float preferredWidth { get; }
        public float preferredHeight { get; }
    }

    public abstract class BaseMeshEffect : UIBehaviour
    {
        protected BaseMeshEffect(IntPtr pointer) : base(pointer) { }
    }

    public class Shadow : BaseMeshEffect
    {
        public Shadow(IntPtr pointer) : base(pointer) { }
        public Shadow() : base(IntPtr.Zero) { }
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
        public bool useGraphicAlpha { get; set; }
    }

    public class Outline : Shadow
    {
        public Outline(IntPtr pointer) : base(pointer) { }
        public Outline() : base() { }
    }

    public class Mask : UIBehaviour
    {
        public Mask(IntPtr pointer) : base(pointer) { }
        public Mask() : base() { }
        public RectTransform rectTransform { get; }
        public bool showMaskGraphic { get; set; }
        public Graphic graphic { get; }
    }

    public class RectMask2D : UIBehaviour
    {
        public RectMask2D(IntPtr pointer) : base(pointer) { }
        public RectMask2D() : base() { }
        public Vector4 padding { get; set; }
        public Vector2Int softness { get; set; }
        public Rect canvasRect { get; }
        public RectTransform rectTransform { get; }
    }

    // ------------------------------------------------------------------ selectables

    public struct ColorBlock : IEquatable<ColorBlock>
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; }
        public float fadeDuration { get; set; }
        public static ColorBlock defaultColorBlock => throw null;
        public bool Equals(ColorBlock other) => throw null;
    }

    public struct SpriteState : IEquatable<SpriteState>
    {
        public Sprite highlightedSprite { get => throw null; set => throw null; }
        public Sprite pressedSprite { get => throw null; set => throw null; }
        public Sprite selectedSprite { get => throw null; set => throw null; }
        public Sprite disabledSprite { get => throw null; set => throw null; }
        public bool Equals(SpriteState other) => throw null;
    }

    public struct Navigation : IEquatable<Navigation>
    {
        [Flags] public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 }
        public Mode mode { get; set; }
        public bool wrapAround { get; set; }
        public static Navigation defaultNavigation => throw null;
        public bool Equals(Navigation other) => throw null;
    }

    public class AnimationTriggers : Il2CppSystem.Object
    {
        public AnimationTriggers(IntPtr pointer) : base(pointer) { }
        public string normalTrigger { get; set; }
        public string highlightedTrigger { get; set; }
        public string pressedTrigger { get; set; }
        public string selectedTrigger { get; set; }
        public string disabledTrigger { get; set; }
    }

    public class Selectable : UIBehaviour
    {
        public enum Transition { None = 0, ColorTint = 1, SpriteSwap = 2, Animation = 3 }
        public Selectable(IntPtr pointer) : base(pointer) { }
        public Selectable() : base() { }
        public bool interactable { get; set; }
        public Transition transition { get; set; }
        public ColorBlock colors { get; set; }
        public SpriteState spriteState { get; set; }
        public AnimationTriggers animationTriggers { get; set; }
        public Graphic targetGraphic { get; set; }
        public Navigation navigation { get; set; }
        public Image image { get; set; }
        public Selectable FindSelectableOnDown() => throw null;
        public Selectable FindSelectableOnUp() => throw null;
        public Selectable FindSelectableOnLeft() => throw null;
        public Selectable FindSelectableOnRight() => throw null;
        public virtual bool IsInteractable() => throw null;
        public virtual void Select() => throw null;
        public static int allSelectableCount { get; }
    }

    public class Button : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public class ButtonClickedEvent : UnityEvent
        {
            public ButtonClickedEvent(IntPtr pointer) : base(pointer) { }
            public ButtonClickedEvent() : base() { }
        }
        public Button(IntPtr pointer) : base(pointer) { }
        public Button() : base() { }
        public ButtonClickedEvent onClick { get; set; }
        public virtual void OnPointerClick(PointerEventData eventData) => throw null;
        public virtual void OnSubmit(BaseEventData eventData) => throw null;
    }

    public class ToggleGroup : UIBehaviour
    {
        public ToggleGroup(IntPtr pointer) : base(pointer) { }
        public ToggleGroup() : base() { }
        public bool allowSwitchOff { get; set; }
        public bool AnyTogglesOn() => throw null;
        public void SetAllTogglesOff() => throw null;
        public void SetAllTogglesOff(bool sendCallback) => throw null;
        public Toggle GetFirstActiveToggle() => throw null;
        public void NotifyToggleOn(Toggle toggle) => throw null;
        public void NotifyToggleOn(Toggle toggle, bool sendCallback) => throw null;
        public void RegisterToggle(Toggle toggle) => throw null;
        public void UnregisterToggle(Toggle toggle) => throw null;
    }

    public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public enum ToggleTransition { None = 0, Fade = 1 }
        public class ToggleEvent : UnityEvent<bool>
        {
            public ToggleEvent(IntPtr pointer) : base(pointer) { }
            public ToggleEvent() : base() { }
        }
        public Toggle(IntPtr pointer) : base(pointer) { }
        public Toggle() : base() { }
        public ToggleTransition toggleTransition { get; set; }
        public Graphic graphic { get; set; }
        public ToggleGroup group { get; set; }
        public ToggleEvent onValueChanged { get; set; }
        public bool isOn { get; set; }
        public void SetIsOnWithoutNotify(bool value) => throw null;
        public virtual void OnPointerClick(PointerEventData eventData) => throw null;
        public virtual void OnSubmit(BaseEventData eventData) => throw null;
    }

    public class Slider : Selectable
    {
        public enum Direction { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }
        public class SliderEvent : UnityEvent<float>
        {
            public SliderEvent(IntPtr pointer) : base(pointer) { }
            public SliderEvent() : base() { }
        }
        public Slider(IntPtr pointer) : base(pointer) { }
        public Slider() : base() { }
        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public Direction direction { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public bool wholeNumbers { get; set; }
        public float value { get; set; }
        public float normalizedValue { get; set; }
        public SliderEvent onValueChanged { get; set; }
        public void SetValueWithoutNotify(float input) => throw null;
        public void SetDirection(Direction direction, bool includeRectLayouts) => throw null;
    }

    public class Scrollbar : Selectable
    {
        public enum Direction { LeftToRight = 0, RightToLeft = 1, BottomToTop = 2, TopToBottom = 3 }
        public class ScrollEvent : UnityEvent<float>
        {
            public ScrollEvent(IntPtr pointer) : base(pointer) { }
            public ScrollEvent() : base() { }
        }
        public Scrollbar(IntPtr pointer) : base(pointer) { }
        public Scrollbar() : base() { }
        public RectTransform handleRect { get; set; }
        public Direction direction { get; set; }
        public float value { get; set; }
        public float size { get; set; }
        public int numberOfSteps { get; set; }
        public ScrollEvent onValueChanged { get; set; }
        public void SetValueWithoutNotify(float input) => throw null;
        public void SetDirection(Direction direction, bool includeRectLayouts) => throw null;
    }

    public class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted = 0, Elastic = 1, Clamped = 2 }
        public enum ScrollbarVisibility { Permanent = 0, AutoHide = 1, AutoHideAndExpandViewport = 2 }
        public class ScrollRectEvent : UnityEvent<Vector2>
        {
            public ScrollRectEvent(IntPtr pointer) : base(pointer) { }
            public ScrollRectEvent() : base() { }
        }
        public ScrollRect(IntPtr pointer) : base(pointer) { }
        public ScrollRect() : base() { }
        public RectTransform content { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float elasticity { get; set; }
        public bool inertia { get; set; }
        public float decelerationRate { get; set; }
        public float scrollSensitivity { get; set; }
        public RectTransform viewport { get; set; }
        public Scrollbar horizontalScrollbar { get; set; }
        public Scrollbar verticalScrollbar { get; set; }
        public ScrollbarVisibility horizontalScrollbarVisibility { get; set; }
        public ScrollbarVisibility verticalScrollbarVisibility { get; set; }
        public float horizontalScrollbarSpacing { get; set; }
        public float verticalScrollbarSpacing { get; set; }
        public ScrollRectEvent onValueChanged { get; set; }
        public Vector2 velocity { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public float horizontalNormalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public void StopMovement() => throw null;
        public void SetNormalizedPosition(float value, int axis) => throw null;
        public float flexibleWidth { get; }
        public float flexibleHeight { get; }
    }

    // ------------------------------------------------------------------ canvas helpers

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize = 0, ScaleWithScreenSize = 1, ConstantPhysicalSize = 2 }
        public enum ScreenMatchMode { MatchWidthOrHeight = 0, Expand = 1, Shrink = 2 }
        public enum Unit { Centimeters = 0, Millimeters = 1, Inches = 2, Points = 3, Picas = 4 }
        public CanvasScaler(IntPtr pointer) : base(pointer) { }
        public CanvasScaler() : base() { }
        public ScaleMode uiScaleMode { get; set; }
        public float referencePixelsPerUnit { get; set; }
        public float scaleFactor { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public Unit physicalUnit { get; set; }
        public float fallbackScreenDPI { get; set; }
        public float defaultSpriteDPI { get; set; }
        public float dynamicPixelsPerUnit { get; set; }
    }

    public class GraphicRaycaster : BaseRaycaster
    {
        public enum BlockingObjects { None = 0, TwoD = 1, ThreeD = 2, All = 3 }
        public GraphicRaycaster(IntPtr pointer) : base(pointer) { }
        public GraphicRaycaster() : base(IntPtr.Zero) { }
        public override Camera eventCamera { get; }
        public bool ignoreReversedGraphics { get; set; }
        public BlockingObjects blockingObjects { get; set; }
        public LayerMask blockingMask { get; set; }
        public override void Raycast(PointerEventData eventData, Il2CppSystem.Collections.Generic.List<RaycastResult> resultAppendList) => throw null;
    }

    public class AspectRatioFitter : UIBehaviour
    {
        public enum AspectMode { None = 0, WidthControlsHeight = 1, HeightControlsWidth = 2, FitInParent = 3, EnvelopeParent = 4 }
        public AspectRatioFitter(IntPtr pointer) : base(pointer) { }
        public AspectMode aspectMode { get; set; }
        public float aspectRatio { get; set; }
    }

    // ------------------------------------------------------------------ layout

    public interface ILayoutElement
    {
        void CalculateLayoutInputHorizontal();
        void CalculateLayoutInputVertical();
        float minWidth { get; }
        float preferredWidth { get; }
        float flexibleWidth { get; }
        float minHeight { get; }
        float preferredHeight { get; }
        float flexibleHeight { get; }
        int layoutPriority { get; }
    }
    public interface ILayoutController { void SetLayoutHorizontal(); void SetLayoutVertical(); }
    public interface ILayoutGroup : ILayoutController { }

    public class LayoutElement : UIBehaviour, ILayoutElement
    {
        public LayoutElement(IntPtr pointer) : base(pointer) { }
        public LayoutElement() : base() { }
        public bool ignoreLayout { get; set; }
        public virtual float minWidth { get; set; }
        public virtual float minHeight { get; set; }
        public virtual float preferredWidth { get; set; }
        public virtual float preferredHeight { get; set; }
        public virtual float flexibleWidth { get; set; }
        public virtual float flexibleHeight { get; set; }
        public virtual int layoutPriority { get; set; }
        public virtual void CalculateLayoutInputHorizontal() => throw null;
        public virtual void CalculateLayoutInputVertical() => throw null;
    }

    public class ContentSizeFitter : UIBehaviour, ILayoutController
    {
        public enum FitMode { Unconstrained = 0, MinSize = 1, PreferredSize = 2 }
        public ContentSizeFitter(IntPtr pointer) : base(pointer) { }
        public ContentSizeFitter() : base() { }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
        public virtual void SetLayoutHorizontal() => throw null;
        public virtual void SetLayoutVertical() => throw null;
    }

    public abstract class LayoutGroup : UIBehaviour, ILayoutElement, ILayoutGroup
    {
        protected LayoutGroup(IntPtr pointer) : base(pointer) { }
        protected LayoutGroup() : base() { }
        public RectOffset padding { get; set; }
        public TextAnchor childAlignment { get; set; }
        public RectTransform rectTransform { get; }
        public virtual float minWidth { get; }
        public virtual float preferredWidth { get; }
        public virtual float flexibleWidth { get; }
        public virtual float minHeight { get; }
        public virtual float preferredHeight { get; }
        public virtual float flexibleHeight { get; }
        public virtual int layoutPriority { get; }
        public virtual void CalculateLayoutInputHorizontal() => throw null;
        public abstract void CalculateLayoutInputVertical();
        public abstract void SetLayoutHorizontal();
        public abstract void SetLayoutVertical();
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        protected HorizontalOrVerticalLayoutGroup(IntPtr pointer) : base(pointer) { }
        protected HorizontalOrVerticalLayoutGroup() : base() { }
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childScaleWidth { get; set; }
        public bool childScaleHeight { get; set; }
        public bool reverseArrangement { get; set; }
    }

    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        public HorizontalLayoutGroup(IntPtr pointer) : base(pointer) { }
        public HorizontalLayoutGroup() : base() { }
        public override void CalculateLayoutInputVertical() => throw null;
        public override void SetLayoutHorizontal() => throw null;
        public override void SetLayoutVertical() => throw null;
    }

    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        public VerticalLayoutGroup(IntPtr pointer) : base(pointer) { }
        public VerticalLayoutGroup() : base() { }
        public override void CalculateLayoutInputVertical() => throw null;
        public override void SetLayoutHorizontal() => throw null;
        public override void SetLayoutVertical() => throw null;
    }

    public class GridLayoutGroup : LayoutGroup
    {
        public enum Corner { UpperLeft = 0, UpperRight = 1, LowerLeft = 2, LowerRight = 3 }
        public enum Axis { Horizontal = 0, Vertical = 1 }
        public enum Constraint { Flexible = 0, FixedColumnCount = 1, FixedRowCount = 2 }
        public GridLayoutGroup(IntPtr pointer) : base(pointer) { }
        public GridLayoutGroup() : base() { }
        public Corner startCorner { get; set; }
        public Axis startAxis { get; set; }
        public Vector2 cellSize { get; set; }
        public Vector2 spacing { get; set; }
        public Constraint constraint { get; set; }
        public int constraintCount { get; set; }
        public override void CalculateLayoutInputVertical() => throw null;
        public override void SetLayoutHorizontal() => throw null;
        public override void SetLayoutVertical() => throw null;
    }

    public class LayoutRebuilder : Il2CppSystem.Object
    {
        public LayoutRebuilder(IntPtr pointer) : base(pointer) { }
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) => throw null;
        public static void MarkLayoutForRebuild(RectTransform rect) => throw null;
    }

    public static class LayoutUtility
    {
        public static float GetMinSize(RectTransform rect, int axis) => throw null;
        public static float GetPreferredSize(RectTransform rect, int axis) => throw null;
        public static float GetFlexibleSize(RectTransform rect, int axis) => throw null;
        public static float GetMinWidth(RectTransform rect) => throw null;
        public static float GetPreferredWidth(RectTransform rect) => throw null;
        public static float GetFlexibleWidth(RectTransform rect) => throw null;
        public static float GetMinHeight(RectTransform rect) => throw null;
        public static float GetPreferredHeight(RectTransform rect) => throw null;
        public static float GetFlexibleHeight(RectTransform rect) => throw null;
    }
}
