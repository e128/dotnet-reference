using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SuppressionAuditE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode, params DiagnosticResult[] expected)
    {
        return CodeFixTestRunner.RunAsync<SuppressionAuditAnalyzer, SuppressionAuditCodeFixProvider>(
            source,
            fixedCode,
            expected: expected,
            // The analyzer flags #pragma warning disable directives, so the framework's
            // automatic suppression-via-pragma check creates a circular diagnostic.
            options: new CodeFixTestOptions { TestBehaviors = TestBehaviors.SkipSuppressionCheck });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaWithoutComment_AddsJustificationComment()
    {
        const string source = "#pragma warning disable CS1591\nclass C { }\n#pragma warning restore CS1591\n";

        // The code fix appends a comment after the EndOfDirective token.
        // Trailing space comes from the comment text; extra newline from the new LineFeed trivia.
        const string fixedCode = "#pragma warning disable CS1591 // Justification: \n\nclass C { }\n#pragma warning restore CS1591\n";

        return VerifyFixAsync(
            source,
            fixedCode,
            new DiagnosticResult("E128047", DiagnosticSeverity.Warning)
                .WithLocation(1, 1));
    }
}
