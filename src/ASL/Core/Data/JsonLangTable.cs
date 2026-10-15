using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ASL.Core
{
    /// <summary>
    /// Parses a language file into a flat key → text table (pure C#). Accepts flat objects
    /// (<c>{"a.b": "x"}</c>) and nested ones (<c>{"a": {"b": "x"}}</c>, flattened to dotted keys); arrays
    /// flatten to <c>key.0</c>, <c>key.1</c>; numbers/booleans become their text; nulls are skipped.
    /// Comments and trailing commas are tolerated.
    /// </summary>
    public static class JsonLangTable
    {
        /// <summary>Parse JSON text. Throws <see cref="JsonException"/> for invalid JSON or a non-object root.</summary>
        public static Dictionary<string, string> Parse(string json)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json)) return table;

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                MaxDepth = 32,
            });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("language file root must be a JSON object");
            Flatten(doc.RootElement, null, table);
            return table;
        }

        private static void Flatten(JsonElement e, string prefix, Dictionary<string, string> into)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                        Flatten(p.Value, prefix == null ? p.Name : prefix + "." + p.Name, into);
                    break;
                case JsonValueKind.Array:
                    int i = 0;
                    foreach (var item in e.EnumerateArray())
                        Flatten(item, (prefix ?? "") + "." + (i++).ToString(CultureInfo.InvariantCulture), into);
                    break;
                case JsonValueKind.String:
                    if (prefix != null) into[prefix] = e.GetString();
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (prefix != null) into[prefix] = e.GetRawText();
                    break;
                // Null / Undefined: skipped
            }
        }
    }
}
