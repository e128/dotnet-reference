using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StructKeyWithoutEquatableBranchCoverageTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenComparerArgumentIsTypedAsTheInterface()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           struct Key
                           {
                               public int Value;
                           }

                           class C
                           {
                               void M(IEqualityComparer<Key> comparer)
                               {
                                   var map = new Dictionary<Key, int>(comparer);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenGenericKeyImplementsEquatable()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           struct Key<T> : IEquatable<Key<T>>
                           {
                               public bool Equals(Key<T> other) => true;

                               public override bool Equals(object? obj) => obj is Key<T>;

                               public override int GetHashCode() => 0;
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Key<int>, string>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenKeyImplementsEquatableOfAnotherType()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           struct Other
                           {
                               public int Value;
                           }

                           struct Key : IEquatable<Other>
                           {
                               public int Value;

                               public bool Equals(Other other) => Value == other.Value;
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = {|E128253:new Dictionary<Key, int>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_OncePerCreation()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           struct Key
                           {
                               public int Value;
                           }

                           class C
                           {
                               void M()
                               {
                                   var first = {|E128253:new Dictionary<Key, int>()|};
                                   var second = {|E128253:new HashSet<Key>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyIsAnUnresolvedType()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<{|CS0246:Missing|}, int>();
                               }
                           }
                           """);
    }
}
