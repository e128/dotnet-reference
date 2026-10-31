using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RedundantHashSetInFrozenSetE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<RedundantHashSetInFrozenSetE128Analyzer, RedundantHashSetInFrozenSetCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return new CSharpCodeFixTest<RedundantHashSetInFrozenSetE128Analyzer, RedundantHashSetInFrozenSetCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_UnwrapsRedundantHashSet()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly int[] values = [1, 2, 3];
                static readonly FrozenSet<int> s = {|E128026:new HashSet<int>(values).ToFrozenSet()|};
            }
            """,
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly int[] values = [1, 2, 3];
                static readonly FrozenSet<int> s = values.ToFrozenSet();
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheArgumentIsACollectionExpression()
    {
        // A collection expression takes its type from the target, and ToFrozenSet() gives it none.
        return VerifyNoFixAsync(
            """
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly FrozenSet<int> s = {|E128026:new HashSet<int>([1, 2, 3]).ToFrozenSet()|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheHashSetTakesAComparer()
    {
        // The comparer decides which source entries count as duplicates, so dropping it changes the set.
        return VerifyNoFixAsync(
            """
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly FrozenSet<string> s = {|E128026:new HashSet<string>([".PDF"], StringComparer.OrdinalIgnoreCase).ToFrozenSet(StringComparer.OrdinalIgnoreCase)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheHashSetHasACollectionInitializer()
    {
        // The rewrite passes the constructor argument to ToFrozenSet(), so the initializer entries go.
        return VerifyNoFixAsync(
            """
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly FrozenSet<string> s = {|E128026:new HashSet<string>(StringComparer.Ordinal) { "h1", "h2" }.ToFrozenSet(StringComparer.Ordinal)|};
            }
            """);
    }
}
