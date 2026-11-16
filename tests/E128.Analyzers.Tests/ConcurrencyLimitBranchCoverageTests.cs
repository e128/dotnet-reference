using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ConcurrencyLimitBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheSemaphoreCountIsAConstantExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            class C
            {
                const int Limit = 1 - 3;
                SemaphoreSlim M() => {|E128040:new SemaphoreSlim(Limit, 4)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenATargetTypedSemaphoreHasANegativeCount()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            class C
            {
                SemaphoreSlim M() => {|E128040:new(-2)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheFullyQualifiedSemaphoreHasANegativeCount()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            class C
            {
                System.Threading.SemaphoreSlim M() => {|E128040:new global::System.Threading.SemaphoreSlim(-2)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheSemaphoreCountIsNotAConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading;
            class C
            {
                SemaphoreSlim M(int count) => new(count);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeHasTheSemaphoreName()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            class SemaphoreSlim
            {
                public SemaphoreSlim(int count) { }
            }
            class C
            {
                SemaphoreSlim M() => new SemaphoreSlim(-2);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenParallelDegreeIsANegativeConstantExpression()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                const int Degree = 1 - 3;
                ParallelOptions M() => new ParallelOptions { MaxDegreeOfParallelism = {|E128040:Degree|} };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenParallelDegreeIsPositiveOrNotConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading.Tasks;
            class C
            {
                void M(ParallelOptions options, int degree)
                {
                    options.MaxDegreeOfParallelism = 4;
                    options.MaxDegreeOfParallelism = degree;
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeHasAPropertyWithTheSameName()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            class Options { public int MaxDegreeOfParallelism { get; set; } }
            class C
            {
                void M(Options options) { options.MaxDegreeOfParallelism = 0; }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenChannelCapacityIsANegativeConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading.Channels;
            class C
            {
                const int Capacity = 1 - 3;
                Channel<int> M() => {|E128040:Channel.CreateBounded<int>(Capacity)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenChannelCapacityIsNotAnIntConstant()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            using System.Threading.Channels;
            class C
            {
                Channel<int> M(int capacity) => Channel.CreateBounded<int>(capacity);
                Channel<int> N(BoundedChannelOptions options) => Channel.CreateBounded<int>(options);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAnotherTypeHasACreateBoundedMethod()
    {
        return AnalyzerTestRunner.VerifyAsync<ConcurrencyLimitAnalyzer>("""
            class Channel
            {
                public static int CreateBounded(int capacity) => capacity;
            }
            class C
            {
                int M() => Channel.CreateBounded(0);
            }
            """);
    }
}
