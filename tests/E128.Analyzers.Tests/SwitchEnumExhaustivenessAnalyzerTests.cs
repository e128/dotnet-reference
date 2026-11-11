using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwitchEnumExhaustivenessAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenEnumMemberUncased()
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>("""
                            class C
                            {
                                void M(Color c)
                                {
                                    {|E128088:switch|} (c)
                                    {
                                        case Color.Red:
                                            break;
                                        case Color.Green:
                                            break;
                                    }
                                }
                            }

                            enum Color
                            {
                                Red,
                                Green,
                                Blue
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenAllMembersCased()
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>("""
                            class C
                            {
                                string M(Color c)
                                {
                                    return c switch
                                    {
                                        Color.Red => "r",
                                        Color.Green => "g",
                                        Color.Blue => "b"
                                    };
                                }
                            }

                            enum Color
                            {
                                Red,
                                Green,
                                Blue
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenCombinedLabelsCaseEveryMember()
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>("""
                            class C
                            {
                                void M(Color c)
                                {
                                    switch (c)
                                    {
                                        case Color.Red:
                                            break;
                                        case Color.Green or Color.Blue:
                                            break;
                                    }
                                }
                            }

                            enum Color
                            {
                                Red,
                                Green,
                                Blue
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenCombinedLabelLeavesOneMemberUncased()
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>("""
                            class C
                            {
                                void M(Color c)
                                {
                                    {|E128088:switch|} (c)
                                    {
                                        case Color.Red or Color.Green:
                                            break;
                                    }
                                }
                            }

                            enum Color
                            {
                                Red,
                                Green,
                                Blue
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenDefaultArmPresent()
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>("""
                            class C
                            {
                                void M(Color c)
                                {
                                    switch (c)
                                    {
                                        case Color.Red:
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }

                            enum Color
                            {
                                Red,
                                Green,
                                Blue
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenGoverningTypeIsString()
    {
        return AnalyzerTestRunner.VerifyAsync<SwitchEnumExhaustivenessAnalyzer>("""
                            class C
                            {
                                void M(string s)
                                {
                                    switch (s)
                                    {
                                        case "a":
                                            break;
                                    }
                                }
                            }
                            """);
    }
}
