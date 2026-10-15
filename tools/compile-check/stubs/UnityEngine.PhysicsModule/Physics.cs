// STUB: UnityEngine.PhysicsModule.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public enum QueryTriggerInteraction { UseGlobal = 0, Ignore = 1, Collide = 2 }
    public enum ForceMode { Force = 0, Impulse = 1, VelocityChange = 2, Acceleration = 5 }
    [Flags] public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }
    [Flags] public enum RigidbodyConstraints { None = 0, FreezePositionX = 2, FreezePositionY = 4, FreezePositionZ = 8, FreezePosition = 14, FreezeRotationX = 16, FreezeRotationY = 32, FreezeRotationZ = 64, FreezeRotation = 112, FreezeAll = 126 }

    /// <summary>Blittable struct in the interop output (the collider is stored as an instance id).</summary>
    public struct RaycastHit
    {
        public Vector3 point { get => throw null; set => throw null; }
        public Vector3 normal { get => throw null; set => throw null; }
        public Vector3 barycentricCoordinate { get => throw null; set => throw null; }
        public float distance { get => throw null; set => throw null; }
        public int triangleIndex => throw null;
        public Vector2 textureCoord => throw null;
        public Collider collider => throw null;
        public Rigidbody rigidbody => throw null;
        public Transform transform => throw null;
    }

    public class Collider : Component
    {
        public Collider(IntPtr pointer) : base(pointer) { }
        public bool enabled { get; set; }
        public bool isTrigger { get; set; }
        public Bounds bounds { get; }
        public Rigidbody attachedRigidbody { get; }
        public Vector3 ClosestPoint(Vector3 position) => throw null;
        public bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance) => throw null;
    }
    public class BoxCollider : Collider { public BoxCollider(IntPtr pointer) : base(pointer) { } public Vector3 center { get; set; } public Vector3 size { get; set; } }
    public class SphereCollider : Collider { public SphereCollider(IntPtr pointer) : base(pointer) { } public Vector3 center { get; set; } public float radius { get; set; } }
    public class CapsuleCollider : Collider { public CapsuleCollider(IntPtr pointer) : base(pointer) { } public Vector3 center { get; set; } public float radius { get; set; } public float height { get; set; } public int direction { get; set; } }
    public class MeshCollider : Collider { public MeshCollider(IntPtr pointer) : base(pointer) { } public Mesh sharedMesh { get; set; } public bool convex { get; set; } }

    public class CharacterController : Collider
    {
        public CharacterController(IntPtr pointer) : base(pointer) { }
        public float radius { get; set; }
        public float height { get; set; }
        public Vector3 center { get; set; }
        public float slopeLimit { get; set; }
        public float stepOffset { get; set; }
        public float skinWidth { get; set; }
        public float minMoveDistance { get; set; }
        public bool detectCollisions { get; set; }
        public Vector3 velocity { get; }
        public bool isGrounded { get; }
        public CollisionFlags collisionFlags { get; }
        public CollisionFlags Move(Vector3 motion) => throw null;
        public bool SimpleMove(Vector3 speed) => throw null;
    }

    public class Rigidbody : Component
    {
        public Rigidbody(IntPtr pointer) : base(pointer) { }
        public Vector3 velocity { get; set; }
        public Vector3 angularVelocity { get; set; }
        public float mass { get; set; }
        public float drag { get; set; }
        public bool useGravity { get; set; }
        public bool isKinematic { get; set; }
        public bool detectCollisions { get; set; }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public RigidbodyConstraints constraints { get; set; }
        public void AddForce(Vector3 force) => throw null;
        public void AddForce(Vector3 force, ForceMode mode) => throw null;
        public void MovePosition(Vector3 position) => throw null;
        public void MoveRotation(Quaternion rot) => throw null;
    }

    public class Collision : Il2CppSystem.Object
    {
        public Collision(IntPtr pointer) : base(pointer) { }
        public Collider collider { get; }
        public Transform transform { get; }
        public GameObject gameObject { get; }
        public Vector3 relativeVelocity { get; }
    }

    public static class Physics
    {
        public const int DefaultRaycastLayers = -5;
        public const int AllLayers = -1;
        public const int IgnoreRaycastLayer = 4;
        public static Vector3 gravity { get; set; }
        public static bool Raycast(Vector3 origin, Vector3 direction) => throw null;
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance) => throw null;
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask) => throw null;
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo) => throw null;
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance) => throw null;
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask) => throw null;
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction) => throw null;
        public static bool Raycast(Ray ray) => throw null;
        public static bool Raycast(Ray ray, float maxDistance) => throw null;
        public static bool Raycast(Ray ray, out RaycastHit hitInfo) => throw null;
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance) => throw null;
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask) => throw null;
        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction) => throw null;
        public static Il2CppStructArray<RaycastHit> RaycastAll(Ray ray) => throw null;
        public static Il2CppStructArray<RaycastHit> RaycastAll(Ray ray, float maxDistance) => throw null;
        public static Il2CppStructArray<RaycastHit> RaycastAll(Vector3 origin, Vector3 direction, float maxDistance) => throw null;
        public static bool Linecast(Vector3 start, Vector3 end) => throw null;
        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance) => throw null;
        public static bool CheckSphere(Vector3 position, float radius) => throw null;
        public static Il2CppReferenceArray<Collider> OverlapSphere(Vector3 position, float radius) => throw null;
        public static Il2CppReferenceArray<Collider> OverlapSphere(Vector3 position, float radius, int layerMask) => throw null;
        public static Il2CppReferenceArray<Collider> OverlapBox(Vector3 center, Vector3 halfExtents) => throw null;
        public static void IgnoreCollision(Collider collider1, Collider collider2) => throw null;
        public static void IgnoreCollision(Collider collider1, Collider collider2, bool ignore) => throw null;
    }
}
