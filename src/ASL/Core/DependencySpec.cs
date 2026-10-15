using System;
using ASL.Api;

namespace ASL.Core
{
    /// <summary>
    /// One entry of a manifest's <c>dependencies</c> / <c>optionalDependencies</c> / <c>loadAfter</c> /
    /// <c>loadBefore</c> list: <c>"modId"</c> (any version) or <c>"modId@&lt;range&gt;"</c>, e.g.
    /// <c>"coreutils@&gt;=1.2.0"</c> (range syntax: see <see cref="VersionRange"/>).
    /// </summary>
    public sealed class DependencySpec
    {
        /// <summary>The required mod's id.</summary>
        public string Id { get; }

        /// <summary>The accepted versions (<see cref="VersionRange.Any"/> when no range was given).</summary>
        public VersionRange Range { get; }

        /// <summary>True if a range was written.</summary>
        public bool HasRange { get; }

        /// <summary>The text exactly as written in the manifest (trimmed).</summary>
        public string Raw { get; }

        private DependencySpec(string id, VersionRange range, bool hasRange, string raw)
        {
            Id = id; Range = range; HasRange = hasRange; Raw = raw;
        }

        /// <summary>Parse <paramref name="text"/>; returns false (with a reason in <paramref name="error"/>) if malformed.</summary>
        public static bool TryParse(string text, out DependencySpec spec, out string error)
        {
            spec = null; error = null;
            var raw = (text ?? "").Trim();
            if (raw.Length == 0) { error = "empty dependency entry"; return false; }

            int at = raw.IndexOf('@');
            if (at < 0)
            {
                spec = new DependencySpec(raw, VersionRange.Any, false, raw);
                return true;
            }

            var id = raw.Substring(0, at).Trim();
            var rangeText = raw.Substring(at + 1).Trim();
            if (id.Length == 0) { error = "dependency '" + raw + "' has no mod id before '@'"; return false; }
            if (!VersionRange.TryParse(rangeText, out var range))
            {
                error = "dependency '" + raw + "' has an invalid version range '" + rangeText + "'";
                return false;
            }
            spec = new DependencySpec(id, range, rangeText.Length > 0 && rangeText != "*", raw);
            return true;
        }

        /// <summary>Parse <paramref name="text"/> or throw <see cref="FormatException"/>.</summary>
        public static DependencySpec Parse(string text)
        {
            if (!TryParse(text, out var s, out var err)) throw new FormatException(err);
            return s;
        }

        /// <summary>True if a mod with <paramref name="version"/> satisfies this spec's range (always true when no range).</summary>
        public bool IsSatisfiedBy(string version) => !HasRange || Range.Satisfies(version);

        public override string ToString() => Raw;
    }
}
