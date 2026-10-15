using ASL.Api;
using Xunit;

namespace ASL.Tests
{
    public class VersionTests
    {
        [Theory]
        [InlineData("1.2.3", 1, 2, 3, "")]
        [InlineData("v1.2.3", 1, 2, 3, "")]
        [InlineData("1.2", 1, 2, 0, "")]
        [InlineData("2", 2, 0, 0, "")]
        [InlineData("1.2.3-beta.1", 1, 2, 3, "beta.1")]
        [InlineData("1.2.3-rc.1+build5", 1, 2, 3, "rc.1")]
        public void SemVersion_Parses(string text, int maj, int min, int pat, string pre)
        {
            Assert.True(SemVersion.TryParse(text, out var v));
            Assert.Equal(maj, v.Major); Assert.Equal(min, v.Minor); Assert.Equal(pat, v.Patch);
            Assert.Equal(pre, v.PreRelease);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("a.b.c")]
        [InlineData("1.2.3.4.5")]
        [InlineData("1.2.3.x")]
        [InlineData("1.2.3.")]
        [InlineData("1..3")]
        [InlineData("1.2.3-")]
        [InlineData("-1.0.0")]
        public void SemVersion_RejectsGarbage(string text)
        {
            Assert.False(SemVersion.TryParse(text, out _));
        }

        [Theory]
        [InlineData("1.0.0.0", 1, 0, 0)]
        [InlineData("2.3.4.5", 2, 3, 4)]
        public void SemVersion_AcceptsFourthComponent_AndIgnoresIt(string text, int maj, int min, int pat)
        {
            Assert.True(SemVersion.TryParse(text, out var v));
            Assert.Equal(new SemVersion(maj, min, pat), v);
            Assert.Equal(SemVersion.Parse("1.2.3"), SemVersion.Parse("1.2.3.9"));
        }

        [Theory]
        [InlineData("^1.0.0", "2.0.0-beta.1", false)]
        [InlineData("^1.0.0", "1.5.0-rc.1", false)]
        [InlineData("^1.0.0", "1.5.0", true)]
        [InlineData(">=1.0.0", "1.2.0-beta", false)]
        [InlineData("^1.0.0-beta.1", "1.0.0-beta.2", true)]
        [InlineData("^1.0.0-beta.1", "1.0.0", true)]
        [InlineData("^1.0.0-beta.1", "1.1.0-beta.2", false)]
        [InlineData(">=1.0.0-beta.1 <2.0.0", "1.0.0-rc.1", true)]
        [InlineData("*", "2.0.0-beta.1", true)]
        public void VersionRange_PreReleaseSemantics(string range, string version, bool expected)
        {
            Assert.Equal(expected, VersionRange.Parse(range).Satisfies(version));
        }

        [Fact]
        public void SemVersion_Parse_Throws()
        {
            Assert.Throws<System.FormatException>(() => SemVersion.Parse("nope"));
        }

        [Fact]
        public void SemVersion_Ordering()
        {
            Assert.True(SemVersion.Parse("1.2.3") < SemVersion.Parse("1.2.4"));
            Assert.True(SemVersion.Parse("1.10.0") > SemVersion.Parse("1.9.0"));
            Assert.True(SemVersion.Parse("2.0.0") > SemVersion.Parse("1.99.99"));
            Assert.True(SemVersion.Parse("1.0.0-alpha") < SemVersion.Parse("1.0.0"));
            Assert.True(SemVersion.Parse("1.0.0-alpha") < SemVersion.Parse("1.0.0-beta"));
            Assert.True(SemVersion.Parse("1.0.0-alpha.2") < SemVersion.Parse("1.0.0-alpha.10"));
            Assert.Equal(SemVersion.Parse("1.0.0+a"), SemVersion.Parse("1.0.0+b"));
            Assert.Equal(SemVersion.Parse("1.2"), SemVersion.Parse("1.2.0"));
        }

        [Theory]
        [InlineData(">=1.2.0", "1.2.0", true)]
        [InlineData(">=1.2.0", "1.1.9", false)]
        [InlineData(">1.2.0", "1.2.0", false)]
        [InlineData(">1.2.0", "1.2.1", true)]
        [InlineData("<2.0.0", "1.9.9", true)]
        [InlineData("<2.0.0", "2.0.0", false)]
        [InlineData("<=2.0.0", "2.0.0", true)]
        [InlineData("=1.2.3", "1.2.3", true)]
        [InlineData("=1.2.3", "1.2.4", false)]
        [InlineData("^1.2.3", "1.9.0", true)]
        [InlineData("^1.2.3", "2.0.0", false)]
        [InlineData("^1.2.3", "1.2.2", false)]
        [InlineData("^0.2.3", "0.2.9", true)]
        [InlineData("^0.2.3", "0.3.0", false)]
        [InlineData("^0.0.3", "0.0.3", true)]
        [InlineData("^0.0.3", "0.0.4", false)]
        [InlineData("~1.2.3", "1.2.9", true)]
        [InlineData("~1.2.3", "1.3.0", false)]
        [InlineData("1.2.0", "1.5.0", true)]        // bare = >=
        [InlineData("1.2.0", "1.1.0", false)]
        [InlineData("*", "0.0.1", true)]
        [InlineData("", "9.9.9", true)]
        [InlineData(">=1.0.0 <2.0.0", "1.5.0", true)]
        [InlineData(">=1.0.0 <2.0.0", "2.0.0", false)]
        [InlineData(">= 1.0.0", "1.0.0", true)]
        public void VersionRange_Satisfies(string range, string version, bool expected)
        {
            Assert.True(VersionRange.TryParse(range, out var r));
            Assert.Equal(expected, r.Satisfies(version));
        }

        [Fact]
        public void VersionRange_InvalidVersionNeverSatisfies()
        {
            var r = VersionRange.Parse(">=1.0.0");
            Assert.False(r.Satisfies("garbage"));
            Assert.False(r.Satisfies((SemVersion)null));
        }

        [Theory]
        [InlineData(">=")]
        [InlineData("~~1.0")]
        [InlineData("^abc")]
        public void VersionRange_RejectsGarbage(string text)
        {
            Assert.False(VersionRange.TryParse(text, out _));
            Assert.Throws<System.FormatException>(() => VersionRange.Parse(text));
        }

        [Fact]
        public void VersionRange_Any()
        {
            Assert.True(VersionRange.Any.Satisfies("0.0.1"));
        }

        [Fact]
        public void AslVersion_Basics()
        {
            Assert.Equal("2.0.0", AslVersion.Current);
            Assert.Equal(2, AslVersion.Major);
            Assert.True(AslVersion.AtLeast("1.0.0"));
            Assert.True(AslVersion.AtLeast("2.0.0"));
            Assert.False(AslVersion.AtLeast("2.0.1"));
            Assert.False(AslVersion.AtLeast("nonsense"));
        }
    }
}
