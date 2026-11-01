using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAsPointerEscapeAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        var test = new CSharpAnalyzerTest<UnsafeAsPointerEscapeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add(CodeFixTestRunner.AllowUnsafe);

        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePointerIsNotPinned()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(int[] values)
                               {
                                   void* pointer = {|E128125:Unsafe.AsPointer(ref values[0])|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsInsideAFixedBlock()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(int[] values)
                               {
                                   fixed (int* pointer = values)
                                   {
                                       void* other = Unsafe.AsPointer(ref pointer[0]);
                                   }
                               }
                           }
                           """);
    }
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStackLocalLivesInAnAsyncMethod()
    {
        // An async member moves its locals into a heap state machine, which moves during a collection.
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           using System.Threading.Tasks;
                           class C
                           {
                               async Task M(int parameter)
                               {
                                   unsafe
                                   {
                                       void* pointer = {|E128125:Unsafe.AsPointer(ref parameter)|};
                                   }

                                   await Task.Yield();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerTargetsAStackLocal()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(int parameter)
                               {
                                   int local = 1;
                                   void* first = Unsafe.AsPointer(ref local);
                                   void* second = Unsafe.AsPointer(ref parameter);
                               }
                           }
                           """);
    }
}
