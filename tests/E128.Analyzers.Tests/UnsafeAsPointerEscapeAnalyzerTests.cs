using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeAsPointerEscapeAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenThePointerIsNotPinned()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(int[] values)
                               {
                                   void* pointer = {|E128125:Unsafe.AsPointer(ref values[0])|};
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsInsideAFixedBlock()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheStackLocalLivesInAnAsyncMethod()
    {
        // An async member moves its locals into a heap state machine, which moves during a collection.
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>("""
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
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePointerTargetsAStackLocal()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeAsPointerEscapeAnalyzer>("""
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
                           """, options: Options);
    }
}
