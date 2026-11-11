using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class PragmaFileScopedNamespaceAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { TestBehaviors = TestBehaviors.SkipSuppressionCheck };

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_Reports_WhenPragmaPrecedesFileScopedNamespace()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaFileScopedNamespaceAnalyzer>("""
                            {|E128094:#pragma warning disable CA1000|}

                            namespace N;

                            class C
                            {
                            }
                            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenPragmaFollowsNamespace()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaFileScopedNamespaceAnalyzer>("""
                            namespace N;

                            #pragma warning disable CA1000

                            class C
                            {
                            }
                            """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Analyzer_ReportsNothing_WhenNamespaceIsBlockScoped()
    {
        return AnalyzerTestRunner.VerifyAsync<PragmaFileScopedNamespaceAnalyzer>("""
                            #pragma warning disable CA1000

                            namespace N
                            {
                                class C
                                {
                                }
                            }
                            """, options: Options);
    }
}
