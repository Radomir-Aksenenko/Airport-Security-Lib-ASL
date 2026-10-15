// STUB: UnityEngine.AudioModule.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace UnityEngine
{
    public class AudioClip : Object
    {
        public AudioClip(IntPtr pointer) : base(pointer) { }
        public float length { get; }
        public int samples { get; }
        public int channels { get; }
        public int frequency { get; }
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => throw null;
        public bool SetData(Il2CppStructArray<float> data, int offsetSamples) => throw null;
        public bool GetData(Il2CppStructArray<float> data, int offsetSamples) => throw null;
    }

    public class AudioSource : Behaviour
    {
        public AudioSource(IntPtr pointer) : base(pointer) { }
        public AudioClip clip { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public bool loop { get; set; }
        public bool mute { get; set; }
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public float time { get; set; }
        public bool isPlaying { get; }
        public void Play() => throw null;
        public void Play(ulong delay) => throw null;
        public void Stop() => throw null;
        public void Pause() => throw null;
        public void UnPause() => throw null;
        public void PlayOneShot(AudioClip clip) => throw null;
        public void PlayOneShot(AudioClip clip, float volumeScale) => throw null;
        public static void PlayClipAtPoint(AudioClip clip, Vector3 position) => throw null;
    }

    public class AudioListener : Behaviour
    {
        public AudioListener(IntPtr pointer) : base(pointer) { }
        public static float volume { get; set; }
        public static bool pause { get; set; }
    }
}
