using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticReadonlyFrozenCollectionAnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<StaticReadonlyFrozenCollectionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    #region Fires

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnHashSet_WhenStaticReadonlyWithInitializer()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:HashSet<string> Tags = new() { "a", "b" }|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnHashSet_WhenStaticReadonlyWithComparer()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase) { "a", "b" }|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnDictionary_WhenStaticReadonlyWithInitializer()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:Dictionary<string, int> Lookup = new() { ["x"] = 1, ["y"] = 2 }|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FiresOnDictionary_WhenStaticReadonlyWithComparer()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:Dictionary<string, int> Lookup = new(StringComparer.Ordinal) { ["x"] = 1 }|};
            }
            """);
    }

    #endregion Fires

    #region Does not fire

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenAlreadyFrozenSet()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenSet<string> Tags =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b" }.ToFrozenSet();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenAlreadyFrozenDictionary()
    {
        return VerifyAsync("""
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenDictionary<string, int> Lookup =
                    new Dictionary<string, int> { ["x"] = 1 }.ToFrozenDictionary();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenInstanceField()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                private readonly HashSet<string> _items = new() { "a" };
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenNotReadonly()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                private static HashSet<string> _cache = new();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenInitializedInStaticConstructor()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            class C
            {
                private static readonly HashSet<string> Built;
                static C()
                {
                    Built = new HashSet<string>();
                    Built.Add("x");
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task DoesNotFire_WhenImmutableHashSet()
    {
        return VerifyAsync("""
            using System.Collections.Immutable;
            class C
            {
                private static readonly ImmutableHashSet<string> Safe = ImmutableHashSet.Create("a", "b");
            }
            """);
    }

    #endregion Does not fire
}
