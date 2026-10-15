using UnityEngine;

namespace ASL.Api
{
    /// <summary>
    /// Load files from your mod folder, reached through <c>ctx.Assets</c>. All paths are relative to
    /// <see cref="IModContext.ModDirectory"/> and cannot escape it. Loaded textures, sprites, audio and
    /// bundles are cached, so loading the same path twice is free.
    /// <code>
    /// var icon = ctx.Assets.LoadSprite("icons/gear.png");
    /// win.AddIconButton(icon, OpenSettings);
    /// var click = ctx.Assets.LoadAudio("sounds/click.wav");
    /// </code>
    /// </summary>
    public interface IAslAssets
    {
        /// <summary>The absolute path for <paramref name="relativePath"/>, safely joined inside the mod folder (throws on traversal).</summary>
        string GetPath(string relativePath);

        /// <summary>Read a file's bytes.</summary>
        byte[] ReadBytes(string relativePath);

        /// <summary>Read a file as UTF-8 text.</summary>
        string ReadText(string relativePath);

        /// <summary>Load a PNG as a texture (decoded in managed code; cached). Null if missing/invalid (logged).</summary>
        Texture2D LoadTexture(string relativePngPath);

        /// <summary>Load a PNG as a sprite. <paramref name="border"/> &gt; 0 makes it 9-sliced with that border in pixels.</summary>
        Sprite LoadSprite(string relativePngPath, float border = 0f);

        /// <summary>Load a WAV (PCM 8/16/24/32-bit or float32, mono/stereo) as an <see cref="AudioClip"/>. Null if missing/invalid (logged).</summary>
        AudioClip LoadAudio(string relativeWavPath);

        /// <summary>Load a Unity AssetBundle from disk (cached). Null if it fails (logged).</summary>
        AssetBundle LoadBundle(string relativePath);

        /// <summary>Load a named asset from a bundle (loading the bundle if needed). Null if not found.</summary>
        UnityEngine.Object LoadBundleAsset(string bundleRelativePath, string assetName);
    }
}
