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
    /// separator, so it can never escape the folder it is joined to.
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
            while (s.Contains("..")) s = s.Replace("..", ".");
            s = s.Trim(' ', '.');
            if (s.Length == 0) return false;
            if (s.Length > MaxLength) s = s.Substring(0, MaxLength).TrimEnd(' ', '.');
            if (s.Length == 0) return false;

            // "CON", "con.txt", "NUL.x" are device names on Windows regardless of extension.
            int dot = s.IndexOf('.');
            var stem = dot < 0 ? s : s.Substring(0, dot);
            foreach (var r in Reserved)
                if (string.Equals(stem, r, StringComparison.OrdinalIgnoreCase)) { s = "_" + s; break; }

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
