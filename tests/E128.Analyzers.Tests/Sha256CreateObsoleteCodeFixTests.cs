using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class Sha256CreateObsoleteCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return CodeFixTestRunner.RunAsync<Sha256CreateObsoleteAnalyzer, Sha256CreateObsoleteCodeFixProvider>(source, fixedCode);
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
        return CodeFixTestRunner.RunAsync<Sha256CreateObsoleteAnalyzer, Sha256CreateObsoleteCodeFixProvider>(
            """
                       using System.Security.Cryptography;
                       class C
                       {
                           void M()
                           {
                               using var h = {|E128072:SHA256.Create()|};
                           }
                       }
                       """,
            """
                        using System.Security.Cryptography;
                        class C
                        {
                            void M()
                            {
                                using var h = {|E128072:SHA256.Create()|};
                            }
                        }
                        """,
            options: new CodeFixTestOptions { FixAllInDocumentIterations = 0 });
    }
}
