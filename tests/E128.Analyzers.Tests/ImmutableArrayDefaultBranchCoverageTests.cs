using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ImmutableArrayDefaultBranchCoverageTests
{
    private static Task VerifyAsync(string members)
    {
        return AnalyzerTestRunner.VerifyAsync<ImmutableArrayDefaultAnalyzer>(
            "using System;\nusing System.Collections.Generic;\nusing System.Collections.Immutable;\nclass C\n{\n" + members + "}\n");
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenATupleDeconstructionAssignsTheField()
    {
        return VerifyAsync("""
                private ImmutableArray<int> _first;
                private ImmutableArray<int> _second;
                private ImmutableArray<int> _third;

                void Set(ImmutableArray<int> a, ImmutableArray<int> b, ImmutableArray<int> c)
                {
                    (_first, (_second, _third)) = (a, (b, c));
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAThisQualifiedAssignmentSetsTheField()
    {
        return VerifyAsync("""
                private ImmutableArray<int> _items;

                void Set(ImmutableArray<int> value)
                {
                    this._items = value;
                }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenARefOrOutArgumentPassesTheField()
    {
        return VerifyAsync("""
                private ImmutableArray<int> _viaRef;
                private ImmutableArray<int> _viaOut;

                void Fill()
                {
                    Replace(ref _viaRef);
                    Create(out _viaOut);
                }

                static void Replace(ref ImmutableArray<int> target) { target = ImmutableArray<int>.Empty; }

                static void Create(out ImmutableArray<int> target) { target = ImmutableArray<int>.Empty; }

            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyOtherTargetsAreAssignedOrPassed()
    {
        return VerifyAsync("""
                private ImmutableArray<int> {|E128163:_items|};
                private int _count;

                void Work(List<int> list, int[] numbers)
                {
                    list[0] = 1;
                    numbers[0] = 2;
                    _count = _items.Length;
                    int local = 0;
                    Touch(ref local);
                    Use(_items);
                }

                static void Touch(ref int value) { }

                static void Use(ImmutableArray<int> value) { }

            """);
    }
}
