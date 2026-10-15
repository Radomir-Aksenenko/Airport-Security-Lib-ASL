using System;
using ASL.Api;
using BepInEx.Logging;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslAssets"/> (textures, sprites, WAV audio, bundles, text from the mod folder).
    /// Owned by the Data agent. Placeholder: the constructor must stay side-effect free.
    /// </summary>
    internal sealed class ModAssets : IAslAssets
    {
        private readonly string _modId;
        private readonly string _modDirectory;
        private readonly ManualLogSource _log;

        public ModAssets(string modId, string modDirectory, ManualLogSource log)
        {
            _modId = modId;
            _modDirectory = modDirectory;
            _log = log;
        }

        public string GetPath(string relativePath) => throw NotYet("Assets.GetPath");
        public byte[] ReadBytes(string relativePath) => throw NotYet("Assets.ReadBytes");
        public string ReadText(string relativePath) => throw NotYet("Assets.ReadText");
        public Texture2D LoadTexture(string relativePngPath) => throw NotYet("Assets.LoadTexture");
        public Sprite LoadSprite(string relativePngPath, float border = 0f) => throw NotYet("Assets.LoadSprite");
        public AudioClip LoadAudio(string relativeWavPath) => throw NotYet("Assets.LoadAudio");
        public AssetBundle LoadBundle(string relativePath) => throw NotYet("Assets.LoadBundle");
        public UnityEngine.Object LoadBundleAsset(string bundleRelativePath, string assetName) => throw NotYet("Assets.LoadBundleAsset");

        private static NotImplementedException NotYet(string what) => new NotImplementedException($"ASL 2.0: {what} not implemented yet");
    }
}
