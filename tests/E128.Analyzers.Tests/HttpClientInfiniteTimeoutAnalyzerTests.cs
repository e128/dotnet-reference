using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientInfiniteTimeoutAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<HttpClientInfiniteTimeoutAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ObjectInitializer_WithInfiniteTimeSpan_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           using System.Threading;
                           class C
                           {
                               static HttpClient M()
                               {
                                   return new HttpClient { {|E128181:Timeout = Timeout.InfiniteTimeSpan|} };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ObjectInitializer_WithFromMillisecondsNegativeOne_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           class C
                           {
                               static HttpClient M()
                               {
                                   return new HttpClient { {|E128181:Timeout = TimeSpan.FromMilliseconds(-1)|} };
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TargetTypedObjectInitializer_WithTimeSpanMaxValue_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           class C
                           {
                               static HttpClient M()
                               {
                                   HttpClient client = new() { {|E128181:Timeout = TimeSpan.MaxValue|} };
                                   return client;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyAssignment_WithQualifiedInfiniteTimeSpan_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               static void M(HttpClient client)
                               {
                                   {|E128181:client.Timeout = System.Threading.Timeout.InfiniteTimeSpan|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConfigureHttpClientLambda_WithFromMillisecondsTimeoutInfinite_Fires()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           using System.Threading;
                           class C
                           {
                               static HttpClient Create()
                               {
                                   var client = new HttpClient();
                                   Configure(client, c => {|E128181:c.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite)|});
                                   return client;
                               }

                               static void Configure(HttpClient client, Action<HttpClient> configure)
                               {
                                   configure(client);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyAssignment_WithFiniteTimeout_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           class C
                           {
                               static void M(HttpClient client)
                               {
                                   client.Timeout = TimeSpan.FromSeconds(30);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyAssignment_WithTimeoutBelowOneHundredMilliseconds_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           class C
                           {
                               static void M(HttpClient client)
                               {
                                   client.Timeout = TimeSpan.FromMilliseconds(50);
                                   client.Timeout = TimeSpan.FromMilliseconds(-2);
                                   client.Timeout = TimeSpan.Zero;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PropertyAssignment_WithComputedTimeout_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           class C
                           {
                               static void M(HttpClient client, TimeSpan timeout)
                               {
                                   client.Timeout = timeout;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FromMilliseconds_WithComputedArgument_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Net.Http;
                           class C
                           {
                               static void M(HttpClient client, double milliseconds)
                               {
                                   client.Timeout = TimeSpan.FromMilliseconds(milliseconds);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OtherTypeTimeoutProperty_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           class Settings
                           {
                               public TimeSpan Timeout { get; set; }
                           }
                           class C
                           {
                               static void M(Settings settings)
                               {
                                   settings.Timeout = TimeSpan.MaxValue;
                               }
                           }
                           """);
    }
}
