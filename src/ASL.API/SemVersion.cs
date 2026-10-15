using System;
using System.Globalization;

namespace ASL.Api
{
    /// <summary>
    /// A semantic version (<c>major.minor.patch[-prerelease][+build]</c>). Pure C#, no game types.
    /// Lenient on input: <c>"1.2"</c> means 1.2.0, <c>"1"</c> means 1.0.0, a leading <c>v</c> is ignored.
    /// Build metadata is ignored when comparing. A version with a pre-release tag sorts before the same
    /// version without one (<c>1.0.0-beta.1 &lt; 1.0.0</c>).
    /// <code>
    /// if (SemVersion.TryParse(info.Version, out var v) &amp;&amp; v >= SemVersion.Parse("1.2.0")) { ... }
    /// </code>
    /// </summary>
    public sealed class SemVersion : IComparable<SemVersion>, IEquatable<SemVersion>
    {
        /// <summary>Major number.</summary>
        public int Major { get; }
        /// <summary>Minor number.</summary>
        public int Minor { get; }
        /// <summary>Patch number.</summary>
        public int Patch { get; }
        /// <summary>Pre-release tag without the leading dash (e.g. <c>"beta.1"</c>), or empty.</summary>
        public string PreRelease { get; }
        /// <summary>Build metadata without the leading plus, or empty. Ignored by comparison.</summary>
        public string Build { get; }

        /// <summary>True if <see cref="PreRelease"/> is non-empty.</summary>
        public bool IsPreRelease => PreRelease.Length > 0;

        /// <summary>Create a version from its parts.</summary>
        public SemVersion(int major, int minor = 0, int patch = 0, string preRelease = null, string build = null)
        {
            Major = major; Minor = minor; Patch = patch;
            PreRelease = preRelease ?? "";
            Build = build ?? "";
        }

        /// <summary>Parse a version string; throws <see cref="FormatException"/> if it is not valid.</summary>
        public static SemVersion Parse(string text)
        {
            if (!TryParse(text, out var v)) throw new FormatException("Not a valid version: '" + text + "'");
            return v;
        }

        /// <summary>Try to parse a version string (see the class remarks for tolerated forms).</summary>
        public static bool TryParse(string text, out SemVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var s = text.Trim();
            if (s.Length > 1 && (s[0] == 'v' || s[0] == 'V') && char.IsDigit(s[1])) s = s.Substring(1);

            string build = "";
            int plus = s.IndexOf('+');
            if (plus >= 0) { build = s.Substring(plus + 1); s = s.Substring(0, plus); }

            string pre = "";
            int dash = s.IndexOf('-');
            if (dash >= 0) { pre = s.Substring(dash + 1); s = s.Substring(0, dash); if (pre.Length == 0) return false; }

            var parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 3) return false;
            var nums = new int[3];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out nums[i])) return false;
            }
            version = new SemVersion(nums[0], nums[1], nums[2], pre, build);
            return true;
        }

        /// <summary>Compare two versions by precedence (build metadata ignored).</summary>
        public int CompareTo(SemVersion other)
        {
            if (ReferenceEquals(other, null)) return 1;
            int c = Major.CompareTo(other.Major); if (c != 0) return c;
            c = Minor.CompareTo(other.Minor); if (c != 0) return c;
            c = Patch.CompareTo(other.Patch); if (c != 0) return c;
            return ComparePreRelease(PreRelease, other.PreRelease);
        }

        private static int ComparePreRelease(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 0;
            if (a.Length == 0) return 1;     // release > pre-release
            if (b.Length == 0) return -1;
            var pa = a.Split('.');
            var pb = b.Split('.');
            int n = Math.Min(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                bool na = int.TryParse(pa[i], NumberStyles.None, CultureInfo.InvariantCulture, out int ia);
                bool nb = int.TryParse(pb[i], NumberStyles.None, CultureInfo.InvariantCulture, out int ib);
                int c;
                if (na && nb) c = ia.CompareTo(ib);
                else if (na) c = -1;          // numeric identifiers sort before alphanumeric
                else if (nb) c = 1;
                else c = string.CompareOrdinal(pa[i], pb[i]);
                if (c != 0) return c < 0 ? -1 : 1;
            }
            return pa.Length.CompareTo(pb.Length);
        }

        /// <summary>True if the two versions have equal precedence.</summary>
        public bool Equals(SemVersion other) => !ReferenceEquals(other, null) && CompareTo(other) == 0;

        /// <inheritdoc />
        public override bool Equals(object obj) => Equals(obj as SemVersion);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked(((Major * 397 ^ Minor) * 397 ^ Patch) * 397 ^ PreRelease.GetHashCode());

        /// <summary>Canonical text, e.g. <c>"1.2.3-beta.1+abc"</c>.</summary>
        public override string ToString()
        {
            var s = Major + "." + Minor + "." + Patch;
            if (PreRelease.Length > 0) s += "-" + PreRelease;
            if (Build.Length > 0) s += "+" + Build;
            return s;
        }

        /// <summary>Equality by precedence.</summary>
        public static bool operator ==(SemVersion a, SemVersion b) => ReferenceEquals(a, null) ? ReferenceEquals(b, null) : a.Equals(b);
        /// <summary>Inequality by precedence.</summary>
        public static bool operator !=(SemVersion a, SemVersion b) => !(a == b);
        /// <summary>Less than.</summary>
        public static bool operator <(SemVersion a, SemVersion b) => ReferenceEquals(a, null) ? !ReferenceEquals(b, null) : a.CompareTo(b) < 0;
        /// <summary>Greater than.</summary>
        public static bool operator >(SemVersion a, SemVersion b) => b < a;
        /// <summary>Less than or equal.</summary>
        public static bool operator <=(SemVersion a, SemVersion b) => !(a > b);
        /// <summary>Greater than or equal.</summary>
        public static bool operator >=(SemVersion a, SemVersion b) => !(a < b);
    }
}
