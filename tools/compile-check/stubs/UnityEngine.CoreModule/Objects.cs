// STUB: UnityEngine.CoreModule object model (Object / Component / GameObject / Transform ...).
using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.SceneManagement;

namespace UnityEngine
{
    [Flags]
    public enum HideFlags
    {
        None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8,
        DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61
    }

    public class Object : Il2CppSystem.Object
    {
        public Object(IntPtr pointer) : base(pointer) { }
        public Object() : base(IntPtr.Zero) { }
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        public int GetInstanceID() => throw null;
        public override string ToString() => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public static bool operator ==(Object x, Object y) => throw null;
        public static bool operator !=(Object x, Object y) => throw null;
        public static implicit operator bool(Object exists) => throw null;

        public static Object Instantiate(Object original) => throw null;
        public static Object Instantiate(Object original, Transform parent) => throw null;
        public static Object Instantiate(Object original, Transform parent, bool instantiateInWorldSpace) => throw null;
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation) => throw null;
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform parent) => throw null;
        public static T Instantiate<T>(T original) where T : Object => throw null;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => throw null;
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => throw null;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => throw null;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object => throw null;
        public static void Destroy(Object obj) => throw null;
        public static void Destroy(Object obj, float t) => throw null;
        public static void DestroyImmediate(Object obj) => throw null;
        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets) => throw null;
        public static void DontDestroyOnLoad(Object target) => throw null;
        public static Object FindObjectOfType(Il2CppSystem.Type type) => throw null;
        public static Object FindObjectOfType(Il2CppSystem.Type type, bool includeInactive) => throw null;
        public static Il2CppReferenceArray<Object> FindObjectsOfType(Il2CppSystem.Type type) => throw null;
        public static Il2CppReferenceArray<Object> FindObjectsOfType(Il2CppSystem.Type type, bool includeInactive) => throw null;
        public static T FindObjectOfType<T>() where T : Il2CppObjectBase => throw null;
        public static T FindObjectOfType<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public static Il2CppReferenceArray<T> FindObjectsOfType<T>() where T : Il2CppObjectBase => throw null;
        public static Il2CppReferenceArray<T> FindObjectsOfType<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public static T FindFirstObjectByType<T>() where T : Il2CppObjectBase => throw null;
        public static T FindAnyObjectByType<T>() where T : Il2CppObjectBase => throw null;
    }

    public class Component : Object
    {
        public Component(IntPtr pointer) : base(pointer) { }
        public Component() : base() { }
        public Transform transform { get; }
        public GameObject gameObject { get; }
        public string tag { get; set; }
        public bool CompareTag(string tag) => throw null;
        public Component GetComponent(Il2CppSystem.Type type) => throw null;
        public T GetComponent<T>() where T : Il2CppObjectBase => throw null;
        public bool TryGetComponent<T>(out T component) where T : Il2CppObjectBase => throw null;
        public Component GetComponentInChildren(Il2CppSystem.Type t) => throw null;
        public Component GetComponentInChildren(Il2CppSystem.Type t, bool includeInactive) => throw null;
        public T GetComponentInChildren<T>() where T : Il2CppObjectBase => throw null;
        public T GetComponentInChildren<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public Component GetComponentInParent(Il2CppSystem.Type t) => throw null;
        public Component GetComponentInParent(Il2CppSystem.Type t, bool includeInactive) => throw null;
        public T GetComponentInParent<T>() where T : Il2CppObjectBase => throw null;
        public T GetComponentInParent<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<Component> GetComponents(Il2CppSystem.Type type) => throw null;
        public Il2CppReferenceArray<T> GetComponents<T>() where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInChildren(Il2CppSystem.Type t) => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInChildren(Il2CppSystem.Type t, bool includeInactive) => throw null;
        public Il2CppReferenceArray<T> GetComponentsInChildren<T>() where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<T> GetComponentsInChildren<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInParent(Il2CppSystem.Type t) => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInParent(Il2CppSystem.Type t, bool includeInactive) => throw null;
        public Il2CppReferenceArray<T> GetComponentsInParent<T>() where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<T> GetComponentsInParent<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public void SendMessage(string methodName) => throw null;
        public void BroadcastMessage(string methodName) => throw null;
    }

    public class Behaviour : Component
    {
        public Behaviour(IntPtr pointer) : base(pointer) { }
        public Behaviour() : base() { }
        public bool enabled { get; set; }
        public bool isActiveAndEnabled { get; }
    }

    public class MonoBehaviour : Behaviour
    {
        public MonoBehaviour(IntPtr pointer) : base(pointer) { }
        public MonoBehaviour() : base() { }
        public bool useGUILayout { get; set; }
        public Coroutine StartCoroutine(Il2CppSystem.Collections.IEnumerator routine) => throw null;
        public Coroutine StartCoroutine(string methodName) => throw null;
        public void StopCoroutine(Coroutine routine) => throw null;
        public void StopCoroutine(Il2CppSystem.Collections.IEnumerator routine) => throw null;
        public void StopAllCoroutines() => throw null;
        public void Invoke(string methodName, float time) => throw null;
        public void InvokeRepeating(string methodName, float time, float repeatRate) => throw null;
        public void CancelInvoke() => throw null;
        public void CancelInvoke(string methodName) => throw null;
        public bool IsInvoking(string methodName) => throw null;
    }

    public class ScriptableObject : Object
    {
        public ScriptableObject(IntPtr pointer) : base(pointer) { }
        public ScriptableObject() : base() { }
        public static ScriptableObject CreateInstance(Il2CppSystem.Type type) => throw null;
        public static T CreateInstance<T>() where T : ScriptableObject => throw null;
    }

    public class YieldInstruction : Il2CppSystem.Object { public YieldInstruction(IntPtr pointer) : base(pointer) { } public YieldInstruction() : base() { } }
    public class Coroutine : YieldInstruction { public Coroutine(IntPtr pointer) : base(pointer) { } }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(IntPtr pointer) : base(pointer) { } public WaitForSeconds(float seconds) : base() { } }
    public class WaitForSecondsRealtime : Il2CppSystem.Object { public WaitForSecondsRealtime(IntPtr pointer) : base(pointer) { } public WaitForSecondsRealtime(float time) : base() { } }
    public class WaitForEndOfFrame : YieldInstruction { public WaitForEndOfFrame(IntPtr pointer) : base(pointer) { } public WaitForEndOfFrame() : base() { } }
    public class WaitForFixedUpdate : YieldInstruction { public WaitForFixedUpdate(IntPtr pointer) : base(pointer) { } public WaitForFixedUpdate() : base() { } }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public sealed class GameObject : Object
    {
        public GameObject(IntPtr pointer) : base(pointer) { }
        public GameObject() : base() { }
        public GameObject(string name) : base() { }
        public GameObject(string name, Il2CppReferenceArray<Il2CppSystem.Type> components) : base() { }
        public static GameObject CreatePrimitive(PrimitiveType type) => throw null;
        public static GameObject Find(string name) => throw null;
        public static GameObject FindWithTag(string tag) => throw null;
        public static Il2CppReferenceArray<GameObject> FindGameObjectsWithTag(string tag) => throw null;
        public Transform transform { get; }
        public int layer { get; set; }
        public bool activeSelf { get; }
        public bool activeInHierarchy { get; }
        public bool isStatic { get; set; }
        public string tag { get; set; }
        public Scene scene { get; }
        public GameObject gameObject { get; }
        public void SetActive(bool value) => throw null;
        public bool CompareTag(string tag) => throw null;
        public Component AddComponent(Il2CppSystem.Type componentType) => throw null;
        public T AddComponent<T>() where T : Il2CppObjectBase => throw null;
        public Component GetComponent(Il2CppSystem.Type type) => throw null;
        public T GetComponent<T>() where T : Il2CppObjectBase => throw null;
        public bool TryGetComponent<T>(out T component) where T : Il2CppObjectBase => throw null;
        public Component GetComponentInChildren(Il2CppSystem.Type type) => throw null;
        public Component GetComponentInChildren(Il2CppSystem.Type type, bool includeInactive) => throw null;
        public T GetComponentInChildren<T>() where T : Il2CppObjectBase => throw null;
        public T GetComponentInChildren<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public Component GetComponentInParent(Il2CppSystem.Type type) => throw null;
        public Component GetComponentInParent(Il2CppSystem.Type type, bool includeInactive) => throw null;
        public T GetComponentInParent<T>() where T : Il2CppObjectBase => throw null;
        public T GetComponentInParent<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<Component> GetComponents(Il2CppSystem.Type type) => throw null;
        public Il2CppReferenceArray<T> GetComponents<T>() where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInChildren(Il2CppSystem.Type type) => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInChildren(Il2CppSystem.Type type, bool includeInactive) => throw null;
        public Il2CppReferenceArray<T> GetComponentsInChildren<T>() where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<T> GetComponentsInChildren<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInParent(Il2CppSystem.Type type) => throw null;
        public Il2CppReferenceArray<Component> GetComponentsInParent(Il2CppSystem.Type type, bool includeInactive) => throw null;
        public Il2CppReferenceArray<T> GetComponentsInParent<T>() where T : Il2CppObjectBase => throw null;
        public Il2CppReferenceArray<T> GetComponentsInParent<T>(bool includeInactive) where T : Il2CppObjectBase => throw null;
        public void SendMessage(string methodName) => throw null;
        public void BroadcastMessage(string methodName) => throw null;
    }

    public enum Space { World, Self }

    public class Transform : Component
    {
        public Transform(IntPtr pointer) : base(pointer) { }
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 right { get; set; }
        public Vector3 up { get; set; }
        public Vector3 forward { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 lossyScale { get; }
        public Transform parent { get; set; }
        public Transform root { get; }
        public int childCount { get; }
        public int hierarchyCount { get; }
        public bool hasChanged { get; set; }
        public Matrix4x4 worldToLocalMatrix { get; }
        public Matrix4x4 localToWorldMatrix { get; }
        public void SetParent(Transform p) => throw null;
        public void SetParent(Transform parent, bool worldPositionStays) => throw null;
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) => throw null;
        public void Translate(Vector3 translation) => throw null;
        public void Translate(Vector3 translation, Space relativeTo) => throw null;
        public void Rotate(Vector3 eulers) => throw null;
        public void Rotate(Vector3 eulers, Space relativeTo) => throw null;
        public void Rotate(float xAngle, float yAngle, float zAngle) => throw null;
        public void Rotate(Vector3 axis, float angle) => throw null;
        public void RotateAround(Vector3 point, Vector3 axis, float angle) => throw null;
        public void LookAt(Transform target) => throw null;
        public void LookAt(Vector3 worldPosition) => throw null;
        public void LookAt(Vector3 worldPosition, Vector3 worldUp) => throw null;
        public Vector3 TransformDirection(Vector3 direction) => throw null;
        public Vector3 InverseTransformDirection(Vector3 direction) => throw null;
        public Vector3 TransformVector(Vector3 vector) => throw null;
        public Vector3 InverseTransformVector(Vector3 vector) => throw null;
        public Vector3 TransformPoint(Vector3 position) => throw null;
        public Vector3 InverseTransformPoint(Vector3 position) => throw null;
        public void DetachChildren() => throw null;
        public void SetAsFirstSibling() => throw null;
        public void SetAsLastSibling() => throw null;
        public void SetSiblingIndex(int index) => throw null;
        public int GetSiblingIndex() => throw null;
        public Transform Find(string n) => throw null;
        public bool IsChildOf(Transform parent) => throw null;
        public Transform GetChild(int index) => throw null;
    }

    public class RectTransform : Transform
    {
        public RectTransform(IntPtr pointer) : base(pointer) { }
        public enum Axis { Horizontal = 0, Vertical = 1 }
        public enum Edge { Left = 0, Right = 1, Top = 2, Bottom = 3 }
        public Rect rect { get; }
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector3 anchoredPosition3D { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public void SetSizeWithCurrentAnchors(Axis axis, float size) => throw null;
        public void SetInsetAndSizeFromParentEdge(Edge edge, float inset, float size) => throw null;
        public void ForceUpdateRectTransforms() => throw null;
        public void GetLocalCorners(Il2CppStructArray<Vector3> fourCornersArray) => throw null;
        public void GetWorldCorners(Il2CppStructArray<Vector3> fourCornersArray) => throw null;
    }

    public class Camera : Behaviour
    {
        public Camera(IntPtr pointer) : base(pointer) { }
        public static Camera main { get; }
        public static Camera current { get; }
        public static int allCamerasCount { get; }
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public float orthographicSize { get; set; }
        public bool orthographic { get; set; }
        public float aspect { get; set; }
        public float depth { get; set; }
        public int cullingMask { get; set; }
        public Color backgroundColor { get; set; }
        public float pixelWidth { get; }
        public float pixelHeight { get; }
        public Rect pixelRect { get; set; }
        public RenderTexture targetTexture { get; set; }
        public Vector3 WorldToScreenPoint(Vector3 position) => throw null;
        public Vector3 WorldToViewportPoint(Vector3 position) => throw null;
        public Vector3 ScreenToWorldPoint(Vector3 position) => throw null;
        public Vector3 ScreenToViewportPoint(Vector3 position) => throw null;
        public Vector3 ViewportToWorldPoint(Vector3 position) => throw null;
        public Ray ScreenPointToRay(Vector3 pos) => throw null;
        public Ray ViewportPointToRay(Vector3 pos) => throw null;
        public static Il2CppReferenceArray<Camera> allCameras { get; }
    }

    public static class Application
    {
        public static string persistentDataPath { get; }
        public static string dataPath { get; }
        public static string streamingAssetsPath { get; }
        public static string temporaryCachePath { get; }
        public static string version { get; }
        public static string productName { get; }
        public static string companyName { get; }
        public static string unityVersion { get; }
        public static bool isFocused { get; }
        public static bool isPlaying { get; }
        public static bool runInBackground { get; set; }
        public static int targetFrameRate { get; set; }
        public static void Quit() => throw null;
        public static void Quit(int exitCode) => throw null;
        public static void OpenURL(string url) => throw null;
    }

    public static class Time
    {
        public static float time { get; }
        public static float timeSinceLevelLoad { get; }
        public static float deltaTime { get; }
        public static float fixedTime { get; }
        public static float unscaledTime { get; }
        public static float unscaledDeltaTime { get; }
        public static float fixedUnscaledTime { get; }
        public static float fixedDeltaTime { get; set; }
        public static float maximumDeltaTime { get; set; }
        public static float smoothDeltaTime { get; }
        public static float timeScale { get; set; }
        public static int frameCount { get; }
        public static int renderedFrameCount { get; }
        public static float realtimeSinceStartup { get; }
        public static double timeAsDouble { get; }
        public static double realtimeSinceStartupAsDouble { get; }
    }

    public static class Screen
    {
        public static int width { get; }
        public static int height { get; }
        public static float dpi { get; }
        public static bool fullScreen { get; set; }
        public static Resolution currentResolution { get; }
        public static void SetResolution(int width, int height, bool fullscreen) => throw null;
    }

    public struct Resolution { public int width { get; set; } public int height { get; set; } public int refreshRate { get; set; } }

    public enum CursorLockMode { None = 0, Locked = 1, Confined = 2 }

    public static class Cursor
    {
        public static bool visible { get; set; }
        public static CursorLockMode lockState { get; set; }
    }

    public static class Debug
    {
        public static void Log(Il2CppSystem.Object message) => throw null;
        public static void Log(string message) => throw null;
        public static void LogWarning(Il2CppSystem.Object message) => throw null;
        public static void LogWarning(string message) => throw null;
        public static void LogError(Il2CppSystem.Object message) => throw null;
        public static void LogError(string message) => throw null;
        public static void DrawLine(Vector3 start, Vector3 end) => throw null;
        public static void DrawLine(Vector3 start, Vector3 end, Color color) => throw null;
        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration) => throw null;
        public static void DrawRay(Vector3 start, Vector3 dir, Color color) => throw null;
    }

    public static class Random
    {
        public static float value { get; }
        public static Vector3 insideUnitSphere { get; }
        public static Vector2 insideUnitCircle { get; }
        public static Vector3 onUnitSphere { get; }
        public static int seed { get; set; }
        public static float Range(float minInclusive, float maxInclusive) => throw null;
        public static int Range(int minInclusive, int maxExclusive) => throw null;
    }

    public static class Resources
    {
        public static Object Load(string path) => throw null;
        public static Object Load(string path, Il2CppSystem.Type systemTypeInstance) => throw null;
        public static T Load<T>(string path) where T : Il2CppObjectBase => throw null;
        public static Il2CppReferenceArray<Object> LoadAll(string path) => throw null;
        public static Il2CppReferenceArray<Object> FindObjectsOfTypeAll(Il2CppSystem.Type type) => throw null;
        public static Il2CppReferenceArray<T> FindObjectsOfTypeAll<T>() where T : Il2CppObjectBase => throw null;
        public static void UnloadAsset(Object assetToUnload) => throw null;
        public static AsyncOperation UnloadUnusedAssets() => throw null;
    }

    public class AsyncOperation : YieldInstruction
    {
        public AsyncOperation(IntPtr pointer) : base(pointer) { }
        public bool isDone { get; }
        public float progress { get; }
        public bool allowSceneActivation { get; set; }
    }
}

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single = 0, Additive = 1 }

    public struct Scene : IEquatable<Scene>
    {
        public int handle { get; }
        public string name { get; set; }
        public string path { get; }
        public int buildIndex { get; }
        public bool isLoaded { get; }
        public int rootCount { get; }
        public bool IsValid() => throw null;
        public Il2CppReferenceArray<GameObject> GetRootGameObjects() => throw null;
        public bool Equals(Scene other) => throw null;
        public static bool operator ==(Scene lhs, Scene rhs) => throw null;
        public static bool operator !=(Scene lhs, Scene rhs) => throw null;
    }

    public static class SceneManager
    {
        public static int sceneCount { get; }
        public static int sceneCountInBuildSettings { get; }
        public static Scene GetActiveScene() => throw null;
        public static bool SetActiveScene(Scene scene) => throw null;
        public static Scene GetSceneAt(int index) => throw null;
        public static Scene GetSceneByName(string name) => throw null;
        public static Scene GetSceneByBuildIndex(int buildIndex) => throw null;
        public static void LoadScene(string sceneName) => throw null;
        public static void LoadScene(int sceneBuildIndex) => throw null;
        public static void LoadScene(string sceneName, LoadSceneMode mode) => throw null;
        public static AsyncOperation LoadSceneAsync(string sceneName) => throw null;
        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode) => throw null;
        public static AsyncOperation UnloadSceneAsync(string sceneName) => throw null;
    }
}
