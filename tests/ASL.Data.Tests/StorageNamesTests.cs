using System;
using System.IO;
using ASL.Core;
using Xunit;

namespace ASL.Data.Tests
{
    public class StorageNamesTests
    {
        [Theory]
        [InlineData("save", "save")]
        [InlineData("my.mod.id", "my.mod.id")]
        [InlineData("high scores", "high scores")]
        [InlineData("../../evil", "_._evil*")]   // ".." collapsed, separators replaced
        [InlineData("a/b\\c", "a_b_c*")]
        [InlineData("C:\\Windows\\x", "C__Windows_x*")]
        [InlineData("..", null)]
        [InlineData(".", null)]
        [InlineData("   ", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        [InlineData("na\0me", "na_me*")]
        [InlineData("what?*<>|\"", "what______*")]
        [InlineData("trailing. . ", "trailing*")]
        [InlineData("CON", "_CON*")]
        [InlineData("nul.txt", "_nul.txt*")]
        [InlineData("com1", "_com1*")]
        [InlineData("console", "console")]
        public void Sanitizes(string input, string expected)
        {
            bool ok = StorageNames.TryToSafeFileName(input, out var safe);
            Assert.Equal(expected != null, ok);
            if (expected != null && expected.EndsWith("*"))
            {
                // Changed by sanitizing: "<sanitized>~<8 hex of the original>".
                var prefix = expected.Substring(0, expected.Length - 1) + "~";
                Assert.StartsWith(prefix, safe);
                Assert.Equal(prefix.Length + 8, safe.Length);
                Assert.Matches("^[0-9a-f]{8}$", safe.Substring(prefix.Length));
            }
            else Assert.Equal(expected, safe);
        }

        [Fact]
        public void ChangedNames_DoNotCollideWithTheirSanitizedForm()
        {
            var a = StorageNames.ToSafeFileName("a/b");
            var b = StorageNames.ToSafeFileName("a_b");
            Assert.Equal("a_b", b);          // already clean: unchanged, so existing files are still found
            Assert.NotEqual(a, b);
            Assert.NotEqual(StorageNames.ToSafeFileName("a/b"), StorageNames.ToSafeFileName("a\\b"));
        }

        [Fact]
        public void Hash_IsStable()
        {
            Assert.Equal(StorageNames.ToSafeFileName("a/b"), StorageNames.ToSafeFileName("a/b"));
            // FNV-1a over UTF-16 code units - pinned so saved files keep being found across versions.
            Assert.Equal("a_b~" + "a22d4fe9", StorageNames.ToSafeFileName("a/b"));
        }

        [Fact]
        public void Throws_WhenNothingUsable()
        {
            Assert.Throws<ArgumentException>(() => StorageNames.ToSafeFileName(".."));
            Assert.Throws<ArgumentException>(() => StorageNames.ToSafeFileName(null));
        }

        [Fact]
        public void LongNames_AreTruncated()
        {
            var safe = StorageNames.ToSafeFileName(new string('a', 500));
            Assert.True(safe.Length <= StorageNames.MaxLength);
            Assert.Contains("~", safe);                                   // truncated -> hashed
            Assert.NotEqual(safe, StorageNames.ToSafeFileName(new string('a', 499) + "b"));
        }

        [Theory]
        [InlineData("../x")]
        [InlineData("..\\x")]
        [InlineData("a/../../x")]
        [InlineData("a\\..\\..\\x")]
        [InlineData("/etc/passwd")]
        [InlineData("\\windows\\system32")]
        [InlineData("C:\\x")]
        [InlineData("C:x")]
        [InlineData("file.txt:stream")]
        [InlineData("a\0b")]
        [InlineData("..")]
        public void SafeJoin_RejectsEscapes(string rel)
        {
            var root = Path.Combine(Path.GetTempPath(), "aslmod");
            Assert.ThrowsAny<Exception>(() => StorageNames.SafeJoin(root, rel));
        }

        [Theory]
        [InlineData("a.png", "a.png")]
        [InlineData("sub/a.png", "sub/a.png")]
        [InlineData("sub\\a.png", "sub/a.png")]
        [InlineData("./a.png", "a.png")]
        [InlineData("sub/../a.png", "a.png")]
        [InlineData("a/b/../c/d.wav", "a/c/d.wav")]
        public void SafeJoin_AllowsInside(string rel, string expectedRel)
        {
            var root = Path.Combine(Path.GetTempPath(), "aslmod");
            var full = StorageNames.SafeJoin(root, rel);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, expectedRel.Replace('/', Path.DirectorySeparatorChar))), full);
        }

        [Fact]
        public void SafeJoin_RejectsSiblingWithSamePrefix()
        {
            // "<root>" vs "<root>-evil": a naive StartsWith(root) would accept the second.
            var root = Path.Combine(Path.GetTempPath(), "aslmod");
            Assert.ThrowsAny<Exception>(() => StorageNames.SafeJoin(root, "../aslmod-evil/x"));
        }

        [Fact]
        public void SafeJoin_EmptyThrows()
        {
            var root = Path.GetTempPath();
            Assert.Throws<ArgumentException>(() => StorageNames.SafeJoin(root, ""));
            Assert.Throws<ArgumentException>(() => StorageNames.SafeJoin(root, "  "));
        }
    }
}
