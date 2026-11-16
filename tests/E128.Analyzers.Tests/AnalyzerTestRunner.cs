using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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

        Configure(test, options);

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

    private static void Configure<TAnalyzer>(
        CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> test,
        AnalyzerTestOptions options)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
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
            test.SolutionTransforms.Add(AllowUnsafe);
        }

        if (options.AnalyzerConfig is { } config)
        {
            test.TestState.AnalyzerConfigFiles.Add((config.Name, config.Content));
        }

        AddAdditionalFiles(test.TestState, options.AdditionalFiles);
        AddAdditionalReferences(test.TestState, options.AdditionalReferences);
        AddCompilationSettings(test, options);
        AddSources(test.TestState, options.Sources);
    }

    private static Solution AllowUnsafe(Solution solution, ProjectId projectId)
    {
        return solution.GetProject(projectId)?.CompilationOptions is CSharpCompilationOptions compilationOptions
            ? solution.WithProjectCompilationOptions(
                projectId,
                compilationOptions.WithAllowUnsafe(true))
            : solution;
    }

    private static void AddAdditionalFiles(
        SolutionState state,
        IEnumerable<(string Name, string Content)>? additionalFiles)
    {
        if (additionalFiles is null)
        {
            return;
        }

        foreach (var (name, content) in additionalFiles)
        {
            state.AdditionalFiles.Add((name, SourceText.From(content)));
        }
    }

    private static void AddAdditionalReferences(
        SolutionState state,
        IEnumerable<MetadataReference>? additionalReferences)
    {
        if (additionalReferences is null)
        {
            return;
        }

        foreach (var reference in additionalReferences)
        {
            state.AdditionalReferences.Add(reference);
        }
    }

    private static void AddCompilationSettings<TAnalyzer>(
        CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> test,
        AnalyzerTestOptions options)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        if (options.LanguageVersion is null && options.SuppressedDiagnosticIds is null)
        {
            return;
        }

        test.SolutionTransforms.Add((solution, projectId) =>
            CompilationSettings.Apply(
                solution,
                projectId,
                options.LanguageVersion,
                options.SuppressedDiagnosticIds));
    }

    private static void AddSources(SolutionState state, IEnumerable<string>? sources)
    {
        if (sources is null)
        {
            return;
        }

        foreach (var source in sources)
        {
            state.Sources.Add(source);
        }
    }
}
