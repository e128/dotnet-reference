using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SuppressionAuditE128AnalyzerTests
{
    private static DiagnosticResult Diagnostic(int line, int column)
    {
        return new DiagnosticResult("E128047", DiagnosticSeverity.Warning)
            .WithLocation(line, column);
    }

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SuppressionAuditAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // The analyzer flags #pragma warning disable directives, so the framework's
            // automatic suppression-via-pragma check creates a circular diagnostic.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaDisableWithoutComment_Fires()
    {
        const string code = """
                            #pragma warning disable CS1591
                            class C { }
                            #pragma warning restore CS1591
                            """;

        return VerifyAsync(code, Diagnostic(1, 1));
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaDisableWithTrailingComment_NoDiagnostic()
    {
        const string code = """
                            #pragma warning disable CS1591 // Missing XML comment
                            class C { }
                            #pragma warning restore CS1591
                            """;

        return VerifyAsync(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaDisableWithPrecedingComment_NoDiagnostic()
    {
        const string code = """
                            // Missing XML comment — generated code
                            #pragma warning disable CS1591
                            class C { }
                            #pragma warning restore CS1591
                            """;

        return VerifyAsync(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaRestore_NoDiagnostic()
    {
        const string code = """
                            #pragma warning disable CS1591 // justified
                            class C { }
                            #pragma warning restore CS1591
                            """;

        return VerifyAsync(code);
    }
}
