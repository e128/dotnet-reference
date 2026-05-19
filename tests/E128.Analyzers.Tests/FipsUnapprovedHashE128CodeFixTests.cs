using System.Threading.Tasks;
using E128.Analyzers.Security;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FipsUnapprovedHashE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<FipsUnapprovedHashAnalyzer, FipsUnapprovedHashCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<FipsUnapprovedHashAnalyzer, FipsUnapprovedHashCodeFixProvider, DefaultVerifier>
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
    public Task MD5Create_FixedToSHA256Create()
    {
        return VerifyFixAsync(
            """
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    using var h = {|E128071:MD5.Create()|};
                }
            }
            """,
            """
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    using var h = SHA256.Create();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SHA1HashData_FixedToSHA256HashData()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var hash = {|E128071:SHA1.HashData(Array.Empty<byte>())|};
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
                    var hash = SHA256.HashData(Array.Empty<byte>());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DESCreate_NoFixOffered()
    {
        return VerifyNoFixAsync(
            """
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    using var h = {|E128071:DES.Create()|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HMACMD5Constructor_FixedToHMACSHA256()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    using var h = {|E128071:new HMACMD5(Array.Empty<byte>())|};
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
                    using var h = new HMACSHA256(Array.Empty<byte>());
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HMACSHA1HashData_FixedToHMACSHA256HashData()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Security.Cryptography;
            class C
            {
                void M()
                {
                    var hash = {|E128071:HMACSHA1.HashData(Array.Empty<byte>(), Array.Empty<byte>())|};
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
                    var hash = HMACSHA256.HashData(Array.Empty<byte>(), Array.Empty<byte>());
                }
            }
            """);
    }
}
