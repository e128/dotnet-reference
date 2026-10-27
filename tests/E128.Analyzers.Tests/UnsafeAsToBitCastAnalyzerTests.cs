using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAsToBitCastAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UnsafeAsToBitCastAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenBothTypesHaveTheSameSize()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   int value = 1;
                                   float result = {|E128112:Unsafe.As<int, float>(ref value)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenSizesDiffer()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   int value = 1;
                                   long result = Unsafe.As<int, long>(ref value);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTargetIsAReferenceType()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   int value = 1;
                                   object result = Unsafe.As<int, object>(ref value);
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheResultIsUsedAsAReference()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               static void Use(ref float value)
                               {
                               }

                               void M()
                               {
                                   int value = 1;
                                   Unsafe.As<int, float>(ref value) = 1f;
                                   ref float alias = ref Unsafe.As<int, float>(ref value);
                                   Use(ref Unsafe.As<int, float>(ref value));
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCalledThroughUsingStatic()
    {
        return VerifyAsync("""
                           using static System.Runtime.CompilerServices.Unsafe;
                           class C
                           {
                               float M(int value)
                               {
                                   return {|E128112:As<int, float>(ref value)|};
                               }
                           }
                           """);
    }
}
