using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientMissingOceCatchAnalyzerBranchCoverageMoreTests
{
    private const string Header = "using System;\nusing System.Net.Http;\nusing System.Threading.Tasks;\n";

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAsyncMethodHasAnExpressionBody()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>(
            Header + """
            class C
            {
                private readonly HttpClient _client = new();

                async Task<HttpResponseMessage> M() => await _client.GetAsync("u");
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRequestMethodCallDoesNotResolve()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>(
            Header + """
            class C
            {
                async Task M(Missing client)
                {
                    try
                    {
                        await client.GetAsync("u");
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAFilteredCatchWithoutTypePrecedesTheBroadCatch()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>(
            Header + """
            class C
            {
                private readonly HttpClient _client = new();

                async Task M(bool flag)
                {
                    try
                    {
                        await _client.GetAsync("u");
                    }
                    catch when (flag)
                    {
                    }
                    {|E128051:catch (Exception)|}
                    {
                    }
                }
            }
            """);
    }
}
