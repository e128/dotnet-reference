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
        int? fixAllIterations = null,
        CompilerDiagnostics? compilerDiagnostics = null,
        DiagnosticResult[]? expected = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = references ?? ReferenceAssemblies.Net.Net100,
        };

        if (fixAllIterations is not null)
        {
            test.NumberOfFixAllIterations = fixAllIterations.Value;
        }

        if (compilerDiagnostics is not null)
        {
            test.CompilerDiagnostics = compilerDiagnostics.Value;
        }

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        return test.RunAsync();
    }

    /// <summary>
    ///     The fixer must not change the code. Both fix-all counts are zeroed so
    ///     neither fix-all path runs, and any diagnostic in <paramref name="expected"/>
    ///     decides whether the analyzer itself still fires.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer under test.</typeparam>
    /// <typeparam name="TFixer">The fixer that must keep the code unchanged.</typeparam>
    internal static Task NoFixAsync<TAnalyzer, TFixer>(
        string source,
        ReferenceAssemblies? references = null,
        params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TFixer : CodeFixProvider, new()
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TFixer, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = references ?? ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 0,
            NumberOfFixAllInDocumentIterations = 0,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>Solution transform that turns on unsafe compilation for the test project.</summary>
    internal static Solution AllowUnsafe(Solution solution, ProjectId projectId)
    {
        return solution.GetProject(projectId)?.CompilationOptions is CSharpCompilationOptions options
            ? solution.WithProjectCompilationOptions(projectId, options.WithAllowUnsafe(true))
            : solution;
    }
}
