using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace ASL.Tests
{
    public class ManifestTests
    {
        // The same options ModLoader uses.
        private static readonly JsonSerializerOptions Opts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static ModManifest Parse(string json) => JsonSerializer.Deserialize<ModManifest>(json, Opts);

        [Fact]
        public void WrongShapes_DoNotThrow_AndFallBackToDefaults()
        {
            var m = Parse(@"{ ""id"": ""x"", ""icon"": 5, ""website"": [1], ""minAslVersion"": { ""a"": 1 },
                              ""hotReload"": ""yes"",
                              ""content"": { ""audio"": ""x"", ""strings"": [1,2], ""textures"": 3, ""listAudioNames"": ""no"" } }");
            Assert.Equal("x", m.Id);
            Assert.Null(m.Icon);
            Assert.Null(m.Website);
            Assert.Null(m.MinAslVersion);
            Assert.True(m.HotReload);   // "yes" is accepted as true
            Assert.NotNull(m.Content);
            Assert.Null(m.Content.Audio);
            Assert.Null(m.Content.Strings);
            Assert.Null(m.Content.Textures);
            Assert.False(m.Content.ListAudioNames);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("\"true\"", true)]
        [InlineData("\"false\"", false)]
        [InlineData("1", true)]
        [InlineData("0", false)]
        [InlineData("\"garbage\"", false)]
        [InlineData("null", false)]
        [InlineData("[1]", false)]
        public void HotReload_IsLenient(string value, bool expected)
        {
            Assert.Equal(expected, Parse(@"{ ""id"": ""x"", ""hotReload"": " + value + " }").HotReload);
        }

        [Fact]
        public void ContentWrongShape_IsNull()
        {
            Assert.Null(Parse(@"{ ""id"": ""x"", ""content"": ""nope"" }").Content);
        }

        [Fact]
        public void BadArrayElements_AreSkipped()
        {
            var m = Parse(@"{ ""id"": ""x"", ""content"": { ""audio"": [ 5, { ""target"": ""a"", ""file"": ""a.wav"" }, { ""target"": 7 } ],
                                                       ""strings"": { ""Hi"": ""Yo"", ""bad"": 3 } } }");
            Assert.Single(m.Content.Audio);
            Assert.Equal("a", m.Content.Audio[0].Target);
            Assert.Single(m.Content.Strings);
            Assert.Equal("Yo", m.Content.Strings["Hi"]);
        }

        [Fact]
        public void WellFormedV2Fields_StillParse()
        {
            var m = Parse(@"{ ""id"": ""x"", ""icon"": ""i.png"", ""website"": ""https://e.x"", ""minAslVersion"": ""2.0.0"", ""hotReload"": true,
                              ""dependencies"": [""a@^1.0.0""] }");
            Assert.Equal("i.png", m.Icon);
            Assert.Equal("https://e.x", m.Website);
            Assert.Equal("2.0.0", m.MinAslVersion);
            Assert.True(m.HotReload);
            Assert.Equal(new[] { "a@^1.0.0" }, m.Dependencies);
        }

        [Fact]
        public void ExistingSampleAndTemplateManifests_Parse()
        {
            var root = FindRepoRoot();
            int n = 0;
            foreach (var dir in new[] { "samples", "templates" })
                foreach (var f in Directory.GetFiles(Path.Combine(root, dir), "manifest.json", SearchOption.AllDirectories))
                {
                    var m = Parse(File.ReadAllText(f));
                    Assert.False(string.IsNullOrWhiteSpace(m.Id), f);
                    n++;
                }
            Assert.True(n >= 5, "expected the sample/template manifests to be found");
        }

        private static string FindRepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "samples"))) d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("repo root (samples/) not found");
        }
    }
}
