using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AggressiveOptimizationAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheOptionIsTheOnlyArgument()
    {
        return AnalyzerTestRunner.VerifyAsync<AggressiveOptimizationAnalyzer>("""
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
        return AnalyzerTestRunner.VerifyAsync<AggressiveOptimizationAnalyzer>("""
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
    public Task Reports_WhenTheAttributeUsesTheSuffixOrAFullyQualifiedName()
    {
        return AnalyzerTestRunner.VerifyAsync<AggressiveOptimizationAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           class C
                           {
                               [{|E128116:MethodImplAttribute(MethodImplOptions.AggressiveOptimization)|}]
                               void M()
                               {
                               }

                               [{|E128116:System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                               void N()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OnPartialMethodDeclaration()
    {
        return AnalyzerTestRunner.VerifyAsync<AggressiveOptimizationAnalyzer>("""
                           using System.Runtime.CompilerServices;
                           partial class C
                           {
                               [{|E128116:MethodImpl(MethodImplOptions.AggressiveOptimization)|}]
                               static partial void M();

                               static partial void M()
                               {
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenOnlyInliningIsRequested()
    {
        return AnalyzerTestRunner.VerifyAsync<AggressiveOptimizationAnalyzer>("""
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
