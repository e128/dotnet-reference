using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class RedundantHashSetInFrozenSetE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<RedundantHashSetInFrozenSetE128Analyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HashSetWithCollectionExpression_ToFrozenSet_Fires()
    {
        return VerifyAsync("""
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
    public Task HashSetWithCollectionInitializer_ToFrozenSet_Fires()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly FrozenSet<string> s = {|E128026:new HashSet<string>(StringComparer.Ordinal) { "h1", "h2" }.ToFrozenSet(StringComparer.Ordinal)|};
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HashSetWithNoComparer_ToFrozenSet_Fires()
    {
        return VerifyAsync("""
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
    public Task ArrayToFrozenSet_NoFire()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Frozen;
            class C
            {
                static readonly FrozenSet<string> s = new[] { ".PDF" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ListToFrozenSet_NoFire()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C
            {
                static readonly FrozenSet<string> s = new List<string> { ".PDF" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task HashSetWithoutToFrozenSet_NoFire()
    {
        return VerifyAsync("""
            using System;
            using System.Collections.Generic;
            class C
            {
                static readonly HashSet<string> s = new HashSet<string>(StringComparer.Ordinal) { "a" };
            }
            """);
    }
}
