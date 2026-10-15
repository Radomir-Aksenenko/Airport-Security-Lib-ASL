// STUB: UnityEngine.EventSystems (shipped in the UnityEngine.UI assembly).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.Events;

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour
    {
        protected UIBehaviour(IntPtr pointer) : base(pointer) { }
        protected UIBehaviour() : base() { }
        public virtual bool IsActive() => throw null;
        public bool IsDestroyed() => throw null;
    }

    public interface IEventSystemHandler { }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
    public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
    public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }
    public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }

    public abstract class AbstractEventData : Il2CppSystem.Object
    {
        protected AbstractEventData(IntPtr pointer) : base(pointer) { }
        public bool used { get; }
        public virtual void Reset() => throw null;
        public virtual void Use() => throw null;
    }

    public class BaseEventData : AbstractEventData
    {
        public BaseEventData(IntPtr pointer) : base(pointer) { }
        public BaseEventData(EventSystem eventSystem) : base(IntPtr.Zero) { }
        public BaseInputModule currentInputModule { get; }
        public GameObject selectedObject { get; set; }
    }

    /// <summary>Non-blittable struct (holds references) -> a class in the interop output.</summary>
    public class RaycastResult : Il2CppSystem.ValueType
    {
        public RaycastResult(IntPtr pointer) : base(pointer) { }
        public GameObject gameObject { get; set; }
        public BaseRaycaster module { get; set; }
        public float distance { get; set; }
        public float index { get; set; }
        public int depth { get; set; }
        public int sortingLayer { get; set; }
        public int sortingOrder { get; set; }
        public Vector3 worldPosition { get; set; }
        public Vector3 worldNormal { get; set; }
        public Vector2 screenPosition { get; set; }
        public bool isValid { get; }
        public void Clear() => throw null;
    }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left = 0, Right = 1, Middle = 2 }
        public enum FramePressState { Pressed = 0, Released = 1, PressedAndReleased = 2, NotChanged = 3 }

        public PointerEventData(IntPtr pointer) : base(pointer) { }
        public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }
        public GameObject pointerEnter { get; set; }
        public GameObject lastPress { get; }
        public GameObject rawPointerPress { get; set; }
        public GameObject pointerDrag { get; set; }
        public GameObject pointerClick { get; set; }
        public GameObject pointerPress { get; set; }
        public RaycastResult pointerCurrentRaycast { get; set; }
        public RaycastResult pointerPressRaycast { get; set; }
        public Il2CppSystem.Collections.Generic.List<GameObject> hovered { get; }
        public bool eligibleForClick { get; set; }
        public int pointerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }
        public Vector3 worldPosition { get; set; }
        public Vector3 worldNormal { get; set; }
        public float clickTime { get; set; }
        public int clickCount { get; set; }
        public Vector2 scrollDelta { get; set; }
        public bool useDragThreshold { get; set; }
        public bool dragging { get; set; }
        public InputButton button { get; set; }
        public Camera enterEventCamera { get; }
        public Camera pressEventCamera { get; }
        public bool IsPointerMoving() => throw null;
        public bool IsScrolling() => throw null;
    }

    public abstract class BaseRaycaster : UIBehaviour
    {
        protected BaseRaycaster(IntPtr pointer) : base(pointer) { }
        public abstract Camera eventCamera { get; }
        public virtual int sortOrderPriority { get; }
        public virtual int renderOrderPriority { get; }
        public abstract void Raycast(PointerEventData eventData, Il2CppSystem.Collections.Generic.List<RaycastResult> resultAppendList);
    }

    public abstract class BaseInputModule : UIBehaviour
    {
        protected BaseInputModule(IntPtr pointer) : base(pointer) { }
        public virtual bool IsModuleSupported() => throw null;
        public virtual bool ShouldActivateModule() => throw null;
        public virtual void Process() => throw null;
    }

    public abstract class PointerInputModule : BaseInputModule
    {
        protected PointerInputModule(IntPtr pointer) : base(pointer) { }
        public const int kMouseLeftId = -1;
        public const int kMouseRightId = -2;
        public const int kMouseMiddleId = -3;
    }

    public class StandaloneInputModule : PointerInputModule
    {
        public StandaloneInputModule(IntPtr pointer) : base(pointer) { }
        public StandaloneInputModule() : base(IntPtr.Zero) { }
        public string horizontalAxis { get; set; }
        public string verticalAxis { get; set; }
        public string submitButton { get; set; }
        public string cancelButton { get; set; }
        public float inputActionsPerSecond { get; set; }
        public bool forceModuleActive { get; set; }
    }

    public class EventSystem : UIBehaviour
    {
        public EventSystem(IntPtr pointer) : base(pointer) { }
        public EventSystem() : base(IntPtr.Zero) { }
        public static EventSystem current { get; set; }
        public bool sendNavigationEvents { get; set; }
        public int pixelDragThreshold { get; set; }
        public BaseInputModule currentInputModule { get; }
        public GameObject firstSelectedGameObject { get; set; }
        public GameObject currentSelectedGameObject { get; }
        public bool alreadySelecting { get; }
        public void SetSelectedGameObject(GameObject selected) => throw null;
        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer) => throw null;
        public bool IsPointerOverGameObject() => throw null;
        public bool IsPointerOverGameObject(int pointerId) => throw null;
        public void RaycastAll(PointerEventData eventData, Il2CppSystem.Collections.Generic.List<RaycastResult> raycastResults) => throw null;
        public void UpdateModules() => throw null;
    }

    public enum EventTriggerType
    {
        PointerEnter = 0, PointerExit = 1, PointerDown = 2, PointerUp = 3, PointerClick = 4, Drag = 5, Drop = 6, Scroll = 7,
        UpdateSelected = 8, Select = 9, Deselect = 10, Move = 11, InitializePotentialDrag = 12, BeginDrag = 13, EndDrag = 14,
        Submit = 15, Cancel = 16
    }

    public class EventTrigger : MonoBehaviour
    {
        public EventTrigger(IntPtr pointer) : base(pointer) { }
        public class TriggerEvent : UnityEvent<BaseEventData>
        {
            public TriggerEvent(IntPtr pointer) : base(pointer) { }
            public TriggerEvent() : base() { }
        }
        public class Entry : Il2CppSystem.Object
        {
            public Entry(IntPtr pointer) : base(pointer) { }
            public Entry() : base(IntPtr.Zero) { }
            public EventTriggerType eventID { get; set; }
            public TriggerEvent callback { get; set; }
        }
        public Il2CppSystem.Collections.Generic.List<Entry> triggers { get; set; }
    }
}
