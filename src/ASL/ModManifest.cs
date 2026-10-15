using System;
using System.Collections.Generic;
using System.Text.Json;
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

        // ---- v2 fields (all optional; parsed by 2.0, acted on by the Core/Data/UI subsystems) ----

        /// <summary>Hard dependencies: <c>"modId"</c> or <c>"modId@&gt;=1.2.0"</c>. Missing/failed ones stop this mod loading.</summary>
        [JsonPropertyName("dependencies")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] Dependencies { get; set; }

        /// <summary>Soft dependencies: loaded first if present, ignored if absent.</summary>
        [JsonPropertyName("optionalDependencies")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] OptionalDependencies { get; set; }

        /// <summary>Mod ids this mod prefers to load after (ordering only).</summary>
        [JsonPropertyName("loadAfter")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] LoadAfter { get; set; }

        /// <summary>Mod ids this mod prefers to load before (ordering only).</summary>
        [JsonPropertyName("loadBefore")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] LoadBefore { get; set; }

        /// <summary>Minimum ASL version required (semver, e.g. <c>"2.0.0"</c>). Older ASL marks the mod Incompatible.</summary>
        [JsonPropertyName("minAslVersion")] public string MinAslVersion { get; set; }

        /// <summary>Free-form tags shown in the Mod Manager.</summary>
        [JsonPropertyName("tags")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] Tags { get; set; }

        /// <summary>Project/home page URL.</summary>
        [JsonPropertyName("website")] public string Website { get; set; }

        /// <summary>Icon PNG path relative to the mod folder (Mod Manager).</summary>
        [JsonPropertyName("icon")] public string Icon { get; set; }

        /// <summary>Declarative UI layout files (relative paths) auto-loaded for any mod kind, content mods included.</summary>
        [JsonPropertyName("ui")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] Ui { get; set; }

        /// <summary>Script mods only: watch source files and reload the mod when they change.</summary>
        [JsonPropertyName("hotReload")] public bool HotReload { get; set; }
    }

    /// <summary>No-code content a mod applies to the game.</summary>
    public sealed class ContentSpec
    {
        /// <summary>Texture swaps: replace a named game texture with a PNG shipped in the mod.</summary>
        [JsonPropertyName("textures")] public TextureReplacement[] Textures { get; set; }

        /// <summary>If true, ASL logs a sample of loaded texture names (to help find swap targets).</summary>
        [JsonPropertyName("listTextureNames")] public bool ListTextureNames { get; set; }

        /// <summary>Audio swaps: AudioSources playing a clip named <c>target</c> get a WAV shipped in the mod.</summary>
        [JsonPropertyName("audio")] public AudioReplacement[] Audio { get; set; }

        /// <summary>Text swaps (experimental): on-screen text exactly equal to a key is replaced by its value.</summary>
        [JsonPropertyName("strings")] public Dictionary<string, string> Strings { get; set; }

        /// <summary>If true, ASL logs the distinct AudioClip names it finds (to help find swap targets).</summary>
        [JsonPropertyName("listAudioNames")] public bool ListAudioNames { get; set; }
    }

    /// <summary>One texture swap: game texture named <see cref="Target"/> ← PNG file <see cref="File"/>.</summary>
    public sealed class TextureReplacement
    {
        [JsonPropertyName("target")] public string Target { get; set; }
        [JsonPropertyName("file")]   public string File { get; set; }
    }

    /// <summary>One audio swap: game AudioClip named <see cref="Target"/> ← WAV file <see cref="File"/>.</summary>
    public sealed class AudioReplacement
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

namespace ASL
{
    /// <summary>
    /// Reads a JSON string array, but tolerates a single string (treated as a one-item array) and ignores
    /// any other shape (yielding null) instead of throwing — so an old manifest that already used one of
    /// the new v2 field names for something else can never stop its mod from loading.
    /// </summary>
    internal sealed class LenientStringArrayConverter : JsonConverter<string[]>
    {
        public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return new[] { reader.GetString() };
                case JsonTokenType.StartArray:
                {
                    var list = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String) list.Add(reader.GetString());
                        else reader.Skip();
                    }
                    return list.ToArray();
                }
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
        {
            if (value == null) { writer.WriteNullValue(); return; }
            writer.WriteStartArray();
            foreach (var v in value) writer.WriteStringValue(v);
            writer.WriteEndArray();
        }
    }
}
