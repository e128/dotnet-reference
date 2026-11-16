using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsSha3BranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>(code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_Sha3TypesCreatedOrCalledStatically()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(byte[] key, byte[] data)
                {
                    var a = {|E128150:new HMACSHA3_256(key)|};
                    var b = {|E128150:SHA3_256.HashData(data)|};
                    var c = {|E128150:Shake128.HashData(data, 16)|};
                    var d = SHA256.HashData(data);
                    var e = SHA512.HashData(data);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_Sha3PaddingButNotAnIsSupportedRead()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                void M(RSA rsa, byte[] data)
                {
                    var a = rsa.Encrypt(data, {|E128150:RSAEncryptionPadding.OaepSHA3_256|});
                    var b = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                    var c = SHA3_256.IsSupported;
                    var d = RSAEncryptionPadding.Pkcs1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_Sha3FactoryMethodGroupButNotAnInstanceMethodGroup()
    {
        return VerifyAsync("""
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M(SHA256 sha256)
                {
                    Func<byte[], byte[]> a = {|E128150:SHA3_256.HashData|};
                    Func<byte[], byte[]> b = SHA256.HashData;
                    Func<byte[], byte[]> c = sha256.ComputeHash;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_Sha3HashNamesPassedAsArgumentsOrStored()
    {
        return VerifyAsync("""
            using System.Security.Cryptography;
            class C
            {
                private static HashAlgorithmName stored = {|E128150:HashAlgorithmName.SHA3_256|};

                static void Use(HashAlgorithmName name) { }

                void M(byte[] data, byte[] salt, SHA256 sha256)
                {
                    {|E128150:Use(HashAlgorithmName.SHA3_384)|};
                    {|E128150:Use(new HashAlgorithmName("SHA3-512"))|};
                    var a = {|E128150:IncrementalHash.CreateHash(HashAlgorithmName.SHA3_256)|};
                    var b = {|E128150:new Rfc2898DeriveBytes("password", salt, 10, HashAlgorithmName.SHA3_256)|};
                    var c = sha256.ComputeHash(data);
                    Use(HashAlgorithmName.SHA256);
                    HashAlgorithmName local;
                    {|E128150:local = HashAlgorithmName.SHA3_512|};
                    local = HashAlgorithmName.SHA384;
                }
            }
            """);
    }
}
