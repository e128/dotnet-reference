using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemInfoEqualityAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_Equality_ReportsOnEquals()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
                           class C
                           {
                               bool M(string a, string b) => a == b;
                           }
                           """);
    }
}
