using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HandlerNoDecompressionBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenALocalAssignedLaterGetsDecompression()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net;
            using System.Net.Http;
            class C
            {
                void M()
                {
                    HttpClientHandler handler;
                    handler = new HttpClientHandler();
                    handler.AutomaticDecompression = DecompressionMethods.GZip;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenLaterStatementsSetNothingOnTheHandler()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net.Http;
            class C
            {
                void M(HttpClientHandler other, int[] numbers)
                {
                    var handler = {|E128214:new HttpClientHandler()|};
                    handler = other;
                    numbers[0] = 1;
                    handler.UseProxy = false;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenDecompressionIsSetOnAnotherHandler()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net;
            using System.Net.Http;
            class C
            {
                void M(HttpClientHandler other)
                {
                    var handler = {|E128214:new HttpClientHandler()|};
                    other.AutomaticDecompression = DecompressionMethods.GZip;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAFieldAssignmentReceivesTheHandler()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net.Http;
            class C
            {
                private HttpClientHandler _handler;

                void M()
                {
                    _handler = new HttpClientHandler();
                }
            }
            """);
    }
}
