using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemInfoEqualityCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<FileSystemInfoEqualityAnalyzer, FileSystemInfoEqualityCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_Equality_FixReplacesWithStringEquals()
    {
        const string source = """
                              using System;
                              using System.IO;
                              class C
                              {
                                  bool M(FileInfo a, FileInfo b) => a {|E128030:==|} b;
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.IO;
                                 class C
                                 {
                                     bool M(FileInfo a, FileInfo b) => string.Equals(a.FullName, b.FullName, StringComparison.Ordinal);
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_Inequality_FixReplacesWithNegatedStringEquals()
    {
        const string source = """
                              using System;
                              using System.IO;
                              class C
                              {
                                  bool M(FileInfo a, FileInfo b) => a {|E128030:!=|} b;
                              }
                              """;

        const string fixedCode = """
                                 using System;
                                 using System.IO;
                                 class C
                                 {
                                     bool M(FileInfo a, FileInfo b) => !string.Equals(a.FullName, b.FullName, StringComparison.Ordinal);
                                 }
                                 """;

        return VerifyFixAsync(source, fixedCode);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FileInfo_Equality_NoFix_WhenOtherOperandIsNotFileSystemInfo()
    {
        // The rule fires on either operand, but .FullName exists only on a FileSystemInfo.
        return VerifyNoFixAsync("""
                                using System.IO;
                                class C
                                {
                                    bool M(FileInfo file, object other) => file {|E128030:==|} other;
                                }
                                """);
    }

    private static Task VerifyNoFixAsync(string code) =>
        CodeFixTestRunner.NoFixAsync<FileSystemInfoEqualityAnalyzer, FileSystemInfoEqualityCodeFixProvider>(code);
}
