using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsCipherModeBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsCipherModeAnalyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForbiddenModeFieldsButNotAuthenticatedOrCbcModes()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(Aes aes)
                {
                    aes.Mode = {|E128144:CipherMode.ECB|};
                    aes.Mode = {|E128144:CipherMode.OFB|};
                    aes.Mode = {|E128144:CipherMode.CFB|};
                    aes.Mode = CipherMode.CBC;
                    aes.Mode = CipherMode.CTS;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAForbiddenModeIsOnlyCompared()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                bool M(CipherMode mode)
                {
                    var a = mode == CipherMode.ECB;
                    var b = mode != CipherMode.CFB;
                    var c = mode is CipherMode.OFB;
                    return a && b && c && mode.Equals(CipherMode.ECB);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_NumericCastsToForbiddenModes()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(Aes aes, int number)
                {
                    aes.Mode = {|E128144:(CipherMode)2|};
                    aes.Mode = {|E128144:(CipherMode)3|};
                    aes.Mode = {|E128144:(CipherMode)4|};
                    aes.Mode = (CipherMode)1;
                    aes.Mode = (CipherMode)5;
                    aes.Mode = (CipherMode)number;
                    aes.Mode = (CipherMode)PaddingMode.PKCS7;
                    var same = aes.Mode == (CipherMode)2;
                    var number2 = (int)CipherMode.CBC;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_EcbAndCfbHelperMethodsOfSymmetricAlgorithms()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(Aes aes, byte[] data, byte[] iv, Span<byte> destination)
                {
                    var a = {|E128144:aes.EncryptEcb(data, PaddingMode.PKCS7)|};
                    var b = {|E128144:aes.DecryptEcb(data, PaddingMode.PKCS7)|};
                    var c = {|E128144:aes.TryEncryptEcb(data, destination, PaddingMode.PKCS7, out _)|};
                    var d = {|E128144:aes.TryDecryptEcb(data, destination, PaddingMode.PKCS7, out _)|};
                    var e = {|E128144:aes.EncryptCfb(data, iv)|};
                    var f = {|E128144:aes.DecryptCfb(data, iv)|};
                    var g = {|E128144:aes.TryEncryptCfb(data, iv, destination, out _)|};
                    var h = {|E128144:aes.TryDecryptCfb(data, iv, destination, out _)|};
                    var i = aes.EncryptCbc(data, iv);
                    var j = aes.CreateEncryptor();
                    var k = new Own().EncryptEcb(data);
                }
            }

            class Own
            {
                public byte[] EncryptEcb(byte[] data) => data;
            }
            """);
    }
}
