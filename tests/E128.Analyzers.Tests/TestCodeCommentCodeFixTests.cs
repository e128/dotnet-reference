using System.Threading.Tasks;
using E128.Analyzers.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class TestCodeCommentCodeFixTests
{
    private static readonly ReferenceAssemblies Net100WithXunit = ReferenceAssemblies.Net.Net100
        .AddPackages([new PackageIdentity("xunit.v3.core", "3.2.2")]);

    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<TestCodeCommentAnalyzer, TestCodeCommentCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = Net100WithXunit
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesComment_WithoutExtraBlankLines()
    {
        return VerifyFixAsync(
            """
            using Xunit;

            public class C
            {
                [Fact]
                public void M()
                {
                    {|E128097:// comment|}
                    var x = 1;
                }
            }
            """,
            """
            using Xunit;

            public class C
            {
                [Fact]
                public void M()
                {
                    var x = 1;
                }
            }
            """);
    }
}
