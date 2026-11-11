using System.Threading.Tasks;
using E128.Analyzers.Reliability;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class CollectionMutationInEnumerationAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task MutationInOwnLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in {|E128155:items|})
                                   {
                                       items.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task IndexerSetInOwnLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in {|E128155:items|})
                                   {
                                       items[0] = item;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MaterializedSource_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           using System.Linq;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in items.ToArray())
                                   {
                                       items.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DifferentCollectionMutated_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items, List<int> other)
                               {
                                   foreach (var item in items)
                                   {
                                       other.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SameFieldOnOtherInstance_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               private readonly List<int> _children = new();

                               void M(C other)
                               {
                                   foreach (var child in _children)
                                   {
                                       other._children.Add(child);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task SameFieldThroughThis_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               private readonly List<int> _children = new();

                               void M()
                               {
                                   foreach (var child in {|E128155:_children|})
                                   {
                                       this._children.Add(child);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ImmutableCollection_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Immutable;
                           class C
                           {
                               void M(ImmutableArray<int> items)
                               {
                                   foreach (var item in items)
                                   {
                                       _ = items.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MutationInsideLambda_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System;
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in items)
                                   {
                                       Action later = () => items.Add(item);
                                       later();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FieldMutatedInOwnLoop_Fires()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               private readonly List<int> _items = new();

                               void M()
                               {
                                   foreach (var item in {|E128155:_items|})
                                   {
                                       _items.Add(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ReadOnlyMemberCall_DoesNotFire()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in items)
                                   {
                                       _ = items.Count;
                                       _ = items.Contains(item);
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ArrayElementWriteInOwnLoop_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           class C
                           {
                               void M(int[] items)
                               {
                                   foreach (var item in items)
                                   {
                                       items[0] = item;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemoveThenBreak_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in items)
                                   {
                                       items.Remove(item);
                                       break;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task RemoveThenReturn_Silent()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in items)
                                   {
                                       items.Remove(item);
                                       return;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task PlainRemoveInOwnLoop_StillFires()
    {
        return AnalyzerTestRunner.VerifyAsync<CollectionMutationInEnumerationAnalyzer>("""
                           using System.Collections.Generic;
                           class C
                           {
                               void M(List<int> items)
                               {
                                   foreach (var item in {|E128155:items|})
                                   {
                                       items.Remove(item);
                                   }
                               }
                           }
                           """);
    }
}
