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
        [JsonPropertyName("content")] [JsonConverter(typeof(LenientObjectConverter<ContentSpec>))] public ContentSpec Content { get; set; }

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
        [JsonPropertyName("minAslVersion")] [JsonConverter(typeof(LenientStringConverter))] public string MinAslVersion { get; set; }

        /// <summary>Free-form tags shown in the Mod Manager.</summary>
        [JsonPropertyName("tags")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] Tags { get; set; }

        /// <summary>Project/home page URL.</summary>
        [JsonPropertyName("website")] [JsonConverter(typeof(LenientStringConverter))] public string Website { get; set; }

        /// <summary>Icon PNG path relative to the mod folder (Mod Manager).</summary>
        [JsonPropertyName("icon")] [JsonConverter(typeof(LenientStringConverter))] public string Icon { get; set; }

        /// <summary>Declarative UI layout files (relative paths) auto-loaded for any mod kind, content mods included.</summary>
        [JsonPropertyName("ui")] [JsonConverter(typeof(LenientStringArrayConverter))] public string[] Ui { get; set; }

        /// <summary>Script mods only: watch source files and reload the mod when they change.</summary>
        [JsonPropertyName("hotReload")] [JsonConverter(typeof(LenientBoolConverter))] public bool HotReload { get; set; }
    }

    /// <summary>No-code content a mod applies to the game.</summary>
    public sealed class ContentSpec
    {
        /// <summary>Texture swaps: replace a named game texture with a PNG shipped in the mod.</summary>
        [JsonPropertyName("textures")] [JsonConverter(typeof(LenientArrayConverter<TextureReplacement>))] public TextureReplacement[] Textures { get; set; }

        /// <summary>If true, ASL logs a sample of loaded texture names (to help find swap targets).</summary>
        [JsonPropertyName("listTextureNames")] [JsonConverter(typeof(LenientBoolConverter))] public bool ListTextureNames { get; set; }

        /// <summary>Audio swaps: AudioSources playing a clip named <c>target</c> get a WAV shipped in the mod.</summary>
        [JsonPropertyName("audio")] [JsonConverter(typeof(LenientArrayConverter<AudioReplacement>))] public AudioReplacement[] Audio { get; set; }

        /// <summary>Text swaps (experimental): on-screen text exactly equal to a key is replaced by its value.</summary>
        [JsonPropertyName("strings")] [JsonConverter(typeof(LenientStringMapConverter))] public Dictionary<string, string> Strings { get; set; }

        /// <summary>If true, ASL logs the distinct AudioClip names it finds (to help find swap targets).</summary>
        [JsonPropertyName("listAudioNames")] [JsonConverter(typeof(LenientBoolConverter))] public bool ListAudioNames { get; set; }
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

    /// <summary>A string, or null for any other JSON shape (never throws).</summary>
    internal sealed class LenientStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String) return reader.GetString();
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value == null) writer.WriteNullValue(); else writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// A bool: <c>true</c>/<c>false</c>, the strings <c>"true"</c>/<c>"false"</c>/<c>"yes"</c>/<c>"no"</c>/<c>"on"</c>/<c>"off"</c>/<c>"1"</c>/<c>"0"</c>,
    /// or a number (non-zero = true). Anything else is false. Never throws.
    /// </summary>
    internal sealed class LenientBoolConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True: return true;
                case JsonTokenType.False: return false;
                case JsonTokenType.Number:
                    return reader.TryGetDouble(out var d) && d != 0;
                case JsonTokenType.String:
                    switch ((reader.GetString() ?? "").Trim().ToLowerInvariant())
                    {
                        case "true": case "yes": case "on": case "1": return true;
                        default: return false;
                    }
                default:
                    reader.Skip();
                    return false;
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
    }

    /// <summary>An array of objects; elements that are not valid objects of <typeparamref name="T"/> are skipped, any other shape yields null.</summary>
    internal sealed class LenientArrayConverter<T> : JsonConverter<T[]> where T : class
    {
        public override T[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray) { reader.Skip(); return null; }
            var list = new List<T>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }
                try
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    var item = doc.RootElement.Deserialize<T>(options);
                    if (item != null) list.Add(item);
                }
                catch (JsonException) { /* bad element: skip it */ }
            }
            return list.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, T[] value, JsonSerializerOptions options)
        {
            if (value == null) { writer.WriteNullValue(); return; }
            writer.WriteStartArray();
            foreach (var v in value) JsonSerializer.Serialize(writer, v, options);
            writer.WriteEndArray();
        }
    }

    /// <summary>A JSON object of string values; non-string values are skipped, any other shape yields null.</summary>
    internal sealed class LenientStringMapConverter : JsonConverter<Dictionary<string, string>>
    {
        public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); return null; }
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var key = reader.GetString();
                reader.Read();
                if (reader.TokenType == JsonTokenType.String) map[key] = reader.GetString();
                else reader.Skip();
            }
            return map;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
        {
            if (value == null) { writer.WriteNullValue(); return; }
            writer.WriteStartObject();
            foreach (var kv in value) writer.WriteString(kv.Key, kv.Value);
            writer.WriteEndObject();
        }
    }

    /// <summary>A nested object; a non-object value or a malformed object yields null instead of failing the whole manifest.</summary>
    internal sealed class LenientObjectConverter<T> : JsonConverter<T> where T : class
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); return null; }
            try
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                return doc.RootElement.Deserialize<T>(options);
            }
            catch (JsonException) { return null; }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value == null) { writer.WriteNullValue(); return; }
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
