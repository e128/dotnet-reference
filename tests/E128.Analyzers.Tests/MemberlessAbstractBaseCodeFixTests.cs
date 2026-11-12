using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MemberlessAbstractBaseCodeFixTests
{
    private static Task RunAsync(string code, string fixedCode, DiagnosticResult[]? expected = null)
    {
        var test = new CSharpCodeFixTest<MemberlessAbstractBaseAnalyzer, MemberlessAbstractBaseCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
        };

        test.TestState.Sources.Add(UnionTestRuntime.StubSource);
        test.FixedState.Sources.Add(UnionTestRuntime.StubSource);

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var project = solution.GetProject(projectId);
            if (project?.ParseOptions is not CSharpParseOptions parseOptions
                || project.CompilationOptions is not CSharpCompilationOptions compilationOptions)
            {
                return solution;
            }

            var suppressions = compilationOptions.SpecificDiagnosticOptions
                .Add("RSEXPERIMENTAL006", ReportDiagnostic.Suppress);
            return solution
                .WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview))
                .WithProjectCompilationOptions(
                    projectId,
                    compilationOptions.WithSpecificDiagnosticOptions(suppressions));
        });

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        return test.RunAsync(TestContext.Current.CancellationToken);
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
