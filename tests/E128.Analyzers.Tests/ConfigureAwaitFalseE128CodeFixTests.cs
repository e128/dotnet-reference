using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConfigureAwaitFalseE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        var test = new CSharpCodeFixTest<ConfigureAwaitFalseE128Analyzer, ConfigureAwaitFalseCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var options = (CSharpCompilationOptions)project.CompilationOptions!;
            return solution.WithProjectCompilationOptions(
                projectId,
                options.WithOutputKind(OutputKind.ConsoleApplication));
        });
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesConfigureAwaitFalse()
    {
        return VerifyFixAsync(
            """
            using System.Threading.Tasks;
            await {|E128022:Task.Delay(1).ConfigureAwait(false)|};
            """,
            """
            using System.Threading.Tasks;
            await Task.Delay(1);
            """);
    }
}
