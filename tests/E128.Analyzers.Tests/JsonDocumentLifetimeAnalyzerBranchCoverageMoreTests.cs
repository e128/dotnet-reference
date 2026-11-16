using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class JsonDocumentLifetimeAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string source)
    {
        return AnalyzerTestRunner.VerifyAsync<JsonDocumentLifetimeAnalyzer>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenParseIsCalledOnADynamicReceiver()
    {
        return VerifyAsync("""
            class C
            {
                void M(dynamic parser)
                {
                    var result = parser.Parse("{}");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnExpressionBodiedMethodLeaksTheRootElementFromALambda()
    {
        return VerifyAsync("""
            using System;
            using System.Text.Json;
            class C
            {
                static JsonElement Keep;

                static int Run(Func<int> action) => action();

                int M() => Run(() =>
                {
                    using var doc = {|E128041:JsonDocument.Parse("{}")|};
                    Keep = doc.RootElement;
                    return 0;
                });
            }
            """);
    }
}
