using System.Collections.Generic;
using System.Text.Json;
using ASL.Core;
using Xunit;

namespace ASL.Data.Tests
{
    public class LanguageResolverTests
    {
        private static readonly string[] EnRu = { "en", "ru" };

        [Theory]
        [InlineData("ru", "ru")]
        [InlineData("RU", "ru")]
        [InlineData("en", "en")]
        [InlineData("en-US", "en")]
        [InlineData("en_GB", "en")]
        [InlineData("ru-RU", "ru")]
        [InlineData("Russian", "ru")]
        [InlineData("русский", "ru")]
        [InlineData("Русский", "ru")]
        [InlineData("English", "en")]
        [InlineData("english (united states)", "en")]
        public void PicksFromEnRu(string token, string expected) => Assert.Equal(expected, LanguageResolver.Pick(EnRu, token));

        [Fact]
        public void ExactFileNameWins_CaseInsensitive_ReturnsOriginalSpelling()
        {
            Assert.Equal("pt-BR", LanguageResolver.Pick(new[] { "en", "pt", "pt-BR" }, "pt-br"));
            Assert.Equal("pt", LanguageResolver.Pick(new[] { "en", "pt", "pt-BR" }, "pt-PT"));   // region missing → base code
        }

        [Fact]
        public void BaseFileMatchesRegionalToken_AndViceVersa()
        {
            Assert.Equal("en-US", LanguageResolver.Pick(new[] { "en-US", "ru" }, "en"));
            Assert.Equal("zh-CN", LanguageResolver.Pick(new[] { "en", "zh-CN" }, "Chinese (Simplified)"));
            Assert.Equal("zh", LanguageResolver.Pick(new[] { "en", "zh" }, "中文"));
        }

        [Theory]
        [InlineData("French")]   // no fr file
        [InlineData("")]
        [InlineData(null)]
        [InlineData("klingon")]
        public void FallsBackToEnglish(string token) => Assert.Equal("en", LanguageResolver.Pick(EnRu, token));

        [Fact]
        public void NoEnglish_FirstAvailable() => Assert.Equal("ru", LanguageResolver.Pick(new[] { "ru", "de" }, "fr"));

        [Fact]
        public void Empty_ReturnsNull()
        {
            Assert.Null(LanguageResolver.Pick(new string[0], "en"));
            Assert.Null(LanguageResolver.Pick(null, "en"));
        }

        [Fact]
        public void TwoLetterPrefix_ForUnknownNames() => Assert.Equal("de", LanguageResolver.Pick(new[] { "en", "de" }, "Deutschland"));

        [Theory]
        [InlineData("Russian", "ru")]
        [InlineData("ukrainian", "uk")]
        [InlineData("Chinese", "zh")]
        [InlineData("日本語", "ja")]
        [InlineData("한국어", "ko")]
        [InlineData("Português (Brasil)", "pt")]
        [InlineData("nothing", null)]
        public void CodeForName(string name, string code) => Assert.Equal(code, LanguageResolver.CodeForName(name));
    }

    public class JsonLangTableTests
    {
        [Fact]
        public void Flat()
        {
            var t = JsonLangTable.Parse("{ \"hello\": \"Hi\", \"a.b\": \"dotted\" }");
            Assert.Equal("Hi", t["hello"]);
            Assert.Equal("dotted", t["a.b"]);
            Assert.Equal(2, t.Count);
        }

        [Fact]
        public void Nested_BecomesDottedKeys()
        {
            var t = JsonLangTable.Parse("{ \"menu\": { \"play\": \"Play\", \"opts\": { \"sound\": \"Sound\" } }, \"top\": \"T\" }");
            Assert.Equal("Play", t["menu.play"]);
            Assert.Equal("Sound", t["menu.opts.sound"]);
            Assert.Equal("T", t["top"]);
            Assert.Equal(3, t.Count);
        }

        [Fact]
        public void Mixed_ArraysNumbersBoolsNulls()
        {
            var t = JsonLangTable.Parse("{ \"list\": [\"a\", \"b\"], \"n\": 5, \"f\": 1.5, \"b\": true, \"z\": null }");
            Assert.Equal("a", t["list.0"]);
            Assert.Equal("b", t["list.1"]);
            Assert.Equal("5", t["n"]);
            Assert.Equal("1.5", t["f"]);
            Assert.Equal("true", t["b"]);
            Assert.False(t.ContainsKey("z"));
        }

        [Fact]
        public void CommentsAndTrailingCommas_Tolerated()
        {
            var t = JsonLangTable.Parse("{\n // greeting\n \"a\": \"x\", /* c */ \"b\": \"y\",\n}");
            Assert.Equal("x", t["a"]);
            Assert.Equal("y", t["b"]);
        }

        [Fact]
        public void Unicode_AndFormatPlaceholders_Preserved()
        {
            var t = JsonLangTable.Parse("{ \"g\": \"Привет, {0}! \\u263A\" }");
            Assert.Equal("Привет, {0}! \u263A", t["g"]);
        }

        [Fact]
        public void KeysAreCaseSensitive()
        {
            var t = JsonLangTable.Parse("{ \"A\": \"1\", \"a\": \"2\" }");
            Assert.Equal("1", t["A"]);
            Assert.Equal("2", t["a"]);
        }

        [Fact]
        public void EmptyInput_GivesEmptyTable()
        {
            Assert.Empty(JsonLangTable.Parse(""));
            Assert.Empty(JsonLangTable.Parse("   "));
            Assert.Empty(JsonLangTable.Parse("{}"));
        }

        [Theory]
        [InlineData("[1,2]")]
        [InlineData("\"str\"")]
        public void NonObjectRoot_Throws(string json) => Assert.Throws<JsonException>(() => JsonLangTable.Parse(json));

        [Theory]
        [InlineData("{ \"a\": ")]
        [InlineData("not json")]
        [InlineData("{ a: 1 }")]
        public void Invalid_Throws(string json) => Assert.ThrowsAny<JsonException>(() => JsonLangTable.Parse(json));
    }
}
