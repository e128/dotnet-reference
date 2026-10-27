using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAddArrayDataReferenceAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UnsafeAddArrayDataReferenceAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAddingOverTheArrayDataReference()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               void M(int[] items)
                               {
                                   ref int value = ref {|E128114:Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(items), 1)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAddingOverAPlainReference()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M(int[] items)
                               {
                                   ref int first = ref items[0];
                                   ref int value = ref Unsafe.Add(ref first, 1);
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheArrayIsMultiDimensional()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           using System.Runtime.InteropServices;
                           class C
                           {
                               int M(int[,] grid, int index)
                               {
                                   return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(grid), index) is byte value ? value : 0;
                               }
                           }
                           """);
    }
}
