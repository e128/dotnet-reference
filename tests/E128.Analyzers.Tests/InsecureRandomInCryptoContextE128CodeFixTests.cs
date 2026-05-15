using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class InsecureRandomInCryptoContextE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<InsecureRandomInCryptoContextAnalyzer, InsecureRandomInCryptoContextCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNext_OneArg_FixedToGetInt32()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var x = {|E128075:Random.Shared|}.Next(10);
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var x = RandomNumberGenerator.GetInt32(10);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNext_TwoArgs_FixedToGetInt32()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var x = {|E128075:Random.Shared|}.Next(1, 100);
                }
            }
            """,
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var x = RandomNumberGenerator.GetInt32(1, 100);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NewRandom_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var rng = {|E128075:new Random()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RandomSharedNextDouble_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var x = {|E128075:Random.Shared|}.NextDouble();
                }
            }
            """);
    }
}
