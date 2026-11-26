using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     Shared helpers for the ComplexityCeiling analyzer and code fix test files.
/// </summary>
internal static class ComplexityCeilingTestHarness
{
    /// <summary>
    ///     The E128152 warning at a markup position.
    /// </summary>
    internal static DiagnosticResult Warning(int line, int column)
    {
        return new DiagnosticResult("E128152", DiagnosticSeverity.Warning).WithLocation(line, column);
    }

    /// <summary>
    ///     Runs the analyzer with a <c lang="csharp">e128_max_complexity</c> ceiling option.
    /// </summary>
    internal static Task VerifyWithCeilingAsync(string ceiling, string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<ComplexityCeilingAnalyzer>(
            code,
            expected,
            new AnalyzerTestOptions { AnalyzerConfig = ("/.globalconfig", "is_global = true\ne128_max_complexity = " + ceiling) });
    }

    /// <summary>
    ///     Runs the membership scan fixer with the fix-all iteration the rewrite needs.
    /// </summary>
    internal static Task VerifyMembershipAsync(string source, string fixedCode, params DiagnosticResult[] expected)
    {
        return CodeFixTestRunner.RunAsync<ComplexityCeilingAnalyzer, ComplexityCeilingMembershipScanCodeFixProvider>(
            source,
            fixedCode,
            expected: expected,
            options: new CodeFixTestOptions { FixAllIterations = 1 });
    }

    /// <summary>
    ///     Asserts the membership scan fixer keeps the source unchanged.
    /// </summary>
    internal static Task VerifyNoMembershipAsync(string source, params DiagnosticResult[] expected)
    {
        return CodeFixTestRunner.NoFixAsync<ComplexityCeilingAnalyzer, ComplexityCeilingMembershipScanCodeFixProvider>(
            source,
            expected: expected);
    }

    /// <summary>
    ///     Runs the suppression fixer over one rewrite.
    /// </summary>
    internal static Task VerifySuppressionAsync(string source, string fixedCode, params DiagnosticResult[] expected)
    {
        return CodeFixTestRunner.RunAsync<ComplexityCeilingAnalyzer, ComplexityCeilingSuppressionCodeFixProvider>(
            source,
            fixedCode,
            expected: expected);
    }
}
