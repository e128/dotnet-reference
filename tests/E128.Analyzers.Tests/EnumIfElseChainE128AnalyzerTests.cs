using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumIfElseChainE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<EnumIfElseChainAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeBranchEnumChain_Fires()
    {
        return VerifyAsync("""
            enum Color { Red, Green, Blue }

            class C
            {
                void M(Color c)
                {
                    {|E128048:if (c == Color.Red)
                    {
                    }
                    else if (c == Color.Green)
                    {
                    }
                    else if (c == Color.Blue)
                    {
                    }|}
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task TwoBranchEnumChain_NoDiagnostic()
    {
        return VerifyAsync("""
            enum Color { Red, Green }

            class C
            {
                void M(Color c)
                {
                    if (c == Color.Red)
                    {
                    }
                    else if (c == Color.Green)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeBranchNonEnumChain_NoDiagnostic()
    {
        return VerifyAsync("""
            class C
            {
                void M(int x)
                {
                    if (x == 1) { }
                    else if (x == 2) { }
                    else if (x == 3) { }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MixedEnumTypes_NoDiagnostic()
    {
        return VerifyAsync("""
            enum Color { Red, Green, Blue }
            enum Size { Small, Medium, Large }

            class C
            {
                void M(Color c, Size s)
                {
                    if (c == Color.Red) { }
                    else if (s == Size.Small) { }
                    else if (c == Color.Blue) { }
                }
            }
            """);
    }
}
