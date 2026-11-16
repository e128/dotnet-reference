using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticReadonlyFrozenCollectionAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheStaticReadonlyFieldIsNotAGenericNamedType()
    {
        return AnalyzerTestRunner.VerifyAsync<StaticReadonlyFrozenCollectionAnalyzer>("""
            using System.Collections.Generic;

            class C
            {
                private static readonly string Name = "name";
                private static readonly int[] Numbers = [1, 2, 3];
                private static readonly List<int> Items = [1, 2, 3];
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheFrameworkHasNoFrozenCollections()
    {
        return AnalyzerTestRunner.VerifyAsync<StaticReadonlyFrozenCollectionAnalyzer>(
            """
            using System.Collections.Generic;

            class C
            {
                private static readonly HashSet<int> Items = new HashSet<int>();
            }
            """,
            expected: null,
            options: new AnalyzerTestOptions { References = ReferenceAssemblies.NetStandard.NetStandard20 });
    }
}
