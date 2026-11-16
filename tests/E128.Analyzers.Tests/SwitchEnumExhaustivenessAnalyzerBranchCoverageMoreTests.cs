using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwitchEnumExhaustivenessAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>(
            "enum Color { Red, Green, Blue }\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnExpressionWithAGuardedDiscardArm()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c, bool flag) => c {|E128088:switch|}
                {
                    Color.Red => 1,
                    _ when flag => 2,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnExpressionWithAGuardedVarArm()
    {
        return VerifyAsync("""
            class C
            {
                int M(Color c, bool flag) => c {|E128088:switch|}
                {
                    Color.Red => 1,
                    var other when flag => 2,
                };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePatternIsNotAConstantOrAnOrPattern()
    {
        return VerifyAsync("""
            class C
            {
                void M(Color c)
                {
                    {|E128088:switch|} (c)
                    {
                        case Color.Green and not Color.Blue:
                            break;
                        case > Color.Green:
                            break;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCaseNamesAConstantThatIsNotAnEnumField()
    {
        return VerifyAsync("""
            class C
            {
                void M(Color c)
                {
                    const Color local = Color.Red;
                    {|E128088:switch|} (c)
                    {
                        case local:
                            break;
                        case (Color)7:
                            break;
                    }
                }
            }
            """);
    }
}
