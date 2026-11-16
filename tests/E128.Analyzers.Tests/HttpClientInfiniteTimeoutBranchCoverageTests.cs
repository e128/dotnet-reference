using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientInfiniteTimeoutBranchCoverageTests
{
    private static Task VerifyAsync(string body)
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientInfiniteTimeoutAnalyzer>(
            "using System;\nusing System.Net.Http;\nusing System.Threading;\nclass Other { public TimeSpan Timeout { get; set; } }\n"
            + "class C\n{\n    TimeSpan _field;\n    void M(HttpClient client, Other other, int milliseconds)\n    {\n" + body + "\n    }\n}\n");
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("-1L")]
    [InlineData("-1.0")]
    [InlineData("-1")]
    public Task Reports_ForEveryConstantFormOfMinusOneMillisecond(string literal)
    {
        return VerifyAsync($$"""
                    {|E128181:client.Timeout = TimeSpan.FromMilliseconds({{literal}})|};
            """);
    }

    [Theory]
    [Trait("Category", "CI")]
    [InlineData("TimeSpan.FromMilliseconds(-1.5)")]
    [InlineData("TimeSpan.FromMilliseconds(-2L)")]
    [InlineData("TimeSpan.FromMilliseconds(30000)")]
    [InlineData("TimeSpan.FromMilliseconds(milliseconds)")]
    [InlineData("TimeSpan.FromSeconds(-1)")]
    [InlineData("TimeSpan.Zero")]
    [InlineData("_field")]
    [InlineData("Timeout.InfiniteTimeSpan - TimeSpan.Zero")]
    public Task Silent_ForFiniteOrComputedTimeouts(string value)
    {
        return VerifyAsync($$"""
                    client.Timeout = {{value}};
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForATimeoutPropertyOnAnotherTypeAndForNonProperties()
    {
        return VerifyAsync("""
                    other.Timeout = Timeout.InfiniteTimeSpan;
                    _field = Timeout.InfiniteTimeSpan;
                    var local = TimeSpan.Zero;
                    local = Timeout.InfiniteTimeSpan;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnotherHttpClientProperty()
    {
        return VerifyAsync("""
                    client.MaxResponseContentBufferSize = 10;
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAnInfiniteTimeSpanNamedFieldOnAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientInfiniteTimeoutAnalyzer>("""
            using System;
            using System.Net.Http;
            static class Limits { public static readonly TimeSpan InfiniteTimeSpan = TimeSpan.FromMilliseconds(-1); }
            class C
            {
                void M(HttpClient client)
                {
                    client.Timeout = Limits.InfiniteTimeSpan;
                }
            }
            """);
    }
}
