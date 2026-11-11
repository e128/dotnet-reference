using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeCopyReferenceTypeAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTypeHoldsAReference()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(ref Holder target, void* source)
                               {
                                   {|E128129:Unsafe.Copy(ref target, source)|};
                               }
                           }

                           class Holder
                           {
                               public string Text { get; set; } = string.Empty;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTypeIsUnmanaged()
    {
        return AnalyzerTestRunner.VerifyAsync<UnsafeCopyReferenceTypeAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           unsafe class C
                           {
                               void M(ref int target, void* source)
                               {
                                   Unsafe.Copy(ref target, source);
                               }
                           }
                           """);
    }
}
