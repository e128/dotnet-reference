using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAddArrayDataReferenceCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<
            UnsafeAddArrayDataReferenceAnalyzer,
            UnsafeAddArrayDataReferenceCodeFixProvider,
            DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ReplacesTheAddWithAnIndexer()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            class C
            {
                void M(int[] items)
                {
                    ref int value = ref {|E128114:Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(items), 1)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            class C
            {
                void M(int[] items)
                {
                    ref int value = ref items[1];
                }
            }
            """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Fix_ParenthesizesACastArray()
    {
        return VerifyFixAsync(
            """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            class C
            {
                int M(object source, int index)
                {
                    return {|E128114:Unsafe.Add(ref MemoryMarshal.GetArrayDataReference((int[])source), index)|};
                }
            }
            """,
            """
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            class C
            {
                int M(object source, int index)
                {
                    return ((int[])source)[index];
                }
            }
            """);
    }
}
