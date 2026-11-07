using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace E128.Analyzers.Tests;

/// <summary>
///     Shared runner for the analyzer test suite. One place owns the default reference
///     assemblies and the optional analyzer config file.
/// </summary>
internal static class AnalyzerTestRunner
{
    internal static Task VerifyAsync<TAnalyzer>(
        string code,
        DiagnosticResult[]? expected = null,
        ReferenceAssemblies? references = null,
        MarkupOptions markupOptions = MarkupOptions.None,
        (string Name, string Content)? analyzerConfig = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = references ?? ReferenceAssemblies.Net.Net100,
            MarkupOptions = markupOptions
        };

        if (analyzerConfig is { } config)
        {
            test.TestState.AnalyzerConfigFiles.Add((config.Name, config.Content));
        }

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        return test.RunAsync();
    }
}
