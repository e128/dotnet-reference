using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsTlsVersionAnalyzerBranchCoverageMoreTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsTlsVersionAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAConvertedMaskResultIsComparedWithZero()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                bool M(SslProtocols p)
                {
                    return (int)(p & SslProtocols.Tls11) != 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAConvertedComplementMasksAProtocolValue()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                int M(SslProtocols p)
                {
                    return (int)p & (int)~SslProtocols.Tls11;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAComplementIsOredIntoAValue()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                void M(SslProtocols p)
                {
                    p |= ~{|E128146:SslProtocols.Tls11|};
                    p ^= ~{|E128146:SslProtocols.Tls|};
                }
            }
            """);
    }


    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAMaskedValueIsComplemented()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                SslProtocols M(SslProtocols p)
                {
                    return ~({|E128146:SslProtocols.Tls11|} & p);
                }
            }
            """);
    }
}
