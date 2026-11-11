using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class FunctionPointerTypeAnalyzerTests
{

    private static AnalyzerTestOptions Options => new() { AllowUnsafe = true };

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnAField()
    {
        return AnalyzerTestRunner.VerifyAsync<FunctionPointerTypeAnalyzer>("""
                           unsafe class C
                           {
                               {|E128200:delegate*<int, int>|} field;
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnAnUnmanagedPointer()
    {
        return AnalyzerTestRunner.VerifyAsync<FunctionPointerTypeAnalyzer>("""
                           unsafe class C
                           {
                               {|E128200:delegate* unmanaged[Cdecl]<int, void>|} field;
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnALocal()
    {
        return AnalyzerTestRunner.VerifyAsync<FunctionPointerTypeAnalyzer>("""
                           unsafe class C
                           {
                               void M()
                               {
                                   {|E128200:delegate*<int, int>|} f = null;
                               }
                           }
                           """, options: Options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsADelegate()
    {
        return AnalyzerTestRunner.VerifyAsync<FunctionPointerTypeAnalyzer>("""
                           class C
                           {
                               System.Func<int, int> field;
                           }
                           """, options: Options);
    }
}
