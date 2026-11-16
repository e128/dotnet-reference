using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SuppressionAuditAnalyzerBranchCoverageMoreTests
{
    private static AnalyzerTestOptions Options => new() { TestBehaviors = TestBehaviors.SkipSuppressionCheck };

    private static DiagnosticResult Diagnostic(int line, int column)
    {
        return new DiagnosticResult("E128047", DiagnosticSeverity.Warning)
            .WithLocation(line, column);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenABlankLineSeparatesTheCommentFromThePragma()
    {
        const string code = """
                            // The reason sits above a blank line.

                            #pragma warning disable CS1591
                            class C { }
                            #pragma warning restore CS1591
                            """;

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, Diagnostic(3, 1), options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnotherDirectiveIsTheOnlyPrecedingTrivia()
    {
        const string code = """
                            #nullable enable
                            #pragma warning disable CS1591
                            class C { }
                            #pragma warning restore CS1591
                            """;

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, Diagnostic(2, 1), options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenALeadingBlankLineIsTheOnlyPrecedingTrivia()
    {
        const string code = "\n#pragma warning disable CS1591\nclass C { }\n#pragma warning restore CS1591\n";

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, Diagnostic(2, 1), options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnIndentedPragmaHasNoComment()
    {
        const string code = """
                            class C
                            {
                                #pragma warning disable CS1591
                                public int Value;
                                #pragma warning restore CS1591
                            }
                            """;

        return AnalyzerTestRunner.VerifyAsync<SuppressionAuditAnalyzer>(code, Diagnostic(3, 5), options: Options);
    }
}
