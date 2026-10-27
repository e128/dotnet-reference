using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class UnsafeCopyReferenceTypeAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<UnsafeCopyReferenceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheTypeHoldsAReference()
    {
        return VerifyAsync("""
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
        return VerifyAsync("""
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
