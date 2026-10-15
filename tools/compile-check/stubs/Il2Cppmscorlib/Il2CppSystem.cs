// STUB of Il2Cppmscorlib (the Il2CppInterop-generated mirror of the game's mscorlib). Only what ASL/mod code touches.
using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppSystem
{
    public class Object : Il2CppObjectBase
    {
        public Object(IntPtr pointer) : base(pointer) { }
        public Object() : base(IntPtr.Zero) { }
        /// <summary>GUESS: Il2CppInterop exposes the IL2CPP System.Type of an object under this name.</summary>
        public Type GetIl2CppType() => throw null;
    }

    public abstract class ValueType : Object { protected ValueType(IntPtr pointer) : base(pointer) { } }
    public abstract class Enum : ValueType { protected Enum(IntPtr pointer) : base(pointer) { } }
    public class Void : ValueType { public Void(IntPtr pointer) : base(pointer) { } }
    public class Exception : Object
    {
        public Exception(IntPtr pointer) : base(pointer) { }
        public string Message { get; }
        public string StackTrace { get; }
    }
    public class RuntimeTypeHandle : ValueType { public RuntimeTypeHandle(IntPtr pointer) : base(pointer) { } }
    public abstract class Array : Object { protected Array(IntPtr pointer) : base(pointer) { } public int Length { get; } }

    public class Delegate : Object
    {
        public Delegate(IntPtr pointer) : base(pointer) { }
        public Object Target { get; }
    }
    public class MulticastDelegate : Delegate { public MulticastDelegate(IntPtr pointer) : base(pointer) { } }

    /// <summary>System.Type as seen from IL2CPP (what Il2CppType.Of&lt;T&gt;() returns).</summary>
    public class Type : Object
    {
        public Type(IntPtr pointer) : base(pointer) { }
        public string Name { get; }
        public string FullName { get; }
        public string Namespace { get; }
        public string AssemblyQualifiedName { get; }
        public Type BaseType { get; }
        public bool IsAbstract { get; }
        public bool IsInterface { get; }
        public bool IsValueType { get; }
        public bool IsEnum { get; }
        public bool IsAssignableFrom(Type c) => throw null;
        public bool IsSubclassOf(Type c) => throw null;
        public static Type GetType(string typeName) => throw null;
        public static bool operator ==(Type a, Type b) => throw null;
        public static bool operator !=(Type a, Type b) => throw null;
        public override bool Equals(object o) => throw null;
        public override int GetHashCode() => throw null;
    }

    /// <summary>Non-blittable struct (holds an array reference) -> a class in the interop assemblies.</summary>
    public class ArraySegment<T> : ValueType
    {
        public ArraySegment(IntPtr pointer) : base(pointer) { }
        public ArraySegment(Il2CppArrayBase<T> array) : base(IntPtr.Zero) { }
        public ArraySegment(Il2CppArrayBase<T> array, int offset, int count) : base(IntPtr.Zero) { }
        public Il2CppArrayBase<T> Array { get; }
        public int Offset { get; }
        public int Count { get; }
    }

    public class Action : MulticastDelegate
    {
        public Action(IntPtr pointer) : base(pointer) { }
        public void Invoke() => throw null;
        public static implicit operator Action(System.Action a) => throw null;
    }
    public class Action<T> : MulticastDelegate
    {
        public Action(IntPtr pointer) : base(pointer) { }
        public void Invoke(T arg) => throw null;
        public static implicit operator Action<T>(System.Action<T> a) => throw null;
    }
    public class Func<TResult> : MulticastDelegate
    {
        public Func(IntPtr pointer) : base(pointer) { }
        public TResult Invoke() => throw null;
    }
}

namespace Il2CppSystem.Collections
{
    public interface IEnumerator { bool MoveNext(); void Reset(); Il2CppSystem.Object Current { get; } }
    public interface IEnumerable { IEnumerator GetEnumerator(); }
}

namespace Il2CppSystem.Collections.Generic
{
    public class KeyValuePair<TKey, TValue> : ValueType
    {
        public KeyValuePair(IntPtr pointer) : base(pointer) { }
        public KeyValuePair(TKey key, TValue value) : base(IntPtr.Zero) { }
        public TKey Key { get; }
        public TValue Value { get; }
    }

    public class List<T> : Il2CppSystem.Object, System.Collections.Generic.IList<T>
    {
        public List(IntPtr pointer) : base(pointer) { }
        public List() : base(IntPtr.Zero) { }
        public List(int capacity) : base(IntPtr.Zero) { }
        public int Count { get; }
        public int Capacity { get; set; }
        public T this[int index] { get => throw null; set => throw null; }
        public void Add(T item) => throw null;
        public void AddRange(System.Collections.Generic.IEnumerable<T> collection) => throw null;
        public void Insert(int index, T item) => throw null;
        public bool Remove(T item) => throw null;
        public void RemoveAt(int index) => throw null;
        public void RemoveRange(int index, int count) => throw null;
        public void Clear() => throw null;
        public bool Contains(T item) => throw null;
        public int IndexOf(T item) => throw null;
        public void Reverse() => throw null;
        public Il2CppArrayBase<T> ToArray() => throw null;
        public System.Collections.Generic.IEnumerator<T> GetEnumerator() => throw null;
        bool System.Collections.Generic.ICollection<T>.IsReadOnly => false;
        void System.Collections.Generic.ICollection<T>.CopyTo(T[] array, int arrayIndex) => throw null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw null;
    }

    public class HashSet<T> : Il2CppSystem.Object, System.Collections.Generic.ICollection<T>
    {
        public HashSet(IntPtr pointer) : base(pointer) { }
        public HashSet() : base(IntPtr.Zero) { }
        public int Count { get; }
        public bool Add(T item) => throw null;
        public bool Remove(T item) => throw null;
        public bool Contains(T item) => throw null;
        public void Clear() => throw null;
        public System.Collections.Generic.IEnumerator<T> GetEnumerator() => throw null;
        void System.Collections.Generic.ICollection<T>.Add(T item) => throw null;
        bool System.Collections.Generic.ICollection<T>.IsReadOnly => false;
        void System.Collections.Generic.ICollection<T>.CopyTo(T[] array, int arrayIndex) => throw null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw null;
    }

    public class Queue<T> : Il2CppSystem.Object
    {
        public Queue(IntPtr pointer) : base(pointer) { }
        public Queue() : base(IntPtr.Zero) { }
        public int Count { get; }
        public void Enqueue(T item) => throw null;
        public T Dequeue() => throw null;
        public T Peek() => throw null;
        public void Clear() => throw null;
    }

    public class Dictionary<TKey, TValue> : Il2CppSystem.Object
    {
        public Dictionary(IntPtr pointer) : base(pointer) { }
        public Dictionary() : base(IntPtr.Zero) { }
        public int Count { get; }
        public TValue this[TKey key] { get => throw null; set => throw null; }
        public void Add(TKey key, TValue value) => throw null;
        public bool Remove(TKey key) => throw null;
        public bool ContainsKey(TKey key) => throw null;
        public bool ContainsValue(TValue value) => throw null;
        public bool TryGetValue(TKey key, out TValue value) => throw null;
        public void Clear() => throw null;
        public KeyCollection Keys { get; }
        public ValueCollection Values { get; }
        public Enumerator GetEnumerator() => throw null;

        public class Enumerator : Il2CppSystem.ValueType, IDisposable
        {
            public Enumerator(IntPtr pointer) : base(pointer) { }
            public KeyValuePair<TKey, TValue> Current { get; }
            public bool MoveNext() => throw null;
            public void Dispose() => throw null;
        }
        public class KeyCollection : Il2CppSystem.Object
        {
            public KeyCollection(IntPtr pointer) : base(pointer) { }
            public int Count { get; }
            public System.Collections.Generic.IEnumerator<TKey> GetEnumerator() => throw null;
        }
        public class ValueCollection : Il2CppSystem.Object
        {
            public ValueCollection(IntPtr pointer) : base(pointer) { }
            public int Count { get; }
            public System.Collections.Generic.IEnumerator<TValue> GetEnumerator() => throw null;
        }
    }
}
