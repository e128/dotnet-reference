using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemInfoEqualityAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FileSystemInfoEqualityAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_Equality_ReportsOnEquals()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                bool M(FileInfo a, FileInfo b) => a {|E128030:==|} b;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DirectoryInfo_Inequality_ReportsOnNotEquals()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                bool M(DirectoryInfo a, DirectoryInfo b) => a {|E128030:!=|} b;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_EqualsMethod_ReportsOnInstanceCall()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                bool M(FileInfo a, FileInfo b) => a.{|E128030:Equals|}(b);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_NullComparison_DoesNotReport()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                bool M(FileInfo a) => a == null;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_FullNameComparison_DoesNotReport()
    {
        return VerifyAsync("""
            using System.IO;
            class C
            {
                bool M(FileInfo a, FileInfo b) => a.FullName == b.FullName;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task String_Equality_DoesNotReport()
    {
        return VerifyAsync("""
            class C
            {
                bool M(string a, string b) => a == b;
            }
            """);
    }
}
