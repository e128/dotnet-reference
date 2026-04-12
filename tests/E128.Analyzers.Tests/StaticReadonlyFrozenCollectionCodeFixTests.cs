using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class StaticReadonlyFrozenCollectionCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<StaticReadonlyFrozenCollectionAnalyzer, StaticReadonlyFrozenCollectionCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixHashSet_TransformsToFrozenSet()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:HashSet<string> Tags = new() { "a", "b" }|};
            }
            """,
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenSet<string> Tags = new HashSet<string>() { "a", "b" }.ToFrozenSet();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixDictionary_TransformsToFrozenDictionary()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:Dictionary<string, int> Lookup = new() { ["x"] = 1, ["y"] = 2 }|};
            }
            """,
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenDictionary<string, int> Lookup = new Dictionary<string, int>() { ["x"] = 1, ["y"] = 2 }.ToFrozenDictionary();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task FixHashSet_PreservesCustomComparer()
    {
        return VerifyFixAsync(
            """
            using System;
            using System.Collections.Generic;
            class C
            {
                private static readonly {|E128027:HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase) { "a", "b" }|};
            }
            """,
            """
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                private static readonly FrozenSet<string> Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b" }.ToFrozenSet();
            }
            """);
    }
}
