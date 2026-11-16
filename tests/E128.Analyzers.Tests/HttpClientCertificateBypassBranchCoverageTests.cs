using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientCertificateBypassBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientCertificateBypassAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_DelegatesThatAReferencedMethodLocalOrFieldHolds()
    {
        return VerifyAsync("""
            using System.Net.Http;
            using System.Net.Security;
            using System.Security.Cryptography.X509Certificates;

            class C
            {
                static readonly RemoteCertificateValidationCallback FieldCallback = (_, _, _, _) => true;

                static bool AcceptAll(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;

                static bool AcceptWithBlock(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
                {
                    return true;
                }

                static bool Inspect(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
                {
                    var ok = errors == SslPolicyErrors.None;
                    return ok;
                }

                void M(SslClientAuthenticationOptions options)
                {
                    RemoteCertificateValidationCallback local = (_, _, _, _) => true;
                    RemoteCertificateValidationCallback second = local;

                    {|E128156:options.RemoteCertificateValidationCallback = local|};
                    {|E128156:options.RemoteCertificateValidationCallback = FieldCallback|};
                    {|E128156:options.RemoteCertificateValidationCallback = AcceptAll|};
                    {|E128156:options.RemoteCertificateValidationCallback = AcceptWithBlock|};
                    options.RemoteCertificateValidationCallback = second;
                    options.RemoteCertificateValidationCallback = Inspect;

                    bool LocalFunction(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;
                    bool LocalBlock(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
                    {
                        return true;
                    }

                    {|E128156:options.RemoteCertificateValidationCallback = LocalFunction|};
                    {|E128156:options.RemoteCertificateValidationCallback = LocalBlock|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallbackIsMissingUnknownOrNotDecidable()
    {
        return VerifyAsync("""
            using System;
            using System.Net.Http;
            using System.Net.Security;
            using System.Security.Cryptography.X509Certificates;

            class C
            {
                RemoteCertificateValidationCallback? field;

                void Use(RemoteCertificateValidationCallback callback) { }

                void M(SslClientAuthenticationOptions options, RemoteCertificateValidationCallback parameter, bool flag)
                {
                    options.RemoteCertificateValidationCallback = null;
                    options.RemoteCertificateValidationCallback = parameter;
                    options.RemoteCertificateValidationCallback = field;
                    options.RemoteCertificateValidationCallback = (_, _, _, errors) =>
                    {
                        if (errors == SslPolicyErrors.None)
                        {
                            return true;
                        }

                        return false;
                    };
                    options.RemoteCertificateValidationCallback = (_, _, _, errors) => { var x = 1; return true; };
                    options.RemoteCertificateValidationCallback = (_, _, _, errors) => errors == SslPolicyErrors.None;
                    options.RemoteCertificateValidationCallback = (_, _, _, errors) => errors == SslPolicyErrors.None && true;
                    options.RemoteCertificateValidationCallback = flag ? parameter : parameter;
                    Use((_, _, _, _) => true);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAssignedPropertyBelongsToAnotherType()
    {
        return VerifyAsync("""
            using System;

            class Other
            {
                public Func<int, bool>? ServerCertificateCustomValidationCallback { get; set; }

                public Func<int, bool>? RemoteCertificateValidationCallback { get; set; }

                public Func<int, bool>? Something { get; set; }
            }

            class C
            {
                void M(Other other)
                {
                    other.ServerCertificateCustomValidationCallback = _ => true;
                    other.RemoteCertificateValidationCallback = _ => true;
                    other.Something = _ => true;
                    Func<int, bool>[] array = new Func<int, bool>[1];
                    array[0] = _ => true;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TheDangerousValidatorAndOrExpressionsThroughAHandlerProperty()
    {
        return VerifyAsync("""
            using System.Net.Http;
            using System.Net.Security;
            class C
            {
                void M(HttpClientHandler handler, bool flag)
                {
                    {|E128156:handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator|};
                    {|E128156:handler.ServerCertificateCustomValidationCallback = (_, _, _, errors) => flag || true|};
                    {|E128156:handler.ServerCertificateCustomValidationCallback = (_, _, _, errors) => (true || flag)|};
                    {|E128156:handler.ServerCertificateCustomValidationCallback = ((_, _, _, _) => true)|};
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, errors) => flag || errors == SslPolicyErrors.None;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_SslStreamCreatedWithAnAcceptAllCallbackAndSilentOtherwise()
    {
        return VerifyAsync("""
            using System.IO;
            using System.Net.Security;
            class C
            {
                static bool AcceptAll(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? c, System.Security.Cryptography.X509Certificates.X509Chain? ch, SslPolicyErrors e) => true;

                void M(Stream stream, RemoteCertificateValidationCallback parameter)
                {
                    var a = {|E128156:new SslStream(stream, false, (_, _, _, _) => true)|};
                    SslStream b = {|E128156:new(stream, false, AcceptAll)|};
                    var c = new SslStream(stream);
                    var d = new SslStream(stream, false);
                    var e = new SslStream(stream, false, parameter);
                    var f = new SslStream(stream, false, (_, _, _, errors) => errors == SslPolicyErrors.None);
                    var g = new MemoryStream();
                }
            }
            """);
    }
}
