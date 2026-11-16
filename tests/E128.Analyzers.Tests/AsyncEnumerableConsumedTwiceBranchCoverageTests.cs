using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncEnumerableConsumedTwiceBranchCoverageTests
{
    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncEnumerableConsumedTwiceAnalyzer>(
            "using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing System.Threading.Tasks;\n" + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAnExpressionBodiedMethod()
    {
        return VerifyAsync("""
            class C
            {
                (IAsyncEnumerator<int>, IAsyncEnumerator<int>) M(IAsyncEnumerable<int> source) =>
                    (source.GetAsyncEnumerator(), {|E128182:source|}.GetAsyncEnumerator());
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_InAnExpressionBodiedLocalFunctionAndAccessor()
    {
        return VerifyAsync("""
            class C
            {
                IAsyncEnumerable<int> Source = default!;

                void M(IAsyncEnumerable<int> source)
                {
                    (IAsyncEnumerator<int>, IAsyncEnumerator<int>) Local() =>
                        (source.GetAsyncEnumerator(), {|E128182:source|}.GetAsyncEnumerator());
                    _ = Local();
                }

                int Other
                {
                    get => Take(this.Source.GetAsyncEnumerator(), {|E128182:this.Source|}.GetAsyncEnumerator());
                }

                static int Take(IAsyncEnumerator<int> a, IAsyncEnumerator<int> b) => 0;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ThroughParenthesesAndConfigureAwait()
    {
        return VerifyAsync("""
            class C
            {
                async Task M(IAsyncEnumerable<int> source)
                {
                    await foreach (var item in (source))
                    {
                    }

                    await foreach (var item in ({|E128182:source|}).ConfigureAwait(false))
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForALocalWhoseTypeImplementsTheInterface()
    {
        return VerifyAsync("""
            sealed class Numbers : IAsyncEnumerable<int>
            {
                public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            }

            class C
            {
                async Task M(Numbers source)
                {
                    await foreach (var item in source)
                    {
                    }

                    await foreach (var item in {|E128182:source|})
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForSynchronousForeachOverAList()
    {
        return VerifyAsync("""
            class C
            {
                void M(List<int> source)
                {
                    foreach (var item in source)
                    {
                    }

                    foreach (var item in source)
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMembersOfAnotherInstanceCallsAndProperties()
    {
        return VerifyAsync("""
            class C
            {
                IAsyncEnumerable<int> Source { get; } = default!;

                IAsyncEnumerable<int> Make() => Source;

                async Task M(C other)
                {
                    await foreach (var item in other.Source)
                    {
                    }

                    await foreach (var item in other.Source)
                    {
                    }

                    await foreach (var item in Make())
                    {
                    }

                    await foreach (var item in Make())
                    {
                    }

                    await foreach (var item in this.Source)
                    {
                    }

                    await foreach (var item in this.Source)
                    {
                    }
                }
            }
            """);
    }
}
