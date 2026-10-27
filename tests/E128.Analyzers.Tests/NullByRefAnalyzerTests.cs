using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class NullByRefAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<NullByRefAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenNullRefIsUsed()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   ref int value = ref {|E128127:Unsafe.NullRef<int>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenAsRefIsGivenANullPointer()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M()
                               {
                                   ref int value = ref {|E128127:Unsafe.AsRef<int>((void*)0)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAsRefIsGivenARealPointer()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(int[] values)
                               {
                                   fixed (int* pointer = values)
                                   {
                                       ref int value = ref Unsafe.AsRef<int>(pointer);
                                   }
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenNullRefIsReturnedAsASentinel()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               private int _value;

                               ref int Find(bool found)
                               {
                                   if (!found)
                                   {
                                       return ref Unsafe.NullRef<int>();
                                   }

                                   return ref _value;
                               }
                           }
                           """);
    }
}
