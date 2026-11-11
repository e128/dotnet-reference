using System.Threading.Tasks;
using E128.Analyzers.Security;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsSha3AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task UserDefinedProbeWithHashName_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;

                           static class Probe
                           {
                               public static bool Contains(HashAlgorithmName left, HashAlgorithmName right) => false;
                           }

                           class C
                           {
                               void M()
                               {
                                   var found = {|E128150:Probe.Contains(HashAlgorithmName.SHA3_256, HashAlgorithmName.SHA256)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BclProbeWithHashName_NoDiagnostic()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;

                           class C
                           {
                               void M()
                               {
                                   var same = HashAlgorithmName.SHA3_256.Equals(HashAlgorithmName.SHA256);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RepeatedHashNameInOneCall_ReportsOnce()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;

                           class C
                           {
                               void Use(HashAlgorithmName first, HashAlgorithmName second)
                               {
                               }

                               void M()
                               {
                                   {|E128150:Use(HashAlgorithmName.SHA3_256, HashAlgorithmName.SHA3_256)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConditionalInReadonlyField_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System;
                           using System.Security.Cryptography;

                           class C
                           {
                               private static readonly HashAlgorithmName Name;
                               private static readonly HashAlgorithmName Other =
                                   Environment.GetEnvironmentVariable("HASH") is null ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA3_256;

                               static C()
                               {
                                   Name = HashAlgorithmName.SHA256;
                               }

                               void Use(HashAlgorithmName name)
                               {
                               }

                               void M()
                               {
                                   {|E128150:Use(Other)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Sha3HashData_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var hash = {|E128150:SHA3_256.HashData(new byte[1])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Sha256HashData_NoReport()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var hash = SHA256.HashData(new byte[1]);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OaepSha3Padding_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var padding = {|E128150:RSAEncryptionPadding.OaepSHA3_256|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ShakeType_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var xof = {|E128150:Shake128.HashData(new byte[1], 16)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HmacSha3Type_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var mac = {|E128150:HMACSHA3_256.HashData(new byte[32], new byte[1])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task KmacType_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var mac = {|E128150:Kmac128.HashData(new byte[32], new byte[1], 16)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Sha3HashNameInLocal_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void Use(HashAlgorithmName name)
                               {
                               }

                               void M()
                               {
                                   var name = HashAlgorithmName.SHA3_256;
                                   {|E128150:Use(name)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Sha3HashNameAssignedToField_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               private HashAlgorithmName name;

                               void M()
                               {
                                   {|E128150:name = HashAlgorithmName.SHA3_384|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Sha3HashNameInMutableFieldInitializer_Reports_E128150()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               private HashAlgorithmName name = {|E128150:HashAlgorithmName.SHA3_512|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IsSupportedRead_NoReport()
    {
        return AnalyzerTestRunner.VerifyAsync<FipsSha3Analyzer>("""
                           using System.Security.Cryptography;
                           class C
                           {
                               bool M()
                               {
                                   return SHA3_256.IsSupported;
                               }
                           }
                           """);
    }
}
