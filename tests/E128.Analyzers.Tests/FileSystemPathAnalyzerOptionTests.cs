using System.Threading.Tasks;
using E128.Analyzers.FileSystem;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FileSystemPathAnalyzerOptionTests
{
    private const string OptionStub =
        "class Option<T> { public Option(string name) { } }\n" +
        "class Argument<T> { public Argument(string name) { } }\n";

    private static Task VerifyAsync(string testBody, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<FileSystemPathAnalyzer, DefaultVerifier>
        {
            TestCode = OptionStub + testBody,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Option_StringInput_Fires()
    {
        return VerifyAsync("""
            class C { void M() { var opt = new Option<{|E128001:string|}>("--input"); } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Option_StringDirectory_Fires()
    {
        return VerifyAsync("""
            class C { void M() { var opt = new Option<{|E128001:string|}>("--directory"); } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Option_StringFile_Fires()
    {
        return VerifyAsync("""
            class C { void M() { var opt = new Option<{|E128001:string|}>("--file"); } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Option_StringVerbose_NoFire()
    {
        return VerifyAsync("""
            class C { void M() { var opt = new Option<string>("--verbose"); } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Argument_StringPath_Fires()
    {
        return VerifyAsync("""
            class C { void M() { var arg = new Argument<{|E128001:string|}>("path"); } }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Argument_StringUrl_NoFire()
    {
        return VerifyAsync("""
            class C { void M() { var arg = new Argument<string>("url"); } }
            """);
    }
}
