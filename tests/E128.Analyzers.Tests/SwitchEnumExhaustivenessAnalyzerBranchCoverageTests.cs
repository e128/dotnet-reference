using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwitchEnumExhaustivenessAnalyzerBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>(
            "enum Color { Red, Green, Blue }\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnExhaustiveSwitchExpression()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c) => c switch
                {
                    Color.Red => 1,
                    Color.Green => 2,
                    Color.Blue => 3,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnExpressionWithADiscardArm()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c) => c switch
                {
                    Color.Red => 1,
                    _ => 0,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAStatementWithAVarPattern()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c)
                {
                    switch (c)
                    {
                        case Color.Red:
                            return 1;
                        case var other:
                            return (int)other;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnExpressionWithAVarPattern()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c) => c switch
                {
                    Color.Red => 1,
                    var other => (int)other,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAGuardedVarPatternThatDoesNotCoverTheEnum()
    {
        return VerifyAsync("""
            class C
            {
                void M(Color c)
                {
                    {|E128088:switch|} (c)
                    {
                        case Color.Red:
                            break;
                        case var value when value == Color.Green:
                            break;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForTheRemainingMemberAfterAnOrPattern()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c) => c {|E128088:switch|}
                {
                    Color.Red or Color.Green => 1,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForASwitchExpressionOverAString()
    {
        return VerifyAsync("""
            class C
            {
                int M(string text) => text switch
                {
                    "x" => 1,
                    _ => 0,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForASwitchOverANullableEnum()
    {
        return VerifyAsync("""
            class C
            {
                void M(Color? c)
                {
                    switch (c)
                    {
                        case Color.Red:
                            break;
                    }
                }
            }
            """);
    }

}
