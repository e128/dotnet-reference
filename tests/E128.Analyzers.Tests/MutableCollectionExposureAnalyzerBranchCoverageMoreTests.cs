using System.Threading.Tasks;
using E128.Analyzers.Design;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class MutableCollectionExposureAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMethodsThatReturnArraysOrTypeParameters()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
            using System.Collections.Generic;

            class Source
            {
                public List<int>[] Buckets() => [];

                public T Single<T>(T value) => value;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForPropertiesThatAreArraysOrTypeParameters()
    {
        return AnalyzerTestRunner.VerifyAsync<MutableCollectionExposureAnalyzer>("""
            using System.Collections.Generic;

            class Source<T>
            {
                public List<int>[] Buckets { get; } = [];

                public T Value { get; } = default!;
            }
            """);
    }
}
