using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SuppressionAuditE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<SuppressionAuditAnalyzer, SuppressionAuditCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // The analyzer flags #pragma warning disable directives, so the framework's
            // automatic suppression-via-pragma check creates a circular diagnostic.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
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
