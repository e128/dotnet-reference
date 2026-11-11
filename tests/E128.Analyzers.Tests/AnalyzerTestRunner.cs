using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Tests;

/// <summary>
///     Shared runner for the analyzer test suite. One place owns the default reference
///     assemblies, the optional files, and the compilation switches.
/// </summary>
internal static class AnalyzerTestRunner
{
    internal static Task VerifyAsync<TAnalyzer>(
        string code,
        DiagnosticResult[]? expected = null,
        AnalyzerTestOptions? options = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        options ??= new AnalyzerTestOptions();
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = options.References ?? ReferenceAssemblies.Net.Net100,
            MarkupOptions = options.MarkupOptions
        };

        if (options.CompilerDiagnostics is { } diagnostics)
        {
            test.CompilerDiagnostics = diagnostics;
        }

        if (options.TestBehaviors is not TestBehaviors.None)
        {
            test.TestBehaviors = options.TestBehaviors;
        }

        if (options.AllowUnsafe)
        {
            test.SolutionTransforms.Add(static (solution, projectId) =>
                solution.GetProject(projectId)?.CompilationOptions is CSharpCompilationOptions compilationOptions
                    ? solution.WithProjectCompilationOptions(
                        projectId,
                        compilationOptions.WithAllowUnsafe(true))
                    : solution);
        }

        if (options.AnalyzerConfig is { } config)
        {
            test.TestState.AnalyzerConfigFiles.Add((config.Name, config.Content));
        }

        if (options.AdditionalFiles is not null)
        {
            foreach (var (name, content) in options.AdditionalFiles)
            {
                test.TestState.AdditionalFiles.Add((name, SourceText.From(content)));
            }
        }

        if (options.Sources is not null)
        {
            foreach (var source in options.Sources)
            {
                test.TestState.Sources.Add(source);
            }
        }

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        return test.RunAsync(Xunit.TestContext.Current.CancellationToken);
    }

    internal static Task VerifyAsync<TAnalyzer>(
        string[] sources,
        DiagnosticResult[]? expected = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        return VerifyAsync<TAnalyzer>(
            string.Empty,
            expected,
            new AnalyzerTestOptions { Sources = sources });
    }

    internal static Task VerifyAsync<TAnalyzer>(
        string code,
        DiagnosticResult expected,
        AnalyzerTestOptions? options = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        return VerifyAsync<TAnalyzer>(code, [expected], options);
    }

    internal static Task VerifyAsync<TAnalyzer>(
        string code,
        DiagnosticResult first,
        DiagnosticResult second,
        AnalyzerTestOptions? options = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        return VerifyAsync<TAnalyzer>(code, [first, second], options);
    }
}
