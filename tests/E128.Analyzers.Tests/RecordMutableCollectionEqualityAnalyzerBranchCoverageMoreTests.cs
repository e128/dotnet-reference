using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecordMutableCollectionEqualityAnalyzerBranchCoverageMoreTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBaseRecordOnlyHasTheGeneratedGetHashCode()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            using System.Collections.Generic;

            record Base(int Id);

            record Derived(int Id, {|E128177:List<int> Items|}) : Base(Id);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenTheBaseRecordOnlyDeclaresAGetHashCodeOverload()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            using System.Collections.Generic;

            record Base(int Id)
            {
                public int GetHashCode(int salt) => Id + salt;
            }

            record Derived(int Id, {|E128177:List<int> Items|}) : Base(Id);
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_ForMembersOfATypeParameterType()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>("""
            record Box<T>(T Item)
            {
                public T Other { get; init; } = Item;
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenThePropertyDeclaresNoAccessors()
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>(
            """
            using System.Collections.Generic;

            record Widget
            {
                public List<int> Items { }
            }
            """,
            options: new AnalyzerTestOptions { CompilerDiagnostics = CompilerDiagnostics.None });
    }
}
