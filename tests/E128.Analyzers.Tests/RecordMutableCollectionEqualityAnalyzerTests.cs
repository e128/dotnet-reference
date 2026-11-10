using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RecordMutableCollectionEqualityAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        return AnalyzerTestRunner.VerifyAsync<RecordMutableCollectionEqualityAnalyzer>(code, expected);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PositionalRecordClass_ListParameter_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Person({|E128177:List<string> Names|});
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RecordThatDeclaresGetHashCode_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;
                           using System.Linq;

                           record Person(List<string> Names)
                           {
                               public virtual bool Equals(Person? other) => other is not null && Names.SequenceEqual(other.Names);

                               public override int GetHashCode() => Names.Count;
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DerivedRecord_InheritsBaseGetHashCode_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Base
                           {
                               public override int GetHashCode() => 0;
                           }

                           record Derived : Base
                           {
                               public List<string> Names { get; init; } = new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NominalRecordClass_DictionaryProperty_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Order
                           {
                               {|E128177:public Dictionary<string, int> Lines { get; init; } = new();|}
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RecordStruct_ArrayParameter_Fires()
    {
        return VerifyAsync("""
                           record struct Point({|E128177:int[] Coordinates|});
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NominalRecordClass_MutableField_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Inventory
                           {
                               private List<int> {|E128177:Items = new()|};
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PositionalRecord_IListParameter_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Group({|E128177:IList<int> Items|});
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NominalRecordClass_HashSetProperty_Fires()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Tags
                           {
                               {|E128177:public HashSet<string> Values { get; init; } = new();|}
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ImmutableCollectionAndStringMembers_DoNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Frozen;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;
                           using System.Collections.ObjectModel;

                           record Snapshot
                           {
                               public ImmutableArray<int> Numbers { get; init; }
                               public ImmutableList<string> Names { get; init; } = ImmutableList<string>.Empty;
                               public ImmutableDictionary<string, int> Map { get; init; } = ImmutableDictionary<string, int>.Empty;
                               public ReadOnlyCollection<int> ReadOnly { get; init; } = new(new List<int>());
                               public IReadOnlyList<int> View { get; init; } = new List<int>();
                               public FrozenSet<int> FrozenValues { get; init; } = FrozenSet.Create(1);
                               public FrozenDictionary<string, int> FrozenMap { get; init; } = FrozenDictionary.Create<string, int>();
                               public string Label { get; init; } = "";
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonRecordClass_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           class Holder
                           {
                               public List<int> Items { get; } = new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ExpressionBodiedProperty_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Cache
                           {
                               public List<int> Items => new();
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task StaticMember_DoesNotFire()
    {
        return VerifyAsync("""
                           using System.Collections.Generic;

                           record Registry
                           {
                               public static List<int> Shared { get; } = new();
                           }
                           """);
    }
}
