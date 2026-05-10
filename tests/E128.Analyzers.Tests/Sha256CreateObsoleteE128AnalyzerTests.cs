using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Sha256CreateObsoleteE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<Sha256CreateObsoleteAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SHA256Create_Reports_E128072()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var h = {|E128072:SHA256.Create()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SHA256HashData_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System;
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   var hash = SHA256.HashData(Array.Empty<byte>());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IncrementalHashCreateHash_NoDiagnostic()
    {
        return VerifyAsync("""
                           using System.Security.Cryptography;
                           class C
                           {
                               void M()
                               {
                                   using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                               }
                           }
                           """);
    }
}
