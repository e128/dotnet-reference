using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AggressiveOptimizationAnalyzerTests
{
    private static Task VerifyAsync(string code)
    {
        return new CSharpAnalyzerTest<AggressiveOptimizationAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOptionIsTheOnlyArgument()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                               void M()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOptionIsCombined()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               [{|E128116:MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)|}]
                               void M()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyInliningIsRequested()
    {
        return VerifyAsync("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               [MethodImpl(MethodImplOptions.AggressiveInlining)]
                               void M()
                               {
                               }
                           }
                           """);
    }
}
