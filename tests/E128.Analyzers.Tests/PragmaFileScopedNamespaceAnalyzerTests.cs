using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaFileScopedNamespaceAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<PragmaFileScopedNamespaceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // Pragma-targeting analyzer: skip the suppression check to avoid the harness
            // injecting #pragma warning disable E128094 and creating self-referential behavior.
            TestBehaviors = TestBehaviors.SkipSuppressionCheck
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenPragmaPrecedesFileScopedNamespace()
    {
        return VerifyAsync("""
                            {|E128094:#pragma warning disable CA1000|}

                            namespace N;

                            class C
                            {
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenPragmaFollowsNamespace()
    {
        return VerifyAsync("""
                            namespace N;

                            #pragma warning disable CA1000

                            class C
                            {
                            }
                            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenNamespaceIsBlockScoped()
    {
        return VerifyAsync("""
                            #pragma warning disable CA1000

                            namespace N
                            {
                                class C
                                {
                                }
                            }
                            """);
    }
}
