using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecordMutableCollectionEqualityBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForStaticAndConstantFields()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            using System.Collections.Generic;

            record Widget
            {
                public static List<int> Shared = new();
                public const List<int>? Missing = null;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnInstanceFieldNextToAStaticField()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            using System.Collections.Generic;

            record Widget
            {
                public static List<int> Shared = new();
                public List<int> {|E128177:Items = new()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForPropertiesThatNeverJoinEquality()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            using System.Collections.Generic;

            abstract record Widget
            {
                public static List<int> Shared { get; set; } = new();
                public abstract List<int> Declared { get; init; }
                public List<int> Computed => new();
                public List<int> WithBody { get { return new(); } }
                public List<int> WithSetterBody { get; set { _ = value; } }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForAnArrayInARecordStruct()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            record struct Pair({|E128177:int[] Values|});
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_ForADerivedRecordWhenTheBaseHasOnlyTheGeneratedHashCode()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            using System.Collections.Generic;

            record Base(int Id);

            record Derived(int Id) : Base(Id)
            {
                {|E128177:public HashSet<int> Tags { get; init; } = new();|}
            }
            """);
    }
}
