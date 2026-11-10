using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsTlsVersionAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FipsTlsVersionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Tls10Member_Reports_E128146()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               void M()
                               {
                                   var p = {|E128146:SslProtocols.Tls|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Tls12Member_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               void M()
                               {
                                   var p = SslProtocols.Tls12;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NumericCast_Reports_E128146()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               void M()
                               {
                                   var p = {|E128146:(SslProtocols)192|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WeakCryptoSwitch_Reports_E128146()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   {|E128146:AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StrongCryptoSwitchFalse_NoReport()
    {
        return VerifyAsync("""
                           using System;
                           class C
                           {
                               void M()
                               {
                                   AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WeakSslProtocolsMembers_Report_E128146()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               void M()
                               {
                                   var ssl2 = {|E128146:SslProtocols.Ssl2|};
                                   var ssl3 = {|E128146:SslProtocols.Ssl3|};
                                   var tls11 = {|E128146:SslProtocols.Tls11|};
                                   var other = {|E128146:SslProtocols.Default|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WeakSecurityProtocolTypeMembers_Report_E128146()
    {
        return VerifyAsync("""
                           using System.Net;
                           class C
                           {
                               void M()
                               {
                                   var ssl3 = {|E128146:SecurityProtocolType.Ssl3|};
                                   var tls = {|E128146:SecurityProtocolType.Tls|};
                                   var tls11 = {|E128146:SecurityProtocolType.Tls11|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ComplementOfAllowedProtocol_Reports_E128146()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               void M()
                               {
                                   var all = {|E128146:~SslProtocols.None|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ProtocolComparison_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               bool M(SslProtocols protocols)
                               {
                                   return protocols == SslProtocols.Tls11;
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MaskRemoval_NoReport()
    {
        return VerifyAsync("""
                           using System.Security.Authentication;
                           class C
                           {
                               SslProtocols M(SslProtocols protocols)
                               {
                                   protocols &= ~SslProtocols.Tls11;
                                   return protocols;
                               }
                           }
                           """);
    }
}
