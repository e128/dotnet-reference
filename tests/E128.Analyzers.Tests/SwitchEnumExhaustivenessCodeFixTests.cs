using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwitchEnumExhaustivenessCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<SwitchEnumExhaustivenessAnalyzer, SwitchEnumExhaustivenessCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsThrowingDefaultToSwitchStatement()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                int M(DayOfWeek day)
                {
                    {|E128088:switch|} (day)
                    {
                        case DayOfWeek.Monday:
                            return 1;
                    }

                    return 0;
                }
            }
            """,
            """
            using System;

            class C
            {
                int M(DayOfWeek day)
                {
                    switch (day)
                    {
                        case DayOfWeek.Monday:
                            return 1;
                        default:
                            throw new System.ArgumentOutOfRangeException();
                    }

                    return 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsThrowingDiscardArmToSwitchExpression()
    {
        return VerifyFixAsync(
            """
            using System;

            class C
            {
                int M(DayOfWeek day)
                {
                    return day {|E128088:switch|}
                    {
                        DayOfWeek.Monday => 1
                    };
                }
            }
            """,
            """
            using System;

            class C
            {
                int M(DayOfWeek day)
                {
                    return day switch
                    {
                        DayOfWeek.Monday => 1,
                        _ => throw new System.ArgumentOutOfRangeException()
                    };
                }
            }
            """);
    }
}
