using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConfigureAwaitFalseE128AnalyzerTests
{
    private static Task VerifyAsExeAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConfigureAwaitFalseE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var options = (CSharpCompilationOptions)project.CompilationOptions!;
            return solution.WithProjectCompilationOptions(
                projectId,
                options.WithOutputKind(OutputKind.ConsoleApplication));
        });
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyAsBlazorWasmAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConfigureAwaitFalseE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.TestState.Sources.Add("""
                                   namespace Microsoft.AspNetCore.Components.WebAssembly.Hosting
                                   {
                                       public sealed class WebAssemblyHostBuilder { }
                                   }
                                   """);
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var options = (CSharpCompilationOptions)project.CompilationOptions!;
            return solution.WithProjectCompilationOptions(
                projectId,
                options.WithOutputKind(OutputKind.ConsoleApplication));
        });
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private static Task VerifyAsDllAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ConfigureAwaitFalseE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var options = (CSharpCompilationOptions)project.CompilationOptions!;
            return solution.WithProjectCompilationOptions(
                projectId,
                options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary));
        });
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureAwaitFalse_InAppCode_FiresE128022()
    {
        return VerifyAsExeAsync("""
                                using System.Threading.Tasks;
                                await {|E128022:Task.Delay(1).ConfigureAwait(false)|};
                                """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureAwaitFalse_InClassLibrary_NoFire()
    {
        return VerifyAsDllAsync("""
                                using System.Threading.Tasks;
                                class C
                                {
                                    async Task M()
                                    {
                                        await Task.Delay(1).ConfigureAwait(false);
                                    }
                                }
                                """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureAwaitFalse_InBlazorWasm_NoFire()
    {
        return VerifyAsBlazorWasmAsync("""
                                       using System.Threading.Tasks;
                                       await Task.Delay(1).ConfigureAwait(false);
                                       """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureAwaitTrue_NoFire()
    {
        return VerifyAsExeAsync("""
                                using System.Threading.Tasks;
                                await Task.Delay(1).ConfigureAwait(true);
                                """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureAwaitVariable_NoFire()
    {
        return VerifyAsExeAsync("""
                                using System.Threading.Tasks;
                                bool continueOnCapturedContext = true;
                                await Task.Delay(1).ConfigureAwait(continueOnCapturedContext);
                                """);
    }
}
