using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemberlessAbstractBaseCodeFixTests
{
    private static Task RunAsync(string code, string fixedCode, DiagnosticResult[]? expected = null)
    {
        return CodeFixTestRunner.RunAsync<MemberlessAbstractBaseAnalyzer, MemberlessAbstractBaseCodeFixProvider>(
            code,
            fixedCode,
            expected: expected,
            behaviors: CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            options: new CodeFixTestOptions
            {
                FixAllIterations = 1,
                Sources = [UnionTestRuntime.StubSource],
                LanguageVersion = LanguageVersion.Preview,
                SuppressedDiagnosticIds = ["RSEXPERIMENTAL006"],
            });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReplacesAbstractWithClosed()
    {
        return RunAsync(
            """
            internal abstract class {|E128219:Shape|}
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """,
            """
            internal closed class Shape
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task KeepsTheOriginalModifierPlacement()
    {
        return RunAsync(
            """
            internal
            abstract
            class {|E128219:Shape|}
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """,
            """
            internal
            closed
            class Shape
            {
            }

            internal sealed class Circle : Shape
            {
            }
            """);
    }
}
