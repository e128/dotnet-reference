using System.Threading.Tasks;
using E128.Analyzers.Maintainability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SwitchEnumExhaustivenessAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SwitchEnumExhaustivenessAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenEnumMemberUncased()
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
        return VerifyAsync("""
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
    public Task Analyzer_ReportsNothing_WhenDefaultArmPresent()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
