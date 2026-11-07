using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HandlerNoDecompressionAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task HttpClientHandler_NoDecompression_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net.Http;
            class C
            {
                HttpClientHandler M()
                {
                    return {|E128214:new HttpClientHandler()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SocketsHttpHandler_NoDecompression_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net.Http;
            class C
            {
                SocketsHttpHandler M()
                {
                    return {|E128214:new SocketsHttpHandler()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ImplicitCreation_NoDecompression_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net.Http;
            class C
            {
                HttpClientHandler M()
                {
                    HttpClientHandler handler = {|E128214:new()|};
                    return handler;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ObjectInitializerWithDecompression_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net;
            using System.Net.Http;
            class C
            {
                HttpClientHandler M()
                {
                    return new HttpClientHandler
                    {
                        AutomaticDecompression = DecompressionMethods.All
                    };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AssignedAfterCreation_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net;
            using System.Net.Http;
            class C
            {
                void M()
                {
                    var handler = new HttpClientHandler();
                    handler.AutomaticDecompression = DecompressionMethods.All;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DerivedHandler_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net;
            using System.Net.Http;
            class MyHandler : HttpClientHandler
            {
                public MyHandler()
                {
                    AutomaticDecompression = DecompressionMethods.All;
                }
            }

            class C
            {
                MyHandler M()
                {
                    return new MyHandler();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldInitializerConfiguredInConstructor_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<HandlerNoDecompressionAnalyzer>("""
            using System.Net;
            using System.Net.Http;
            class C
            {
                private readonly HttpClientHandler _handler = new HttpClientHandler();

                public C()
                {
                    _handler.AutomaticDecompression = DecompressionMethods.All;
                }
            }
            """);
    }
}
