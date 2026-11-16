using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientMissingOceCatchBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientMissingOceCatchAnalyzer>(
            "using System;\nusing System.Net.Http;\nusing System.Threading;\nusing System.Threading.Tasks;\n"
            + "sealed class Client : HttpClient { }\n"
            + "class C\n{\n    private readonly HttpClient _client = new();\n" + members + "}\n");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("PostAsync(\"u\", null)")]
    [InlineData("PutAsync(\"u\", null)")]
    [InlineData("DeleteAsync(\"u\")")]
    [InlineData("SendAsync(new HttpRequestMessage())")]
    public Task Reports_ForEachRequestMethodWithABroadCatch(string call)
    {
        return VerifyAsync($$"""
                async Task M()
                {
                    try
                    {
                        await _client.{{call}};
                    }
                    {|E128051:catch (Exception)|}
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForACallOnAnHttpClientSubclass()
    {
        return VerifyAsync("""
                async Task M(Client client)
                {
                    try
                    {
                        await client.GetAsync("u");
                    }
                    {|E128051:catch (Exception)|}
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMethodsThatAreNotRequestMethods()
    {
        return VerifyAsync("""
                async Task M()
                {
                    try
                    {
                        await _client.GetStringAsync("u");
                    }
                    catch (Exception)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForALocalFunctionNamedLikeARequestMethod()
    {
        return VerifyAsync("""
                async Task M()
                {
                    try
                    {
                        await GetAsync();
                    }
                    catch (Exception)
                    {
                    }

                    static Task GetAsync() => Task.CompletedTask;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheRequestIsOutsideTheTryBlock()
    {
        return VerifyAsync("""
                async Task M()
                {
                    await _client.GetAsync("u");
                    try
                    {
                        await Task.Delay(1);
                    }
                    catch (Exception)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenANarrowerCatchHandlesTheRequest()
    {
        return VerifyAsync("""
                async Task M()
                {
                    try
                    {
                        await _client.GetAsync("u");
                    }
                    catch (HttpRequestException)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenADerivedCancellationCatchPrecedesTheBroadCatch()
    {
        return VerifyAsync("""
                async Task M()
                {
                    try
                    {
                        await _client.GetAsync("u");
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception)
                    {
                    }
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAnUnrelatedCatchPrecedesTheBroadCatch()
    {
        return VerifyAsync("""
                async Task M()
                {
                    try
                    {
                        await _client.GetAsync("u");
                    }
                    catch (HttpRequestException)
                    {
                    }
                    {|E128051:catch (Exception)|}
                    {
                    }
                }

            """);
    }
}
