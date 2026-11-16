using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumIfElseChainCodeFixProviderBranchCoverageTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<EnumIfElseChainAnalyzer, EnumIfElseChainCodeFixProvider>(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_MapsTheFinalElseToDefaultAndAddsBreaks()
    {
        return VerifyFixAsync(
            """
            enum Color { Red, Green, Blue }

            class C
            {
                void Log(int value) { }

                void M(Color c)
                {
                    {|E128048:if (c == Color.Red)
                        Log(1);
                    else if (c == Color.Green)
                    {
                    }
                    else if (c == Color.Blue)
                    {
                        throw new System.InvalidOperationException();
                    }
                    else
                        Log(4);|}
                }
            }
            """,
            """
            enum Color { Red, Green, Blue }

            class C
            {
                void Log(int value) { }

                void M(Color c)
                {
                    switch (c)
                    {
                        case Color.Red:
                            Log(1);
                            break;
                        case Color.Green:
                            break;
                        case Color.Blue:
                            throw new System.InvalidOperationException();
                        default:
                            Log(4);
                            break;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_IsNotOfferedForAChainOfInequalities()
    {
        const string unchanged = """
            enum Color { Red, Green, Blue }

            class C
            {
                int M(Color c)
                {
                    if (c != Color.Red)
                    {
                        return 1;
                    }
                    else if (c != Color.Green)
                    {
                        return 2;
                    }
                    else if (c != Color.Blue)
                    {
                        return 3;
                    }
                    return 0;
                }
            }
            """;

        return CodeFixTestRunner.RunAsync<EnumIfElseChainAnalyzer, EnumIfElseChainCodeFixProvider>(
            unchanged,
            unchanged,
            expected: [Remaining()],
            options: new CodeFixTestOptions { FixedExpected = [Remaining()] });
    }

    private static DiagnosticResult Remaining()
    {
        return new DiagnosticResult("E128048", DiagnosticSeverity.Warning).WithSpan(7, 9, 18, 10).WithArguments("Color", "3");
    }
}
