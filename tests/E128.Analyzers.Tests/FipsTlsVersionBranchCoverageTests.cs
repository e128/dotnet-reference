using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsTlsVersionBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsTlsVersionAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WeakMembersOfBothProtocolTypes()
    {
        return VerifyAsync("""
            using System.Net;
            using System.Security.Authentication;
            class C
            {
                void M()
                {
                    var a = {|E128146:SslProtocols.Ssl2|};
                    var b = {|E128146:SslProtocols.Tls11|} | SslProtocols.Tls12;
                    var c = {|E128146:SecurityProtocolType.Ssl3|};
                    var d = {|E128146:SecurityProtocolType.Tls|} | SecurityProtocolType.Tls12;
                    var e = SslProtocols.Tls13 | SslProtocols.None;
                    var f = SecurityProtocolType.SystemDefault;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAMemberOfAnotherTypeSharesAWeakName()
    {
        return VerifyAsync("""
            enum OwnProtocols
            {
                Tls,
                Tls11,
                Ssl3,
            }

            class C
            {
                void M()
                {
                    var a = OwnProtocols.Tls11;
                    var b = (OwnProtocols)3;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAWeakProtocolIsOnlyReadByAComparisonOrAPattern()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                void M(SslProtocols p)
                {
                    var a = p == SslProtocols.Tls11;
                    var b = p != SslProtocols.Tls;
                    var c = p is SslProtocols.Ssl3;
                    var d = p.HasFlag(SslProtocols.Tls11);
                    var e = (p & SslProtocols.Tls11) != 0;
                    var f = (p & SslProtocols.Tls) is not SslProtocols.None;
                    var g = (p & SslProtocols.Tls11) == SslProtocols.Tls11;
                    var h = ((int)p & (int)SslProtocols.Tls11) != 0;
                    switch (p)
                    {
                        case SslProtocols.Ssl2:
                            break;
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAMaskRemovesTheWeakProtocols()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                void M(SslProtocols p)
                {
                    p &= ~SslProtocols.Tls11;
                    var a = p & ~SslProtocols.Tls;
                    var b = p & ~(SslProtocols.Tls | SslProtocols.Tls11);
                    var c = (p & ~SslProtocols.Ssl3) & SslProtocols.Tls12;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_AComplementThatIsNotUsedAsAMask()
    {
        return VerifyAsync("""
            using System.Security.Authentication;
            class C
            {
                void M(SslProtocols p)
                {
                    var a = ~{|E128146:SslProtocols.Tls11|};
                    var b = {|E128146:~SslProtocols.None|};
                    var c = {|E128146:~SslProtocols.Tls12|};
                    var d = p & ~SslProtocols.Tls12;
                    var e = ~p;
                    var f = ~(SslProtocols.Tls12 | SslProtocols.Tls13);
                    var g = ~5;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NumericCastsThatCarryAWeakBit()
    {
        return VerifyAsync("""
            using System.Net;
            using System.Security.Authentication;
            class C
            {
                void M(SslProtocols p, int number)
                {
                    var a = {|E128146:(SslProtocols)192|};
                    var b = {|E128146:(SecurityProtocolType)768|};
                    var c = (SslProtocols)3072;
                    var d = (SslProtocols)number;
                    var e = p == (SslProtocols)192;
                    var f = (p & (SslProtocols)192) != 0;
                    var g = (SslProtocols)SecurityProtocolType.Tls12;
                    var h = (int)SslProtocols.Tls12;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_TheSwitchesThatReEnableWeakTls()
    {
        return VerifyAsync("""
            using System;
            class C
            {
                const string StrongCrypto = "Switch.System.Net.DontEnableSchUseStrongCrypto";

                static bool Flag => true;

                void M(string name, bool enabled)
                {
                    {|E128146:AppContext.SetSwitch(StrongCrypto, true)|};
                    {|E128146:AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", true)|};
                    {|E128146:AppContext.SetSwitch(isEnabled: true, switchName: "Switch.System.Net.DontEnableSchUseStrongCrypto")|};
                    AppContext.SetSwitch(StrongCrypto, false);
                    AppContext.SetSwitch("Switch.Other.Setting", true);
                    AppContext.SetSwitch(name, true);
                    AppContext.SetSwitch(StrongCrypto, enabled);
                    AppContext.SetSwitch(StrongCrypto, Flag);
                    var data = AppContext.GetData("Switch.System.Net.DontEnableSchUseStrongCrypto");
                    new Own().SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", true);
                }
            }

            class Own
            {
                public void SetSwitch(string name, bool value) { }
            }
            """);
    }
}
