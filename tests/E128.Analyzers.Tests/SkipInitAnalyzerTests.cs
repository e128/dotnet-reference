using System.Threading.Tasks;
using E128.Analyzers.Unsafe;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SkipInitAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenSkipInitIsCalled()
    {
        return AnalyzerTestRunner.VerifyAsync<SkipInitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SkipInitAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<SkipInitAnalyzer>("""
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
