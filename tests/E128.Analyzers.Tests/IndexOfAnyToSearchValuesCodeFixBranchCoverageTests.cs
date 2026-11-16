using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class IndexOfAnyToSearchValuesCodeFixBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<IndexOfAnyToSearchValuesAnalyzer, IndexOfAnyToSearchValuesCodeFixProvider>(source, fixedCode);
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<IndexOfAnyToSearchValuesAnalyzer, IndexOfAnyToSearchValuesCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_HandlesAnExplicitCharArray()
    {
        return VerifyFixAsync(
            """
            class C
            {
                void M(string path)
                {
                    var i = {|E128102:path.IndexOfAny(new char[] { '/', '\\' })|};
                }
            }
            """,
            """
            using System;

            class C
            {
                private static readonly System.Buffers.SearchValues<char> _pathChars = System.Buffers.SearchValues.Create("/\\");

                void M(string path)
                {
                    var i = path.AsSpan().IndexOfAny(_pathChars);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheTypeAlreadyHasAMemberWithTheFieldName()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                private static readonly int _pathChars = 0;

                void M(string path)
                {
                    var i = {|E128102:path.IndexOfAny(new[] { '/', '\\' })|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAnElementIsNotACharLiteral()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(string path, char separator)
                {
                    var i = {|E128102:path.IndexOfAny(new[] { separator, '\\' })|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheArrayIsEmpty()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(string path)
                {
                    var i = {|E128102:path.IndexOfAny(new char[] { })|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenAStartIndexIsPassed()
    {
        return VerifyNoFixAsync(
            """
            class C
            {
                void M(string path)
                {
                    var i = {|E128102:path.IndexOfAny(new[] { '/', '\\' }, 1)|};
                }
            }
            """);
    }
}
