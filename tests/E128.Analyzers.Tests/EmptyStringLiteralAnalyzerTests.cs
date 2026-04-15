using System.Threading.Tasks;
using E128.Analyzers.Style;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EmptyStringLiteralAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<EmptyStringLiteralAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task VariableDeclaration_EmptyLiteral_Fires()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var x = {|E128002:""|};
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReturnStatement_EmptyLiteral_Fires()
    {
        return VerifyAsync("""
            class C
            {
                string M() => {|E128002:""|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AttributeArgument_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                [System.Obsolete("")]
                void M() { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ConstField_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                private const string K = "";
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DefaultParameter_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M(string s = "") { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonEmptyLiteral_NoFire()
    {
        return VerifyAsync("""
            class C
            {
                void M()
                {
                    var x = "hello";
                }
            }
            """);
    }
}
