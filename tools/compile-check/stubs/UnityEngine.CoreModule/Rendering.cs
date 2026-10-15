// STUB: textures, sprites, materials, renderers, meshes.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    public enum TextureFormat
    {
        Alpha8 = 1, ARGB4444 = 2, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, RGB565 = 7, R16 = 9, DXT1 = 10, DXT5 = 12,
        RGBA4444 = 13, BGRA32 = 14, RHalf = 15, RGHalf = 16, RGBAHalf = 17, RFloat = 18, RGFloat = 19, RGBAFloat = 20,
        BC4 = 26, BC5 = 27, BC6H = 24, BC7 = 25, R8 = 63
    }
    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1, Mirror = 2, MirrorOnce = 3 }
    public enum SpriteMeshType { FullRect = 0, Tight = 1 }
    public enum SpritePackingMode { Tight = 0, Rectangle = 1 }
    public enum RenderTextureFormat { ARGB32 = 0, Depth = 1, ARGBHalf = 2, Default = 7 }

    public class Texture : Object
    {
        public Texture(IntPtr pointer) : base(pointer) { }
        public int width { get; set; }
        public int height { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public TextureWrapMode wrapModeU { get; set; }
        public TextureWrapMode wrapModeV { get; set; }
        public FilterMode filterMode { get; set; }
        public int anisoLevel { get; set; }
        public float mipMapBias { get; set; }
        public bool isReadable { get; }
        public Vector2 texelSize { get; }
        public IntPtr GetNativeTexturePtr() => throw null;
    }

    public class Texture2D : Texture
    {
        public Texture2D(IntPtr pointer) : base(pointer) { }
        public Texture2D(int width, int height) : base(IntPtr.Zero) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) : base(IntPtr.Zero) { }
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain, bool linear) : base(IntPtr.Zero) { }
        public static Texture2D whiteTexture { get; }
        public static Texture2D blackTexture { get; }
        public TextureFormat format { get; }
        public int mipmapCount { get; }
        public bool Reinitialize(int width, int height) => throw null;
        public bool Reinitialize(int width, int height, TextureFormat format, bool hasMipMap) => throw null;
        public void SetPixel(int x, int y, Color color) => throw null;
        public Color GetPixel(int x, int y) => throw null;
        public Color GetPixelBilinear(float u, float v) => throw null;
        public void SetPixels(Il2CppStructArray<Color> colors) => throw null;
        public Il2CppStructArray<Color> GetPixels() => throw null;
        public void SetPixels32(Il2CppStructArray<Color32> colors) => throw null;
        public void SetPixels32(Il2CppStructArray<Color32> colors, int miplevel) => throw null;
        public Il2CppStructArray<Color32> GetPixels32() => throw null;
        public Il2CppStructArray<Color32> GetPixels32(int miplevel) => throw null;
        public void LoadRawTextureData(IntPtr data, int size) => throw null;
        public void LoadRawTextureData(Il2CppStructArray<byte> data) => throw null;
        public void Apply() => throw null;
        public void Apply(bool updateMipmaps) => throw null;
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) => throw null;
        public bool Resize(int width, int height) => throw null;
        public Il2CppStructArray<Rect> PackTextures(Il2CppReferenceArray<Texture2D> textures, int padding) => throw null;
    }

    public class RenderTexture : Texture
    {
        public RenderTexture(IntPtr pointer) : base(pointer) { }
        public RenderTexture(int width, int height, int depth) : base(IntPtr.Zero) { }
        public static RenderTexture active { get; set; }
        public bool Create() => throw null;
        public void Release() => throw null;
    }

    public class Sprite : Object
    {
        public Sprite(IntPtr pointer) : base(pointer) { }
        public Texture2D texture { get; }
        public Rect rect { get; }
        public Rect textureRect { get; }
        public Vector4 border { get; }
        public Vector2 pivot { get; }
        public Bounds bounds { get; }
        public float pixelsPerUnit { get; }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => throw null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => throw null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude) => throw null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType) => throw null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border) => throw null;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border, bool generateFallbackPhysicsShape) => throw null;
    }

    public class Shader : Object
    {
        public Shader(IntPtr pointer) : base(pointer) { }
        public static Shader Find(string name) => throw null;
        public static int PropertyToID(string name) => throw null;
        public int renderQueue { get; }
    }

    public class Material : Object
    {
        public Material(IntPtr pointer) : base(pointer) { }
        public Material(Shader shader) : base(IntPtr.Zero) { }
        public Material(Material source) : base(IntPtr.Zero) { }
        public Shader shader { get; set; }
        public Color color { get; set; }
        public Texture mainTexture { get; set; }
        public Vector2 mainTextureOffset { get; set; }
        public Vector2 mainTextureScale { get; set; }
        public int renderQueue { get; set; }
        public bool HasProperty(string name) => throw null;
        public bool HasProperty(int nameID) => throw null;
        public void SetColor(string name, Color value) => throw null;
        public void SetColor(int nameID, Color value) => throw null;
        public Color GetColor(string name) => throw null;
        public void SetTexture(string name, Texture value) => throw null;
        public void SetTexture(int nameID, Texture value) => throw null;
        public Texture GetTexture(string name) => throw null;
        public Texture GetTexture(int nameID) => throw null;
        public void SetFloat(string name, float value) => throw null;
        public float GetFloat(string name) => throw null;
        public void SetInt(string name, int value) => throw null;
        public void SetVector(string name, Vector4 value) => throw null;
        public Vector4 GetVector(string name) => throw null;
        public void SetTextureOffset(string name, Vector2 value) => throw null;
        public Vector2 GetTextureOffset(string name) => throw null;
        public void SetTextureScale(string name, Vector2 value) => throw null;
        public Vector2 GetTextureScale(string name) => throw null;
        public void EnableKeyword(string keyword) => throw null;
        public void DisableKeyword(string keyword) => throw null;
        public bool IsKeywordEnabled(string keyword) => throw null;
        public void CopyPropertiesFromMaterial(Material mat) => throw null;
    }

    public class Mesh : Object
    {
        public Mesh(IntPtr pointer) : base(pointer) { }
        public Mesh() : base() { }
        public int vertexCount { get; }
        public int subMeshCount { get; set; }
        public Bounds bounds { get; set; }
        public bool isReadable { get; }
    }

    public class MeshFilter : Component
    {
        public MeshFilter(IntPtr pointer) : base(pointer) { }
        public Mesh sharedMesh { get; set; }
        public Mesh mesh { get; set; }
    }

    public class Renderer : Component
    {
        public Renderer(IntPtr pointer) : base(pointer) { }
        public bool enabled { get; set; }
        public bool forceRenderingOff { get; set; }
        public bool isVisible { get; }
        public Bounds bounds { get; set; }
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public Il2CppReferenceArray<Material> materials { get; set; }
        public Il2CppReferenceArray<Material> sharedMaterials { get; set; }
        public int sortingOrder { get; set; }
        public string sortingLayerName { get; set; }
        public int sortingLayerID { get; set; }
    }

    public class MeshRenderer : Renderer { public MeshRenderer(IntPtr pointer) : base(pointer) { } }

    public class SkinnedMeshRenderer : Renderer
    {
        public SkinnedMeshRenderer(IntPtr pointer) : base(pointer) { }
        public Mesh sharedMesh { get; set; }
        public Transform rootBone { get; set; }
    }

    public class Light : Behaviour
    {
        public Light(IntPtr pointer) : base(pointer) { }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
    }
}
