// STUB: UnityEngine.Events (UnityEvent family). Delegates are Il2Cpp delegate classes; the implicit
// System.Action -> UnityAction conversions are a GUESS at what Il2CppInterop generates (see README).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine.Events
{
    public enum UnityEventCallState { Off = 0, EditorAndRuntime = 1, RuntimeOnly = 2 }

    public class UnityAction : Il2CppSystem.MulticastDelegate
    {
        public UnityAction(IntPtr pointer) : base(pointer) { }
        public UnityAction(Il2CppSystem.Object @object, IntPtr method) : base(IntPtr.Zero) { }
        public void Invoke() => throw null;
        public static implicit operator UnityAction(System.Action action) => throw null;
    }
    public class UnityAction<T0> : Il2CppSystem.MulticastDelegate
    {
        public UnityAction(IntPtr pointer) : base(pointer) { }
        public UnityAction(Il2CppSystem.Object @object, IntPtr method) : base(IntPtr.Zero) { }
        public void Invoke(T0 arg0) => throw null;
        public static implicit operator UnityAction<T0>(System.Action<T0> action) => throw null;
    }
    public class UnityAction<T0, T1> : Il2CppSystem.MulticastDelegate
    {
        public UnityAction(IntPtr pointer) : base(pointer) { }
        public void Invoke(T0 arg0, T1 arg1) => throw null;
        public static implicit operator UnityAction<T0, T1>(System.Action<T0, T1> action) => throw null;
    }
    public class UnityAction<T0, T1, T2> : Il2CppSystem.MulticastDelegate
    {
        public UnityAction(IntPtr pointer) : base(pointer) { }
        public void Invoke(T0 arg0, T1 arg1, T2 arg2) => throw null;
    }

    public abstract class UnityEventBase : Il2CppSystem.Object
    {
        protected UnityEventBase(IntPtr pointer) : base(pointer) { }
        public int GetPersistentEventCount() => throw null;
        public Object GetPersistentTarget(int index) => throw null;
        public string GetPersistentMethodName(int index) => throw null;
        public void SetPersistentListenerState(int index, UnityEventCallState state) => throw null;
        public UnityEventCallState GetPersistentListenerState(int index) => throw null;
        public void RemoveAllListeners() => throw null;
    }

    public class UnityEvent : UnityEventBase
    {
        public UnityEvent(IntPtr pointer) : base(pointer) { }
        public UnityEvent() : base(IntPtr.Zero) { }
        public void AddListener(UnityAction call) => throw null;
        public void RemoveListener(UnityAction call) => throw null;
        public void Invoke() => throw null;
    }
    public class UnityEvent<T0> : UnityEventBase
    {
        public UnityEvent(IntPtr pointer) : base(pointer) { }
        public UnityEvent() : base(IntPtr.Zero) { }
        public void AddListener(UnityAction<T0> call) => throw null;
        public void RemoveListener(UnityAction<T0> call) => throw null;
        public void Invoke(T0 arg0) => throw null;
    }
    public class UnityEvent<T0, T1> : UnityEventBase
    {
        public UnityEvent(IntPtr pointer) : base(pointer) { }
        public UnityEvent() : base(IntPtr.Zero) { }
        public void AddListener(UnityAction<T0, T1> call) => throw null;
        public void RemoveListener(UnityAction<T0, T1> call) => throw null;
        public void Invoke(T0 arg0, T1 arg1) => throw null;
    }
    public class UnityEvent<T0, T1, T2> : UnityEventBase
    {
        public UnityEvent(IntPtr pointer) : base(pointer) { }
        public UnityEvent() : base(IntPtr.Zero) { }
        public void AddListener(UnityAction<T0, T1, T2> call) => throw null;
        public void RemoveListener(UnityAction<T0, T1, T2> call) => throw null;
        public void Invoke(T0 arg0, T1 arg1, T2 arg2) => throw null;
    }
}
