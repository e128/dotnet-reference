using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientCertificateBypassAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<HttpClientCertificateBypassAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task InitializerLambdaReturnsTrue_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       {|E128156:ServerCertificateCustomValidationCallback = (_, _, _, _) => true|}
                                   };

                                   return new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BlockBodyReturnsTrue_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       {|E128156:ServerCertificateCustomValidationCallback = (_, _, _, _) => { return true; }|}
                                   };

                                   return new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DangerousValidator_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       {|E128156:ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator|}
                                   };

                                   return new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CallbackChecksPolicyErrors_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       ServerCertificateCustomValidationCallback = (_, _, _, errors) => errors == System.Net.Security.SslPolicyErrors.None
                                   };

                                   return new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ServicePointManagerCallback_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   System.Net.ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AnonymousMethodReturnsTrue_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       {|E128156:ServerCertificateCustomValidationCallback = delegate { return true; }|}
                                   };

                                   return new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ParenthesizedTrue_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       {|E128156:ServerCertificateCustomValidationCallback = (_, _, _, _) => (true)|}
                                   };

                                   return new HttpClient(handler);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MethodGroupReturningTrue_Fires()
    {
        return VerifyAsync("""
                           using System.Net.Http;
                           class C
                           {
                               HttpClient M()
                               {
                                   var handler = new HttpClientHandler
                                   {
                                       {|E128156:ServerCertificateCustomValidationCallback = Accept|}
                                   };

                                   return new HttpClient(handler);
                               }

                               private static bool Accept(
                                   HttpRequestMessage request,
                                   System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
                                   System.Security.Cryptography.X509Certificates.X509Chain chain,
                                   System.Net.Security.SslPolicyErrors errors) => true;
                           }
                           """);
    }

}
