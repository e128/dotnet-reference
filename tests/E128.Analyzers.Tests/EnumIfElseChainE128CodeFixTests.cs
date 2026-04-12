using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EnumIfElseChainE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<EnumIfElseChainAnalyzer, EnumIfElseChainCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ThreeBranchChain_ConvertsToSwitch()
    {
        const string source = """
            enum Color { Red, Green, Blue }

            class C
            {
                int M(Color c)
                {
                    {|E128048:if (c == Color.Red)
                    {
                        return 1;
                    }
                    else if (c == Color.Green)
                    {
                        return 2;
                    }
                    else if (c == Color.Blue)
                    {
                        return 3;
                    }|}
                    return 0;
                }
            }
            """;

        const string fixedCode = """
            enum Color { Red, Green, Blue }

            class C
            {
                int M(Color c)
                {
                    switch (c)
                    {
                        case Color.Red:
                            return 1;
                            break;
                        case Color.Green:
                            return 2;
                            break;
                        case Color.Blue:
                            return 3;
                            break;
                    }
                    return 0;
                }
            }
            """;

        return VerifyFixAsync(source, fixedCode);
    }
}
