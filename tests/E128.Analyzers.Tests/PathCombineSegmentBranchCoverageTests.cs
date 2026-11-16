using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PathCombineSegmentBranchCoverageTests
{
    [Theory]
    [Trait("Category", "CI")]
    [InlineData("", false)]
    [InlineData("/x", true)]
    [InlineData("\\x", true)]
    [InlineData("C:x", true)]
    [InlineData("z:", true)]
    [InlineData("1:x", false)]
    [InlineData(":x", false)]
    [InlineData("x", false)]
    [InlineData("ab", false)]
    public void IsRooted_RecognizesSeparatorAndDriveRoots(string segment, bool expected)
    {
        Assert.Equal(expected, PathCombineSegmentAnalyzer.IsRooted(segment));
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("a", false)]
    [InlineData("..", false)]
    [InlineData("../a", true)]
    [InlineData("..\\a", true)]
    [InlineData("a/../b", false)]
    [InlineData("a/../../b", true)]
    [InlineData("./a", false)]
    [InlineData("a//b", false)]
    [InlineData("/a/b", false)]
    public void EscapesBase_CountsDepthAcrossSegments(string segment, bool expected)
    {
        Assert.Equal(expected, PathCombineSegmentAnalyzer.EscapesBase(segment));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForNonConstantAndNullSegments()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
            using System.IO;
            class C
            {
                string M(string name) => Path.Combine("base", name) + Path.Combine("base", null!);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EachRiskySegmentAfterTheBase()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
            using System.IO;
            class C
            {
                const string Root = "/etc";

                string M() => Path.Combine("/base", "safe", {|E128161:Root|}, {|E128161:"../up"|});
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCombineOnAnotherPathType()
    {
        return AnalyzerTestRunner.VerifyAsync<PathCombineSegmentAnalyzer>("""
            class Path
            {
                public static string Combine(string a, string b) => a + b;
            }
            class C
            {
                string M() => Path.Combine("base", "/etc");
            }
            """);
    }
}
