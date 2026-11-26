using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     Shared runner for the code fix test suite. One place owns the default
///     reference assemblies and the fix-all iteration settings.
/// </summary>
internal static class CodeFixTestRunner
{
    internal static Task RunAsync<TAnalyzer, TFixer>(
        string source,
        string fixedCode,
        ReferenceAssemblies? references = null,
        DiagnosticResult[]? expected = null,
        MarkupOptions markupOptions = MarkupOptions.None,
        CodeFixTestBehaviors behaviors = CodeFixTestBehaviors.None,
        CodeFixTestOptions? options = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        options ??= new CodeFixTestOptions();
        var test = new CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = references ?? ReferenceAssemblies.Net.Net100,
            MarkupOptions = markupOptions,
        };

        if (behaviors is not CodeFixTestBehaviors.None)
        {
            test.CodeFixTestBehaviors = behaviors;
        }

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        ApplyOptions(test, options);

        return test.RunAsync(Xunit.TestContext.Current.CancellationToken);
    }

    private static void ApplyOptions<TAnalyzer, TFixer>(
        CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier> test,
        CodeFixTestOptions options)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        if (options.FixAllIterations is { } iterations)
        {
            test.NumberOfFixAllIterations = iterations;
        }

        if (options.FixAllInDocumentIterations is { } inDocumentIterations)
        {
            test.NumberOfFixAllInDocumentIterations = inDocumentIterations;
        }

        if (options.CompilerDiagnostics is { } compilerDiagnostics)
        {
            test.CompilerDiagnostics = compilerDiagnostics;
        }

        if (options.TestBehaviors is not TestBehaviors.None)
        {
            test.TestBehaviors = options.TestBehaviors;
        }

        if (options.CodeActionEquivalenceKey is { } equivalenceKey)
        {
            test.CodeActionEquivalenceKey = equivalenceKey;
        }

        if (options.CodeActionIndex is { } actionIndex)
        {
            test.CodeActionIndex = actionIndex;
        }

        if (options.AllowUnsafe is true)
        {
            test.SolutionTransforms.Add(AllowUnsafe);
        }

        if (options.FixedExpected is not null)
        {
            test.FixedState.ExpectedDiagnostics.AddRange(options.FixedExpected);
        }

        AddSources(test, options.Sources);
        AddCompilationSettings(test, options);
    }

    private static void AddSources<TAnalyzer, TFixer>(
        CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier> test,
        IEnumerable<string>? sources)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        if (sources is null)
        {
            return;
        }

        foreach (var extraSource in sources)
        {
            test.TestState.Sources.Add(extraSource);
            test.FixedState.Sources.Add(extraSource);
        }
    }

    private static void AddCompilationSettings<TAnalyzer, TFixer>(
        CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier> test,
        CodeFixTestOptions options)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
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

    /// <summary>
    ///     Runs a rewrite that keeps the diagnostic. Markup in the fixed code supplies the
    ///     diagnostics that survive the rewrite.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
    /// <typeparam name="TFixer">The fixer under test.</typeparam>
    internal static Task RunKeepDiagnosticAsync<TAnalyzer, TFixer>(string source, string fixedCode)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        test.FixedState.MarkupHandling = MarkupMode.Allow;
        return test.RunAsync(Xunit.TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Asserts that the fixer leaves the code unchanged. Any diagnostic in
    ///     <paramref name="expected"/> decides whether the analyzer itself still fires.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
    /// <typeparam name="TFixer">The fixer that must keep the code unchanged.</typeparam>
    internal static Task NoFixAsync<TAnalyzer, TFixer>(
        string source,
        ReferenceAssemblies? references = null,
        CodeFixTestBehaviors behaviors = CodeFixTestBehaviors.None,
        params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = references ?? ReferenceAssemblies.Net.Net100,
        };

        if (behaviors is not CodeFixTestBehaviors.None)
        {
            test.CodeFixTestBehaviors = behaviors;
        }

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(Xunit.TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Solution transform that turns on unsafe compilation for the test project.
    /// </summary>
    internal static Solution AllowUnsafe(Solution solution, ProjectId projectId)
    {
        return solution.GetProject(projectId)?.CompilationOptions is CSharpCompilationOptions options
            ? solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true))
            : solution;
    }
}
