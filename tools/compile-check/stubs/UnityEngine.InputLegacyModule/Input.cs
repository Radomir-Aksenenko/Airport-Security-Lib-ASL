// STUB: UnityEngine.InputLegacyModule (legacy Input manager).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public static class Input
    {
        public static bool anyKey { get; }
        public static bool anyKeyDown { get; }
        public static string inputString { get; }
        public static Vector3 mousePosition { get; }
        public static Vector2 mouseScrollDelta { get; }
        public static bool mousePresent { get; }
        public static int touchCount { get; }
        public static string compositionString { get; }
        public static Vector3 acceleration { get; }
        public static bool GetKey(KeyCode key) => throw null;
        public static bool GetKey(string name) => throw null;
        public static bool GetKeyDown(KeyCode key) => throw null;
        public static bool GetKeyDown(string name) => throw null;
        public static bool GetKeyUp(KeyCode key) => throw null;
        public static bool GetKeyUp(string name) => throw null;
        public static bool GetMouseButton(int button) => throw null;
        public static bool GetMouseButtonDown(int button) => throw null;
        public static bool GetMouseButtonUp(int button) => throw null;
        public static bool GetButton(string buttonName) => throw null;
        public static bool GetButtonDown(string buttonName) => throw null;
        public static bool GetButtonUp(string buttonName) => throw null;
        public static float GetAxis(string axisName) => throw null;
        public static float GetAxisRaw(string axisName) => throw null;
        public static Il2CppStringArray GetJoystickNames() => throw null;
    }
}
