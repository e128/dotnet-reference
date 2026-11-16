using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsUnapprovedHashCodeFixBranchCoverageTests
{
    private static Task NoFixAsync(string source)
    {
        return CodeFixTestRunner.NoFixAsync<FipsUnapprovedHashAnalyzer, FipsUnapprovedHashCodeFixProvider>(source);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheForbiddenTypeIsWrittenWithItsNamespace()
    {
        return NoFixAsync("""
            class C
            {
                void M()
                {
                    var a = {|E128071:System.Security.Cryptography.MD5.Create()|};
                    var b = {|E128071:new System.Security.Cryptography.SHA1Managed()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheForbiddenTypeHasNoApprovedReplacementInTheMap()
    {
        return NoFixAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var a = {|E128071:new SHA1Managed()|};
                    var b = {|E128071:new MD5CryptoServiceProvider()|};
                    var c = {|E128071:TripleDES.Create()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenTheDiagnosticIsNeitherACreationNorAMemberCall()
    {
        return NoFixAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSA rsa, byte[] data)
                {
                    var padding = {|E128071:RSAEncryptionPadding.OaepSHA1|};
                    var a = rsa.Encrypt(data, padding);
                    var b = {|E128071:CryptoConfig.CreateFromName("MD5")|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesSha1ConstructionAndKeepsTheTrivia()
    {
        return CodeFixTestRunner.RunAsync<FipsUnapprovedHashAnalyzer, FipsUnapprovedHashCodeFixProvider>(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] key)
                {
                    using var a = /* before */ {|E128071:new HMACSHA1(key)|}; // after
                    var b = /* lead */ {|E128071:MD5.HashData(key)|};
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] key)
                {
                    using var a = /* before */ new HMACSHA256(key); // after
                    var b = /* lead */ SHA256.HashData(key);
                }
            }
            """);
    }
}
