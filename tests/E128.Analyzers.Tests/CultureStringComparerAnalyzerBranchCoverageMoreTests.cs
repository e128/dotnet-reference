using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CultureStringComparerAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreationHasNoArgumentList()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
            using System.Collections.Generic;
            class C
            {
                HashSet<string> Set = new HashSet<string> { "a" };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCreatedTypeIsATypeParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
            class C
            {
                T Make<T>() where T : new()
                {
                    return new T();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStringCollectionIsNotATrackedType()
    {
        return AnalyzerTestRunner.VerifyAsync<CultureStringComparerAnalyzer>("""
            using System;
            using System.Collections.Generic;
            class C
            {
                List<string> Items = new List<string>(StringComparer.CurrentCulture.GetType().Name.Length);
            }
            """);
    }
}
