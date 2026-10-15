// STUB: UnityEngine.AssetBundleModule.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace UnityEngine
{
    public class AssetBundle : Object
    {
        public AssetBundle(IntPtr pointer) : base(pointer) { }
        public static AssetBundle LoadFromFile(string path) => throw null;
        public static AssetBundle LoadFromFile(string path, uint crc) => throw null;
        public static AssetBundle LoadFromMemory(Il2CppStructArray<byte> binary) => throw null;
        public static AssetBundleCreateRequest LoadFromFileAsync(string path) => throw null;
        public static Il2CppReferenceArray<AssetBundle> GetAllLoadedAssetBundles_Native() => throw null;
        public static void UnloadAllAssetBundles(bool unloadAllObjects) => throw null;
        public bool isStreamedSceneAssetBundle { get; }
        public bool Contains(string name) => throw null;
        public Object LoadAsset(string name) => throw null;
        public Object LoadAsset(string name, Il2CppSystem.Type type) => throw null;
        public T LoadAsset<T>(string name) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase => throw null;
        public AssetBundleRequest LoadAssetAsync(string name, Il2CppSystem.Type type) => throw null;
        public Il2CppReferenceArray<Object> LoadAllAssets() => throw null;
        public Il2CppReferenceArray<Object> LoadAllAssets(Il2CppSystem.Type type) => throw null;
        public Il2CppStringArray GetAllAssetNames() => throw null;
        public Il2CppStringArray GetAllScenePaths() => throw null;
        public void Unload(bool unloadAllLoadedObjects) => throw null;
    }
    public class AssetBundleCreateRequest : AsyncOperation
    {
        public AssetBundleCreateRequest(IntPtr pointer) : base(pointer) { }
        public AssetBundle assetBundle { get; }
    }
    public class AssetBundleRequest : AsyncOperation
    {
        public AssetBundleRequest(IntPtr pointer) : base(pointer) { }
        public Object asset { get; }
        public Il2CppReferenceArray<Object> allAssets { get; }
    }
}
