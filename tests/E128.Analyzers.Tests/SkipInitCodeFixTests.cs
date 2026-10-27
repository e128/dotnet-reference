using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SkipInitCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            SkipInitAnalyzer,
            SkipInitCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_DeclaresTheLocalWithADefaultValue()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    {|E128120:Unsafe.SkipInit(out int value)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = default;
                }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_SpellsOutTheTypeForAVarDeclaration()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    {|E128120:Unsafe.SkipInit<int>(out var value)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value = default;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_AssignsAnExistingLocal()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value;
                    {|E128120:Unsafe.SkipInit(out value)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            class C
            {
                void M()
                {
                    int value;
                    value = default;
                }
            }
            """);
    }
}
