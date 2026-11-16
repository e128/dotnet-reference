using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitShapeBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCreationsThatAreNotSemaphores()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                void M()
                {
                    var a = new object();
                    var b = new List<int>(5);
                    var c = new List<int>(-1 + 3);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForASemaphoreNamedTypeInTheGlobalNamespace()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            class SemaphoreSlim { public SemaphoreSlim(int count) { } }
            class C
            {
                void M()
                {
                    var a = new global::SemaphoreSlim(-1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForASemaphoreInAForeignThreadingNamespace()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            namespace Other.Threading
            {
                public class SemaphoreSlim { public SemaphoreSlim(int count) { } }
            }
            class C
            {
                void M()
                {
                    var a = new Other.Threading.SemaphoreSlim(-1);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAssignmentsThatAreNotTheDegreeProperty()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Collections.Generic;
            class Options { public int MaxDegreeOfParallelism; }
            class C
            {
                void M(List<int> list, Options options)
                {
                    list[0] = 0;
                    options.MaxDegreeOfParallelism = 0;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForCallsThatAreNotBoundedChannelCreation()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            class Factory { public int CreateBounded() => 0; public int Other(int n) => n; }
            class C
            {
                void M(Factory factory)
                {
                    Helper(0);
                    factory.CreateBounded();
                    factory.Other(0);
                }

                static void Helper(int value) { }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForAPositiveConstantWrittenWithAUnaryPlus()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            using System.Threading.Tasks;
            class C
            {
                void M(ParallelOptions options)
                {
                    options.MaxDegreeOfParallelism = +4;
                    var semaphore = new SemaphoreSlim(+1);
                }
            }
            """);
    }
}
