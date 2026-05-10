using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Sha256CreateObsoleteCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<Sha256CreateObsoleteAnalyzer, Sha256CreateObsoleteCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InlineChain_ComputeHash()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                byte[] M(byte[] data) => {|E128072:SHA256.Create()|}.ComputeHash(data);
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                byte[] M(byte[] data) => SHA256.HashData(data);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_InlineChain_ComputeHash_Stream()
    {
        return VerifyFixAsync(
            """
            using System.IO;
            using System.Security.Cryptography;
            class C
            {
                byte[] M(Stream s) => {|E128072:SHA256.Create()|}.ComputeHash(s);
            }
            """,
            """
            using System.IO;
            using System.Security.Cryptography;
            class C
            {
                byte[] M(Stream s) => SHA256.HashData(s);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NoFix_WhenNotChainedWithComputeHash()
    {
        return new CSharpCodeFixTest<Sha256CreateObsoleteAnalyzer, Sha256CreateObsoleteCodeFixProvider, DefaultVerifier>
        {
            TestCode = """
                       using System.Security.Cryptography;
                       class C
                       {
                           void M()
                           {
                               using var h = {|E128072:SHA256.Create()|};
                           }
                       }
                       """,
            FixedCode = """
                        using System.Security.Cryptography;
                        class C
                        {
                            void M()
                            {
                                using var h = {|E128072:SHA256.Create()|};
                            }
                        }
                        """,
            NumberOfFixAllInDocumentIterations = 0
        }.RunAsync();
    }
}
