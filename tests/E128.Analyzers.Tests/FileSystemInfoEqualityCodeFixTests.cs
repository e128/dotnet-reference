using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemInfoEqualityCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<FileSystemInfoEqualityAnalyzer, FileSystemInfoEqualityCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        }.RunAsync();
    }

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
}
