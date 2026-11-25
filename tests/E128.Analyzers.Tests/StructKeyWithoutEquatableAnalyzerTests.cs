using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StructKeyWithoutEquatableAnalyzerTests
{
    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenDictionaryKeyStructLacksEquatable()
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
                                   var map = {|E128253:new Dictionary<Key, int>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenHashSetKeyStructLacksEquatable()
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
                                   var set = {|E128253:new HashSet<Key>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenConcurrentDictionaryKeyStructLacksEquatable()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Concurrent;

                           struct Key
                           {
                               public int Value;
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = {|E128253:new ConcurrentDictionary<Key, int>()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCreationIsTargetTyped()
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
                                   Dictionary<Key, int> map = {|E128253:new()|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenOnlyCapacityIsPassed()
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
                                   var map = {|E128253:new Dictionary<Key, int>(16)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Reports_WhenCopyingAnotherDictionary()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           struct Key
                           {
                               public int Value;
                           }

                           class C
                           {
                               void M(Dictionary<Key, int> source)
                               {
                                   var map = {|E128253:new Dictionary<Key, int>(source)|};
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyImplementsEquatable()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           struct Key : IEquatable<Key>
                           {
                               public int Value;

                               public bool Equals(Key other) => Value == other.Value;

                               public override bool Equals(object? obj) => obj is Key other && Equals(other);

                               public override int GetHashCode() => Value;
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Key, int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyIsARecordStruct()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           record struct Key(int Value);

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Key, int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenCreationPassesACustomComparer()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           struct Key
                           {
                               public int Value;
                           }

                           sealed class KeyComparer : IEqualityComparer<Key>
                           {
                               public bool Equals(Key x, Key y) => x.Value == y.Value;

                               public int GetHashCode(Key obj) => obj.Value;
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Key, int>(new KeyComparer());
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyIsANamedValueTuple()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<(string A, string B), int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenHashSetKeyIsAValueTuple()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           enum Source
                           {
                               One
                           }

                           class C
                           {
                               void M()
                               {
                                   var set = new HashSet<(Source, string)>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyIsAClass()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           sealed class Key
                           {
                               public int Value;
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Key, int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyComesFromMetadata()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System;
                           using System.Collections.Generic;

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Guid, int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyIsAnEnum()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           enum Key
                           {
                               One,
                               Two
                           }

                           class C
                           {
                               void M()
                               {
                                   var map = new Dictionary<Key, int>();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenKeyIsATypeParameter()
    {
        return AnalyzerTestRunner.VerifyAsync<StructKeyWithoutEquatableAnalyzer>("""
                           using System.Collections.Generic;

                           class C
                           {
                               void M<T>()
                               {
                                   var map = new Dictionary<T, int>();
                               }
                           }
                           """);
    }
}
