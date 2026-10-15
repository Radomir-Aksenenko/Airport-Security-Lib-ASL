// STUB: UnityEngine.CoreModule math structs. Blittable structs stay real structs in Il2CppInterop output.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float this[int index] { get => throw null; set => throw null; }
        public static Vector2 zero => throw null;
        public static Vector2 one => throw null;
        public static Vector2 up => throw null;
        public static Vector2 down => throw null;
        public static Vector2 left => throw null;
        public static Vector2 right => throw null;
        public static Vector2 positiveInfinity => throw null;
        public static Vector2 negativeInfinity => throw null;
        public float magnitude => throw null;
        public float sqrMagnitude => throw null;
        public Vector2 normalized => throw null;
        public void Set(float newX, float newY) => throw null;
        public void Normalize() => throw null;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => throw null;
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => throw null;
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta) => throw null;
        public static Vector2 Scale(Vector2 a, Vector2 b) => throw null;
        public void Scale(Vector2 scale) => throw null;
        public static float Dot(Vector2 lhs, Vector2 rhs) => throw null;
        public static float Distance(Vector2 a, Vector2 b) => throw null;
        public static float Angle(Vector2 from, Vector2 to) => throw null;
        public static Vector2 ClampMagnitude(Vector2 vector, float maxLength) => throw null;
        public static Vector2 Min(Vector2 lhs, Vector2 rhs) => throw null;
        public static Vector2 Max(Vector2 lhs, Vector2 rhs) => throw null;
        public static Vector2 Reflect(Vector2 inDirection, Vector2 inNormal) => throw null;
        public static Vector2 Perpendicular(Vector2 inDirection) => throw null;
        public static Vector2 operator +(Vector2 a, Vector2 b) => throw null;
        public static Vector2 operator -(Vector2 a, Vector2 b) => throw null;
        public static Vector2 operator *(Vector2 a, Vector2 b) => throw null;
        public static Vector2 operator /(Vector2 a, Vector2 b) => throw null;
        public static Vector2 operator -(Vector2 a) => throw null;
        public static Vector2 operator *(Vector2 a, float d) => throw null;
        public static Vector2 operator *(float d, Vector2 a) => throw null;
        public static Vector2 operator /(Vector2 a, float d) => throw null;
        public static bool operator ==(Vector2 lhs, Vector2 rhs) => throw null;
        public static bool operator !=(Vector2 lhs, Vector2 rhs) => throw null;
        public static implicit operator Vector2(Vector3 v) => throw null;
        public static implicit operator Vector3(Vector2 v) => throw null;
        public bool Equals(Vector2 other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public override string ToString() => throw null;
        public string ToString(string format) => throw null;
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => throw null;
        public static Vector2Int one => throw null;
        public bool Equals(Vector2Int other) => throw null;
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }
        public float this[int index] { get => throw null; set => throw null; }
        public static Vector3 zero => throw null;
        public static Vector3 one => throw null;
        public static Vector3 up => throw null;
        public static Vector3 down => throw null;
        public static Vector3 left => throw null;
        public static Vector3 right => throw null;
        public static Vector3 forward => throw null;
        public static Vector3 back => throw null;
        public static Vector3 positiveInfinity => throw null;
        public static Vector3 negativeInfinity => throw null;
        public float magnitude => throw null;
        public float sqrMagnitude => throw null;
        public Vector3 normalized => throw null;
        public void Set(float newX, float newY, float newZ) => throw null;
        public void Normalize() => throw null;
        public static Vector3 Normalize(Vector3 value) => throw null;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => throw null;
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => throw null;
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => throw null;
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) => throw null;
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime) => throw null;
        public static Vector3 Scale(Vector3 a, Vector3 b) => throw null;
        public void Scale(Vector3 scale) => throw null;
        public static Vector3 Cross(Vector3 lhs, Vector3 rhs) => throw null;
        public static Vector3 Reflect(Vector3 inDirection, Vector3 inNormal) => throw null;
        public static Vector3 Project(Vector3 vector, Vector3 onNormal) => throw null;
        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal) => throw null;
        public static float Angle(Vector3 from, Vector3 to) => throw null;
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) => throw null;
        public static float Distance(Vector3 a, Vector3 b) => throw null;
        public static Vector3 ClampMagnitude(Vector3 vector, float maxLength) => throw null;
        public static float Magnitude(Vector3 vector) => throw null;
        public static float SqrMagnitude(Vector3 vector) => throw null;
        public static float Dot(Vector3 lhs, Vector3 rhs) => throw null;
        public static Vector3 Min(Vector3 lhs, Vector3 rhs) => throw null;
        public static Vector3 Max(Vector3 lhs, Vector3 rhs) => throw null;
        public static Vector3 operator +(Vector3 a, Vector3 b) => throw null;
        public static Vector3 operator -(Vector3 a, Vector3 b) => throw null;
        public static Vector3 operator -(Vector3 a) => throw null;
        public static Vector3 operator *(Vector3 a, float d) => throw null;
        public static Vector3 operator *(float d, Vector3 a) => throw null;
        public static Vector3 operator /(Vector3 a, float d) => throw null;
        public static bool operator ==(Vector3 lhs, Vector3 rhs) => throw null;
        public static bool operator !=(Vector3 lhs, Vector3 rhs) => throw null;
        public bool Equals(Vector3 other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public override string ToString() => throw null;
        public string ToString(string format) => throw null;
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public bool Equals(Vector3Int other) => throw null;
    }

    public struct Vector4 : IEquatable<Vector4>
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4(float x, float y, float z) { this.x = x; this.y = y; this.z = z; w = 0f; }
        public Vector4(float x, float y) { this.x = x; this.y = y; z = 0f; w = 0f; }
        public float this[int index] { get => throw null; set => throw null; }
        public static Vector4 zero => throw null;
        public static Vector4 one => throw null;
        public float magnitude => throw null;
        public float sqrMagnitude => throw null;
        public Vector4 normalized => throw null;
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => throw null;
        public static float Dot(Vector4 a, Vector4 b) => throw null;
        public static Vector4 operator +(Vector4 a, Vector4 b) => throw null;
        public static Vector4 operator -(Vector4 a, Vector4 b) => throw null;
        public static Vector4 operator *(Vector4 a, float d) => throw null;
        public static Vector4 operator /(Vector4 a, float d) => throw null;
        public static bool operator ==(Vector4 lhs, Vector4 rhs) => throw null;
        public static bool operator !=(Vector4 lhs, Vector4 rhs) => throw null;
        public static implicit operator Vector4(Vector3 v) => throw null;
        public static implicit operator Vector3(Vector4 v) => throw null;
        public static implicit operator Vector4(Vector2 v) => throw null;
        public static implicit operator Vector2(Vector4 v) => throw null;
        public bool Equals(Vector4 other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public override string ToString() => throw null;
    }

    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => throw null;
        public Vector3 eulerAngles { get => throw null; set => throw null; }
        public Quaternion normalized => throw null;
        public static Quaternion Euler(float x, float y, float z) => throw null;
        public static Quaternion Euler(Vector3 euler) => throw null;
        public static Quaternion AngleAxis(float angle, Vector3 axis) => throw null;
        public static Quaternion LookRotation(Vector3 forward) => throw null;
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards) => throw null;
        public static Quaternion FromToRotation(Vector3 fromDirection, Vector3 toDirection) => throw null;
        public static Quaternion Inverse(Quaternion rotation) => throw null;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => throw null;
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => throw null;
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => throw null;
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => throw null;
        public static float Angle(Quaternion a, Quaternion b) => throw null;
        public static float Dot(Quaternion a, Quaternion b) => throw null;
        public void Set(float newX, float newY, float newZ, float newW) => throw null;
        public void SetLookRotation(Vector3 view) => throw null;
        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => throw null;
        public static Vector3 operator *(Quaternion rotation, Vector3 point) => throw null;
        public static bool operator ==(Quaternion lhs, Quaternion rhs) => throw null;
        public static bool operator !=(Quaternion lhs, Quaternion rhs) => throw null;
        public bool Equals(Quaternion other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public override string ToString() => throw null;
    }

    public struct Matrix4x4 : IEquatable<Matrix4x4>
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
        public static Matrix4x4 identity => throw null;
        public static Matrix4x4 zero => throw null;
        public Matrix4x4 inverse => throw null;
        public Matrix4x4 transpose => throw null;
        public float this[int row, int column] { get => throw null; set => throw null; }
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) => throw null;
        public Vector3 MultiplyPoint(Vector3 point) => throw null;
        public Vector3 MultiplyPoint3x4(Vector3 point) => throw null;
        public Vector3 MultiplyVector(Vector3 vector) => throw null;
        public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs) => throw null;
        public bool Equals(Matrix4x4 other) => throw null;
    }

    public struct Ray
    {
        public Ray(Vector3 origin, Vector3 direction) { this.m_Origin = origin; this.m_Direction = direction; }
        private Vector3 m_Origin, m_Direction;
        public Vector3 origin { get => throw null; set => throw null; }
        public Vector3 direction { get => throw null; set => throw null; }
        public Vector3 GetPoint(float distance) => throw null;
    }

    public struct Color : IEquatable<Color>
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public float this[int index] { get => throw null; set => throw null; }
        public static Color red => throw null;
        public static Color green => throw null;
        public static Color blue => throw null;
        public static Color white => throw null;
        public static Color black => throw null;
        public static Color yellow => throw null;
        public static Color cyan => throw null;
        public static Color magenta => throw null;
        public static Color gray => throw null;
        public static Color grey => throw null;
        public static Color clear => throw null;
        public float grayscale => throw null;
        public Color linear => throw null;
        public Color gamma => throw null;
        public float maxColorComponent => throw null;
        public static Color Lerp(Color a, Color b, float t) => throw null;
        public static Color LerpUnclamped(Color a, Color b, float t) => throw null;
        public static Color HSVToRGB(float H, float S, float V) => throw null;
        public static Color HSVToRGB(float H, float S, float V, bool hdr) => throw null;
        public static void RGBToHSV(Color rgbColor, out float H, out float S, out float V) => throw null;
        public static Color operator +(Color a, Color b) => throw null;
        public static Color operator -(Color a, Color b) => throw null;
        public static Color operator *(Color a, Color b) => throw null;
        public static Color operator *(Color a, float b) => throw null;
        public static Color operator *(float b, Color a) => throw null;
        public static Color operator /(Color a, float b) => throw null;
        public static bool operator ==(Color lhs, Color rhs) => throw null;
        public static bool operator !=(Color lhs, Color rhs) => throw null;
        public static implicit operator Vector4(Color c) => throw null;
        public static implicit operator Color(Vector4 v) => throw null;
        public bool Equals(Color other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public override string ToString() => throw null;
    }

    public struct Color32 : IEquatable<Color32>
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color32 Lerp(Color32 a, Color32 b, float t) => throw null;
        public static implicit operator Color32(Color c) => throw null;
        public static implicit operator Color(Color32 c) => throw null;
        public bool Equals(Color32 other) => throw null;
    }

    public struct Rect : IEquatable<Rect>
    {
        private float m_XMin, m_YMin, m_Width, m_Height;
        public Rect(float x, float y, float width, float height) { m_XMin = x; m_YMin = y; m_Width = width; m_Height = height; }
        public Rect(Vector2 position, Vector2 size) { m_XMin = position.x; m_YMin = position.y; m_Width = size.x; m_Height = size.y; }
        public Rect(Rect source) { m_XMin = source.m_XMin; m_YMin = source.m_YMin; m_Width = source.m_Width; m_Height = source.m_Height; }
        public static Rect zero => throw null;
        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax) => throw null;
        public float x { get => throw null; set => throw null; }
        public float y { get => throw null; set => throw null; }
        public Vector2 position { get => throw null; set => throw null; }
        public Vector2 center { get => throw null; set => throw null; }
        public Vector2 min { get => throw null; set => throw null; }
        public Vector2 max { get => throw null; set => throw null; }
        public float width { get => throw null; set => throw null; }
        public float height { get => throw null; set => throw null; }
        public Vector2 size { get => throw null; set => throw null; }
        public float xMin { get => throw null; set => throw null; }
        public float yMin { get => throw null; set => throw null; }
        public float xMax { get => throw null; set => throw null; }
        public float yMax { get => throw null; set => throw null; }
        public bool Contains(Vector2 point) => throw null;
        public bool Contains(Vector3 point) => throw null;
        public bool Overlaps(Rect other) => throw null;
        public static Vector2 NormalizedToPoint(Rect rectangle, Vector2 normalizedRectCoordinates) => throw null;
        public static Vector2 PointToNormalized(Rect rectangle, Vector2 point) => throw null;
        public static bool operator ==(Rect lhs, Rect rhs) => throw null;
        public static bool operator !=(Rect lhs, Rect rhs) => throw null;
        public bool Equals(Rect other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
        public override string ToString() => throw null;
    }

    public struct RectInt
    {
        public int x, y, width, height;
    }

    public struct Bounds : IEquatable<Bounds>
    {
        private Vector3 m_Center, m_Extents;
        public Bounds(Vector3 center, Vector3 size) { m_Center = center; m_Extents = size * 0.5f; }
        public Vector3 center { get => throw null; set => throw null; }
        public Vector3 size { get => throw null; set => throw null; }
        public Vector3 extents { get => throw null; set => throw null; }
        public Vector3 min { get => throw null; set => throw null; }
        public Vector3 max { get => throw null; set => throw null; }
        public void SetMinMax(Vector3 min, Vector3 max) => throw null;
        public void Encapsulate(Vector3 point) => throw null;
        public void Encapsulate(Bounds bounds) => throw null;
        public void Expand(float amount) => throw null;
        public void Expand(Vector3 amount) => throw null;
        public bool Intersects(Bounds bounds) => throw null;
        public bool Contains(Vector3 point) => throw null;
        public Vector3 ClosestPoint(Vector3 point) => throw null;
        public float SqrDistance(Vector3 point) => throw null;
        public static bool operator ==(Bounds lhs, Bounds rhs) => throw null;
        public static bool operator !=(Bounds lhs, Bounds rhs) => throw null;
        public bool Equals(Bounds other) => throw null;
        public override bool Equals(object other) => throw null;
        public override int GetHashCode() => throw null;
    }

    public struct LayerMask
    {
        public int value { get => throw null; set => throw null; }
        public static implicit operator int(LayerMask mask) => throw null;
        public static implicit operator LayerMask(int intVal) => throw null;
        public static string LayerToName(int layer) => throw null;
        public static int NameToLayer(string layerName) => throw null;
        public static int GetMask(Il2CppStringArray layerNames) => throw null;
    }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public static readonly float Epsilon;
        public static float Sin(float f) => throw null;
        public static float Cos(float f) => throw null;
        public static float Tan(float f) => throw null;
        public static float Asin(float f) => throw null;
        public static float Acos(float f) => throw null;
        public static float Atan(float f) => throw null;
        public static float Atan2(float y, float x) => throw null;
        public static float Sqrt(float f) => throw null;
        public static float Abs(float f) => throw null;
        public static int Abs(int value) => throw null;
        public static float Min(float a, float b) => throw null;
        public static int Min(int a, int b) => throw null;
        public static float Max(float a, float b) => throw null;
        public static int Max(int a, int b) => throw null;
        public static float Pow(float f, float p) => throw null;
        public static float Exp(float power) => throw null;
        public static float Log(float f, float p) => throw null;
        public static float Log(float f) => throw null;
        public static float Log10(float f) => throw null;
        public static float Ceil(float f) => throw null;
        public static float Floor(float f) => throw null;
        public static float Round(float f) => throw null;
        public static int CeilToInt(float f) => throw null;
        public static int FloorToInt(float f) => throw null;
        public static int RoundToInt(float f) => throw null;
        public static float Sign(float f) => throw null;
        public static float Clamp(float value, float min, float max) => throw null;
        public static int Clamp(int value, int min, int max) => throw null;
        public static float Clamp01(float value) => throw null;
        public static float Lerp(float a, float b, float t) => throw null;
        public static float LerpUnclamped(float a, float b, float t) => throw null;
        public static float LerpAngle(float a, float b, float t) => throw null;
        public static float MoveTowards(float current, float target, float maxDelta) => throw null;
        public static float MoveTowardsAngle(float current, float target, float maxDelta) => throw null;
        public static float SmoothStep(float from, float to, float t) => throw null;
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime) => throw null;
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime) => throw null;
        public static float Gamma(float value, float absmax, float gamma) => throw null;
        public static bool Approximately(float a, float b) => throw null;
        public static float Repeat(float t, float length) => throw null;
        public static float PingPong(float t, float length) => throw null;
        public static float InverseLerp(float a, float b, float value) => throw null;
        public static float DeltaAngle(float current, float target) => throw null;
        public static bool IsPowerOfTwo(int value) => throw null;
        public static int NextPowerOfTwo(int value) => throw null;
        public static int ClosestPowerOfTwo(int value) => throw null;
    }
}
