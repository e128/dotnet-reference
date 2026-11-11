using System.Threading.Tasks;
using E128.Analyzers.Style;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NonXmlDocCommentE128AnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task SlashSlashAboveMethod_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
                           class Foo
                           {
                               public int A { get; set; }

                               // ── Section separator ───
                               public int B { get; set; }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PragmaJustification_NoFire()
    {
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
                           class Foo
                           {
                           #pragma warning disable CA1822
                               // Intentional: stays instance for the container.
                               public void Run() { }
                           #pragma warning restore CA1822
                           }
                           """);
    }

    /// <summary>
    ///     A restore that ends an earlier pragma block lands in the next method's leading
    ///     trivia, so only a disable suppresses the rule.
    /// </summary>
    [Fact]
    [Trait("Category", "CI")]
    public Task CommentsAfterARestore_Fire()
    {
        return AnalyzerTestRunner.VerifyAsync<NonXmlDocCommentE128Analyzer>("""
                           class Foo
                           {
                               void A() { }
                           #pragma warning restore CA1822
                               {|E128024:// Not covered by the earlier pragma block|}
                               public void B() { }
                           }
                           """);
    }
}
