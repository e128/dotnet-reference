using System;
using System.Collections.Immutable;
using System.Linq;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AppSettingsKeyReaderTests
{
    private static bool TryRead(string json, out string[] keys)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        var isValid = AppSettingsKeyReader.TryRead(json, builder);

        keys = [.. builder.ToImmutable().Order(StringComparer.Ordinal)];

        return isValid;
    }

    private static string[] Read(string json)
    {
        Assert.True(TryRead(json, out var keys));

        return keys;
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NestedObjectsAndArrays_YieldColonPaths()
    {
        var keys = Read(
            //lang=json
            """{ "A": { "B": [ 1, { "C": true } ], "D": null }, "E": "x" }""");

        Assert.Equal(["A", "A:B", "A:B:0", "A:B:1", "A:B:1:C", "A:D", "E"], keys);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void CommentsTrailingCommasAndBom_AreAccepted()
    {
        var keys = Read("\uFEFF{ // line\n \"A\": /* block */ [1, 2,], \"B\": { \"C\": 1.5e3, }, }");

        Assert.Equal(["A", "A:0", "A:1", "B", "B:C"], keys);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void TopLevelArray_YieldsIndexPaths()
    {
        var keys = Read(
            //lang=json
            """[ 1, { "A": true } ]""");

        Assert.Equal(["0", "1", "1:A"], keys);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void EscapedKeyNames_AreDecoded()
    {
        var keys = Read(
            //lang=json
            """{ "AB\"\\": 1, "tab\t": 2 }""");

        Assert.Equal(["AB\"\\", "tab\t"], keys);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{ \"A\": }")]
    [InlineData("{ \"A\" 1 }")]
    [InlineData("{ \"A\": 1,, }")]
    [InlineData("{ \"A\": nope }")]
    [InlineData("{ \"A\": \"unterminated }")]
    [InlineData("{ \"A\": 1 } trailing")]
    [InlineData("{ /* open comment ")]
    public void MalformedText_IsRejected(string json)
    {
        Assert.False(TryRead(json, out _));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("+1")]
    [InlineData("01")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("0x10")]
    [InlineData("TRUE")]
    [InlineData("Null")]
    [InlineData("nope")]
    public void InvalidLiteralValue_IsRejected(string value)
    {
        Assert.False(TryRead("{ \"A\": " + value + " }", out _));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("1.5e3")]
    [InlineData("-2E-2")]
    public void JsonNumber_IsAccepted(string value)
    {
        Assert.True(TryRead("{ \"A\": " + value + " }", out _));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void MalformedText_DoesNotLeakPartialKeys()
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        _ = builder.Add("Existing");

        Assert.False(AppSettingsKeyReader.TryRead("""{ "A": 1, "B": }""", builder));
        Assert.Equal("Existing", Assert.Single(builder));
    }

    [Fact]
    [Trait("Category", "CI")]
    public void NestingPastSixtyFourLevels_IsRejected()
    {
        var json = new string('[', 70) + new string(']', 70);

        Assert.False(TryRead(json, out _));
    }
}
