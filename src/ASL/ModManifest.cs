using System.Text.Json.Serialization;

namespace ASL
{
    /// <summary>
    /// Deserialized <c>manifest.json</c> — every mod folder under <c>mods/</c> ships one.
    /// </summary>
    public sealed class ModManifest
    {
        [JsonPropertyName("id")]          public string Id { get; set; }
        [JsonPropertyName("name")]        public string Name { get; set; }
        [JsonPropertyName("version")]     public string Version { get; set; } = "1.0.0";
        [JsonPropertyName("author")]      public string Author { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }

        /// <summary>
        /// <c>"dll"</c> | <c>"script"</c> | <c>"content"</c>. If omitted, ASL auto-detects from
        /// the folder contents (a .dll -> dll, a .cs -> script, otherwise content).
        /// </summary>
        [JsonPropertyName("type")] public string Type { get; set; }

        /// <summary>
        /// Entry file relative to the mod folder: the .dll (dll mods) or the .cs (script mods).
        /// Optional — ASL falls back to the first matching file(s) in the folder.
        /// </summary>
        [JsonPropertyName("entry")] public string Entry { get; set; }

        /// <summary>Declarative, code-free content for <c>type: "content"</c> mods.</summary>
        [JsonPropertyName("content")] public ContentSpec Content { get; set; }
    }

    /// <summary>No-code content a mod applies to the game.</summary>
    public sealed class ContentSpec
    {
        /// <summary>Texture swaps: replace a named game texture with a PNG shipped in the mod.</summary>
        [JsonPropertyName("textures")] public TextureReplacement[] Textures { get; set; }

        /// <summary>If true, ASL logs a sample of loaded texture names (to help find swap targets).</summary>
        [JsonPropertyName("listTextureNames")] public bool ListTextureNames { get; set; }
    }

    /// <summary>One texture swap: game texture named <see cref="Target"/> ← PNG file <see cref="File"/>.</summary>
    public sealed class TextureReplacement
    {
        [JsonPropertyName("target")] public string Target { get; set; }
        [JsonPropertyName("file")]   public string File { get; set; }
    }

    /// <summary>How ASL should load a given mod.</summary>
    public enum ModKind
    {
        Unknown,
        Dll,
        Script,
        Content,
    }
}
