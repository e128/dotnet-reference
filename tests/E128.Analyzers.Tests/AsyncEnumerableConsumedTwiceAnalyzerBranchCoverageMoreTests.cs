using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class AsyncEnumerableConsumedTwiceAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheTargetFrameworkHasNoAsyncEnumerable()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncEnumerableConsumedTwiceAnalyzer>(
            """
            using System.Collections.Generic;
            class C
            {
                void M(List<int> source)
                {
                    foreach (var first in source)
                    {
                    }

                    foreach (var second in source)
                    {
                    }
                }
            }
            """,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForACallWithOneArgumentThatIsNotAWrapper()
    {
        return AnalyzerTestRunner.VerifyAsync<AsyncEnumerableConsumedTwiceAnalyzer>("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            class Factory
            {
                public IAsyncEnumerable<int> Create(int count) => throw new NotSupportedException();
            }

            class C
            {
                async Task M(Factory factory)
                {
                    await foreach (var first in factory.Create(1))
                    {
                    }

                    await foreach (var second in factory.Create(1))
                    {
                    }
                }
            }
            """);
    }
}
