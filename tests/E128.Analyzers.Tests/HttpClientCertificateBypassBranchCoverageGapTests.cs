using System.IO;
using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class HttpClientCertificateBypassBranchCoverageGapTests
{
    // Only System.Runtime: no HttpClientHandler, no SslClientAuthenticationOptions and no SslStream.
    private static readonly ReferenceAssemblies MinimalReferences = new(
        "netstandard1.5",
        new PackageIdentity("System.Runtime", "4.3.1"),
        Path.Combine("ref", "netstandard1.5"));

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientCertificateBypassAnalyzer>(code);
    }

    private static Task VerifyAsync(ReferenceAssemblies references, string code)
    {
        return AnalyzerTestRunner.VerifyAsync<HttpClientCertificateBypassAnalyzer>(
            code,
            options: new AnalyzerTestOptions { References = references });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNoCertificateValidationTypesExist()
    {
        return VerifyAsync(MinimalReferences, "class C { }");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyTheHandlerAndSslStreamTypesExist()
    {
        return VerifyAsync(ReferenceAssemblies.NetStandard.NetStandard20, """
            using System.Net.Http;
            using System.Net.Security;
            using System.IO;

            class C
            {
                void M(HttpClientHandler handler, Stream stream)
                {
                    {|E128156:handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true|};
                    var ssl = {|E128156:new SslStream(stream, false, (_, _, _, _) => true)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSslStreamCreationsThatDoNotBind()
    {
        return VerifyAsync("""
            using System.Net.Security;

            class C
            {
                void M()
                {
                    var a = new {|CS1729:SslStream|}();
                    var b = new {|CS0246:Missing|}();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheAssignedMemberIsAFieldOrUnresolved()
    {
        return VerifyAsync("""
            using System;

            class Holder
            {
                public Func<int, bool>? ServerCertificateCustomValidationCallback;
            }

            class C
            {
                void M(Holder holder)
                {
                    holder.ServerCertificateCustomValidationCallback = _ => true;
                    holder.{|CS1061:RemoteCertificateValidationCallback|} = _ => true;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheCallbackIsAMemberAccessToAFieldThatHoldsAnAcceptAllLambda()
    {
        return VerifyAsync("""
            using System.Net.Security;

            static class Callbacks
            {
                public static readonly RemoteCertificateValidationCallback AcceptAll = (_, _, _, _) => true;

                public static readonly RemoteCertificateValidationCallback Inspect = (_, _, _, errors) => errors == SslPolicyErrors.None;
            }

            class C
            {
                void M(SslClientAuthenticationOptions options)
                {
                    options.RemoteCertificateValidationCallback = Callbacks.Inspect;
                    {|E128156:options.RemoteCertificateValidationCallback = Callbacks.AcceptAll|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheNamedMethodHasNoBody()
    {
        return VerifyAsync("""
            using System.Net.Security;
            using System.Security.Cryptography.X509Certificates;

            abstract class Base
            {
                protected abstract bool AcceptAbstract(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors);

                static extern bool AcceptExtern(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors);

                void M(SslClientAuthenticationOptions options)
                {
                    static extern bool AcceptLocal(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors);

                    options.RemoteCertificateValidationCallback = AcceptAbstract;
                    options.RemoteCertificateValidationCallback = AcceptExtern;
                    options.RemoteCertificateValidationCallback = AcceptLocal;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallbackBlockIsASingleStatementThatIsNotAReturn()
    {
        return VerifyAsync("""
            using System;
            using System.Net.Security;

            class C
            {
                void M(SslClientAuthenticationOptions options)
                {
                    options.RemoteCertificateValidationCallback = (_, _, _, _) => { throw new InvalidOperationException(); };
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlySslStreamExists()
    {
        return VerifyAsync(ReferenceAssemblies.NetFramework.Net20.Default, """
            using System.Net.Security;

            class C
            {
                void M(System.IO.Stream stream)
                {
                    var ssl = {|E128156:new SslStream(stream, false, delegate { return true; })|};
                }
            }
            """);
    }
}
