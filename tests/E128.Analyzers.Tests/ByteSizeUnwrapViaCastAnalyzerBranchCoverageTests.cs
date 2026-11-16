using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ByteSizeUnwrapViaCastAnalyzerBranchCoverageTests
{
    private const string Stubs = """
        namespace Pug.Core.Classes
        {
            public struct ByteSize
            {
                public double Bytes { get; set; }
                public long Bits { get; set; }
                public double Kilobytes { get; set; }
                public double Megabytes { get; set; }
                public double Gigabytes { get; set; }
                public double Terabytes { get; set; }
                public double Other { get; set; }
                public double BytesField;
            }
        }

        class Measure
        {
            public double Bytes { get; set; }
        }

        """;

    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<ByteSizeUnwrapViaCastAnalyzer>(
            "using Pug.Core.Classes;\n" + Stubs + "class C { void M(ByteSize size, Measure measure) { " + body + " } }");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("Bits")]
    [InlineData("Kilobytes")]
    [InlineData("Megabytes")]
    [InlineData("Gigabytes")]
    [InlineData("Terabytes")]
    public Task Reports_ForEachUnitProperty(string property)
    {
        return VerifyAsync("var x = {|E128082:(long)size." + property + "|};");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("byte")]
    [InlineData("short")]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("decimal")]
    public Task Reports_ForEachNumericTarget(string type)
    {
        return VerifyAsync("var x = {|E128082:(" + type + ")size.Bytes|};");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ThroughParentheses()
    {
        return VerifyAsync("var x = {|E128082:(long)((size.Bytes))|};");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCastsThatAreNotAnUnwrap()
    {
        return VerifyAsync("""
            var a = (long)(size.Bytes + 1);
            var b = (object)size.Bytes;
            var c = (long)measure.Bytes;
            var d = (long)size.Other;
            var e = (long)size.BytesField;
            var f = (long)Value();
            var g = (double)(size.Bytes * 2);
            long Value() { return 1; }
            """);
    }
}
