using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SuppressionAuditE128AnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { TestBehaviors = TestBehaviors.SkipSuppressionCheck };

    private static DiagnosticResult Diagnostic(int line, int column)
    {
        return new DiagnosticResult("E128047", DiagnosticSeverity.Warning)
            .WithLocation(line, column);
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

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, Diagnostic(1, 1), options: Options);
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

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, options: Options);
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

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, options: Options);
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

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, options: Options);
    }
}
