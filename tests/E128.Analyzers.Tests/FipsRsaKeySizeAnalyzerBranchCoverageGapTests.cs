using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsRsaKeySizeAnalyzerBranchCoverageGapTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsRsaKeySizeAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RsaKeyImportedThroughANativeHandle()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(IntPtr handle)
                {
                    var a = {|E128143:new RSAOpenSsl(handle)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_RsaFactoryCallsThatTakeANameOrAKeySize()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var a = {|E128143:RSA.Create("RSA")|};
                    var b = {|E128143:RSA.Create("DSA")|};
                    var c = RSA.Create(3072);
                    var d = {|E128143:RSA.Create(2048)|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ImportOnAConditionalReceiver()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSA? rsa, RSAParameters parameters)
                {
                    rsa?{|E128143:.ImportParameters(parameters)|};
                }
            }
            """);
    }
}
