using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumIfElseChainAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFirstConditionIsNotABinaryExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumIfElseChainAnalyzer>("""
            enum Color { Red, Green, Blue }

            class C
            {
                void M(bool flag, Color c)
                {
                    if (flag) { }
                    else if (c == Color.Green) { }
                    else if (c == Color.Blue) { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALaterConditionIsNotABinaryExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumIfElseChainAnalyzer>("""
            enum Color { Red, Green, Blue }

            class C
            {
                void M(bool flag, Color c)
                {
                    if (c == Color.Red) { }
                    else if (c == Color.Green) { }
                    else if (flag) { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheComparisonIsNotAnEqualityTest()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumIfElseChainAnalyzer>("""
            enum Color { Red, Green, Blue }

            class C
            {
                void M(Color c)
                {
                    if (c < Color.Green) { }
                    else if (c < Color.Blue) { }
                    else if (c > Color.Red) { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALiteralNullSitsOnTheLeftOfTheComparison()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumIfElseChainAnalyzer>(
            """
            enum Color { Red, Green, Blue }

            class C
            {
                void M()
                {
                    {|E128048:if (null == Color.Red) { }
                    else if (null == Color.Green) { }
                    else if (null == Color.Blue) { }|}
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRightSideOfTheComparisonIsANullLiteral()
    {
        return AnalyzerTestRunner.VerifyAsync<EnumIfElseChainAnalyzer>("""
            class C
            {
                void M(object o)
                {
                    if (o == null) { }
                    else if (o != null) { }
                    else if (o == null) { }
                }
            }
            """);
    }
}
