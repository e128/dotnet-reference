using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemInfoEqualityAnalyzerBranchCoverageMoreTests
{
    private static readonly AnalyzerTestOptions IgnoreCompilerErrors = new() { CompilerDiagnostics = CompilerDiagnostics.None };

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLeftOperandIsNullOrDefault()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
            using System.IO;
            class C
            {
                bool M(FileInfo file) => null == file || default == file;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnOperandHasNoType()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>(
            """
            using System.IO;
            class C
            {
                void Method()
                {
                }

                bool M(FileInfo file) => file == Method || Method == file;
            }
            """,
            expected: null,
            options: IgnoreCompilerErrors);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCallsThatAreNotAMemberEqualsCall()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>("""
            using System.IO;
            class C
            {
                static bool Helper(FileInfo file) => true;

                bool M(FileInfo file, string name)
                {
                    var text = file.ToString();
                    return Helper(file) && name.Equals("a") && string.Equals(name, "b") && file.FullName.Equals(name);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheEqualsReceiverHasNoType()
    {
        return AnalyzerTestRunner.VerifyAsync<FileSystemInfoEqualityAnalyzer>(
            """
            class C
            {
                void Method()
                {
                }

                bool M() => Method.Equals(1);
            }
            """,
            expected: null,
            options: IgnoreCompilerErrors);
    }
}
