using System;
using System.IO;
using System.Text;

namespace ASL.Core
{
    /// <summary>
    /// Turns arbitrary user/mod-supplied names into safe single-segment file names, and joins relative
    /// paths inside a folder without allowing escapes (pure C#). Separators, drive colons, control chars
    /// and Windows-invalid characters become '_'; ".." runs are collapsed; leading/trailing dots and spaces
    /// are trimmed; Windows reserved device names are prefixed. The sanitized name can never contain a path
    /// separator, so it can never escape the folder it is joined to. When sanitizing CHANGED the name (or truncated
    /// it) a short stable hash of the original is appended (<c>"a/b"</c> becomes <c>"a_b~xxxxxxxx"</c>), so different
    /// originals can no longer collide (<c>a/b</c> vs <c>a_b</c>); names that were already clean are returned unchanged.
    /// </summary>
    public static class StorageNames
    {
        /// <summary>Longest name we emit (leaves room for an extension within MAX_PATH-ish limits).</summary>
        public const int MaxLength = 100;

        private static readonly string[] Reserved =
        {
            "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        private const int HashSuffixLength = 9;   // "~" + 8 hex digits

        // FNV-1a (32 bit) over the UTF-16 code units: tiny, deterministic across runs/platforms (unlike string.GetHashCode).
        private static string Fnv1aHex(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var ch in text)
                {
                    h = (h ^ (byte)(ch & 0xFF)) * 16777619;
                    h = (h ^ (byte)(ch >> 8)) * 16777619;
                }
                return h.ToString("x8");
            }
        }

        /// <summary>Sanitize; throws <see cref="ArgumentException"/> if nothing usable remains (null/empty/only dots).</summary>
        public static string ToSafeFileName(string name)
        {
            if (!TryToSafeFileName(name, out var safe)) throw new ArgumentException("storage name is empty or invalid", nameof(name));
            return safe;
        }

        /// <summary>Sanitize; false if nothing usable remains.</summary>
        public static bool TryToSafeFileName(string name, out string safe)
        {
            safe = null;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var sb = new StringBuilder(Math.Min(name.Length, MaxLength + 8));
            foreach (var ch in name)
            {
                if (ch < 32 || ch == '/' || ch == '\\' || ch == ':' || ch == '*' || ch == '?' || ch == '"' || ch == '<' || ch == '>' || ch == '|' || ch == '\u007f')
                    sb.Append('_');
                else
                    sb.Append(ch);
            }

            var s = sb.ToString();
            bool truncated = false;
            while (s.Contains("..")) s = s.Replace("..", ".");
            s = s.Trim(' ', '.');
            if (s.Length == 0) return false;
            if (s.Length > MaxLength - HashSuffixLength) { s = s.Substring(0, MaxLength - HashSuffixLength).TrimEnd(' ', '.'); truncated = true; }
            if (s.Length == 0) return false;

            // "CON", "con.txt", "NUL.x" are device names on Windows regardless of extension.
            int dot = s.IndexOf('.');
            var stem = dot < 0 ? s : s.Substring(0, dot);
            foreach (var r in Reserved)
                if (string.Equals(stem, r, StringComparison.OrdinalIgnoreCase)) { s = "_" + s; break; }

            if (truncated || !string.Equals(s, name, StringComparison.Ordinal))
                s += "~" + Fnv1aHex(name);

            safe = s;
            return true;
        }

        /// <summary>
        /// Resolve <paramref name="relative"/> inside <paramref name="baseDir"/>. Throws
        /// <see cref="UnauthorizedAccessException"/> for rooted paths, drive/stream colons, NUL bytes, or any
        /// path whose normalized form leaves <paramref name="baseDir"/> (".." escapes).
        /// </summary>
        public static string SafeJoin(string baseDir, string relative)
        {
            if (string.IsNullOrEmpty(baseDir)) throw new ArgumentException("base directory missing", nameof(baseDir));
            if (string.IsNullOrWhiteSpace(relative)) throw new ArgumentException("path is empty", nameof(relative));
            if (relative.IndexOf('\0') >= 0 || relative.IndexOf(':') >= 0)
                throw new UnauthorizedAccessException($"path '{relative}' is not allowed");

            // Accept both separators regardless of OS (mod authors write either).
            var rel = relative.Replace('\\', '/');
            if (rel[0] == '/' || Path.IsPathRooted(rel))
                throw new UnauthorizedAccessException($"path '{relative}' must be relative to the mod folder");

            var root = Path.GetFullPath(baseDir);
            var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar.ToString()) ? root : root + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));

            // The game runs on Windows (case-insensitive FS); comparing case-insensitively is the conservative direction.
            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"path '{relative}' escapes the mod folder");
            return full;
        }
    }
}
