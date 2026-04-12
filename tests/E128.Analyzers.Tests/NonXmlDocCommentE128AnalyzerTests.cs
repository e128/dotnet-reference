using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NonXmlDocCommentE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<NonXmlDocCommentE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SlashSlashAboveMethod_Fires()
    {
        return VerifyAsync("""
            class Foo
            {
                {|E128024:// This does something|}
                void DoSomething() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task XmlDocAboveMethod_NoFire()
    {
        return VerifyAsync("""
            class Foo
            {
                /// <summary>This is proper XML doc.</summary>
                void DoSomething() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CommentInsideBody_NoFire()
    {
        return VerifyAsync("""
            class Foo
            {
                void DoSomething()
                {
                    // This comment is inside the body — fine
                    var x = 1;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task BlankLineBetweenCommentAndMethod_Fires()
    {
        return VerifyAsync("""
            class Foo
            {
                {|E128024:// Separated by blank line|}

                void DoSomething() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MultipleCommentLines_Fires()
    {
        return VerifyAsync("""
            class Foo
            {
                {|E128024:// First line|}
                {|E128024:// Second line|}
                void DoSomething() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CommentAboveProperty_NoFire()
    {
        return VerifyAsync("""
            class Foo
            {
                // This is above a property, not a method
                public int Value { get; set; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CommentAboveLocalFunction_Fires()
    {
        return VerifyAsync("""
            class Foo
            {
                void Outer()
                {
                    {|E128024:// Above a local function|}
                    void Inner() { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DecorativeSeparatorNotAboveMethod_NoFire()
    {
        return VerifyAsync("""
            class Foo
            {
                public int A { get; set; }

                // ── Section separator ───
                public int B { get; set; }
            }
            """);
    }
}
