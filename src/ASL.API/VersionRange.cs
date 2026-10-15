using System;
using System.Collections.Generic;

namespace ASL.Api
{
    /// <summary>
    /// A version requirement like those in a manifest's <c>dependencies</c> (<c>"other.mod@&gt;=1.2.0"</c>).
    /// Supported forms (several may be joined by spaces; all must hold):
    /// <list type="bullet">
    /// <item><c>&gt;=1.2.0</c>, <c>&gt;1.2.0</c>, <c>&lt;2.0.0</c>, <c>&lt;=2.0.0</c></item>
    /// <item><c>=1.2.0</c> — exactly that version</item>
    /// <item><c>^1.2.3</c> — compatible: &gt;=1.2.3 and &lt;2.0.0 (for 0.x: &lt;0.(minor+1).0; for 0.0.x: exactly that patch)</item>
    /// <item><c>~1.2.3</c> — approximately: &gt;=1.2.3 and &lt;1.3.0</item>
    /// <item><c>1.2.3</c> (bare) — same as <c>&gt;=1.2.3</c></item>
    /// <item>empty or <c>*</c> — any version</item>
    /// </list>
    /// Pre-release versions (<c>2.0.0-beta.1</c>) follow npm semantics: they satisfy a range only if one of the
    /// range's own bounds is a pre-release of the same <c>major.minor.patch</c> (so <c>^1.0.0</c> does NOT accept
    /// <c>2.0.0-beta.1</c> or <c>1.5.0-rc.1</c>, but <c>^1.0.0-beta.1</c> accepts <c>1.0.0-beta.2</c>). A range with
    /// no bounds (<c>*</c>) matches everything, pre-releases included.
    /// <code>
    /// VersionRange.Parse("^1.2.0").Satisfies("1.5.1");   // true
    /// VersionRange.Parse("^1.2.0").Satisfies("2.0.0");   // false
    /// </code>
    /// </summary>
    public sealed class VersionRange
    {
        private readonly struct Bound
        {
            public readonly int Op;   // 0 >=, 1 >, 2 <, 3 <=, 4 ==
            public readonly SemVersion Version;
            public Bound(int op, SemVersion v) { Op = op; Version = v; }
            public bool Test(SemVersion v)
            {
                int c = v.CompareTo(Version);
                switch (Op)
                {
                    case 0: return c >= 0;
                    case 1: return c > 0;
                    case 2: return c < 0;
                    case 3: return c <= 0;
                    default: return c == 0;
                }
            }
        }

        private readonly List<Bound> _bounds;
        private readonly string _text;

        private VersionRange(List<Bound> bounds, string text) { _bounds = bounds; _text = text; }

        /// <summary>A range that matches every version.</summary>
        public static VersionRange Any => new VersionRange(new List<Bound>(), "*");

        /// <summary>Parse a range; throws <see cref="FormatException"/> if invalid.</summary>
        public static VersionRange Parse(string text)
        {
            if (!TryParse(text, out var r)) throw new FormatException("Not a valid version range: '" + text + "'");
            return r;
        }

        /// <summary>Try to parse a range (see the class remarks).</summary>
        public static bool TryParse(string text, out VersionRange range)
        {
            range = null;
            var trimmed = (text ?? "").Trim();
            var bounds = new List<Bound>();
            if (trimmed.Length == 0 || trimmed == "*") { range = new VersionRange(bounds, "*"); return true; }

            var tokens = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                string op = "";
                // Allow ">= 1.2.0" (operator and version split by a space).
                int k = 0;
                while (k < t.Length && (t[k] == '>' || t[k] == '<' || t[k] == '=' || t[k] == '^' || t[k] == '~')) k++;
                op = t.Substring(0, k);
                string ver = t.Substring(k);
                if (ver.Length == 0 && op.Length > 0 && i + 1 < tokens.Length) ver = tokens[++i];
                if (!SemVersion.TryParse(ver, out var v)) return false;

                switch (op)
                {
                    case "": case ">=": bounds.Add(new Bound(0, v)); break;
                    case ">": bounds.Add(new Bound(1, v)); break;
                    case "<": bounds.Add(new Bound(2, v)); break;
                    case "<=": bounds.Add(new Bound(3, v)); break;
                    case "=": case "==": bounds.Add(new Bound(4, v)); break;
                    case "^":
                        bounds.Add(new Bound(0, v));
                        if (v.Major > 0) bounds.Add(new Bound(2, new SemVersion(v.Major + 1)));
                        else if (v.Minor > 0) bounds.Add(new Bound(2, new SemVersion(0, v.Minor + 1)));
                        else bounds.Add(new Bound(2, new SemVersion(0, 0, v.Patch + 1)));
                        break;
                    case "~":
                        bounds.Add(new Bound(0, v));
                        bounds.Add(new Bound(2, new SemVersion(v.Major, v.Minor + 1)));
                        break;
                    default: return false;
                }
            }
            range = new VersionRange(bounds, trimmed);
            return true;
        }

        /// <summary>True if <paramref name="version"/> meets every bound.</summary>
        public bool Satisfies(SemVersion version)
        {
            if (version == null) return false;
            if (version.IsPreRelease && _bounds.Count > 0 && !AllowsPreRelease(version)) return false;
            for (int i = 0; i < _bounds.Count; i++)
                if (!_bounds[i].Test(version)) return false;
            return true;
        }

        // npm rule: a pre-release only matches if some bound is a pre-release of the same major.minor.patch.
        private bool AllowsPreRelease(SemVersion v)
        {
            for (int i = 0; i < _bounds.Count; i++)
            {
                var b = _bounds[i].Version;
                if (b.IsPreRelease && b.Major == v.Major && b.Minor == v.Minor && b.Patch == v.Patch) return true;
            }
            return false;
        }

        /// <summary>Parse <paramref name="version"/> and test it; false if it is not a valid version.</summary>
        public bool Satisfies(string version) => SemVersion.TryParse(version, out var v) && Satisfies(v);

        /// <summary>The range text as written.</summary>
        public override string ToString() => _text;
    }
}
