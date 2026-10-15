// STUB: UnityEngine.ImageConversionModule.
// Deliberately NO LoadImage: on this Unity 6 / IL2CPP build it is span-based and unmarshalable (see CLAUDE.md).
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace UnityEngine
{
    public static class ImageConversion
    {
        public static Il2CppStructArray<byte> EncodeToPNG(Texture2D tex) => throw null;
        public static Il2CppStructArray<byte> EncodeToJPG(Texture2D tex) => throw null;
        public static Il2CppStructArray<byte> EncodeToJPG(Texture2D tex, int quality) => throw null;
    }
}
