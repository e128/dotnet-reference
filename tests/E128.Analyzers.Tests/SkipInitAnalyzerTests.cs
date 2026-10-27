using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SkipInitAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<SkipInitAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSkipInitIsCalled()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               void M()
                               {
                                   {|E128120:Unsafe.SkipInit(out int value)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheLocalIsInitialized()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M()
                               {
                                   int value = 0;
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
                               void M()
                               {
                                   {|E128120:SkipInit(out int value)|};
                               }
                           }
                           """);
    }
}
