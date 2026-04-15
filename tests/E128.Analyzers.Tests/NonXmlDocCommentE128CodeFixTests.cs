using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NonXmlDocCommentE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<NonXmlDocCommentE128Analyzer, NonXmlDocCommentCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RemovesNonXmlDocComment()
    {
        return VerifyFixAsync(
            """
            class Foo
            {
                {|E128024:// This does something|}
                void DoSomething() { }
            }
            """,
            """
            class Foo
            {
                void DoSomething() { }
            }
            """);
    }
}
