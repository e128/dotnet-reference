using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AbstractBaseWithMembersCodeFixTests
{
    private static Task RunAsync(string code, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<AbstractBaseWithMembersAnalyzer, MemberlessAbstractBaseCodeFixProvider>(
            code,
            fixedCode,
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
    public Task ReplacesAbstractWithClosed_OnABaseThatDeclaresMembers()
    {
        return RunAsync(
            """
            internal abstract class {|E128225:Shape|}
            {
                internal abstract int Area();
            }

            internal sealed class Circle : Shape
            {
                internal override int Area() => 1;
            }
            """,
            """
            internal closed class Shape
            {
                internal abstract int Area();
            }

            internal sealed class Circle : Shape
            {
                internal override int Area() => 1;
            }
            """);
    }
}
