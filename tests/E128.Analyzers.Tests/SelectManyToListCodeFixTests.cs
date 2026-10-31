using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class SelectManyToListCodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<SelectManyToListAnalyzer, SelectManyToListCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        }.RunAsync();
    }

    private static Task VerifyNoFixAsync(string source)
    {
        return new CSharpCodeFixTest<SelectManyToListAnalyzer, SelectManyToListCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllInDocumentIterations = 0
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_RewritesToForeachAddRange()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M()
                {
                    var lists = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
                    var flat = {|E128085:lists.SelectMany(x => x).ToList()|};
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M()
                {
                    var lists = new List<List<int>> { new() { 1, 2 }, new() { 3, 4 } };
                    var flat = new List<int>();
                    foreach (var x in lists)
                    {
                        flat.AddRange(x);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_AddsTheGenericCollectionsUsing()
    {
        // Implicit usings are off in this repo, so the emitted List<T> needs its namespace.
        return VerifyFixAsync(
            """
            using System.Linq;
            class C
            {
                void M(int[][] items)
                {
                    var flat = {|E128085:items.SelectMany(x => x).ToList()|};
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(int[][] items)
                {
                    var flat = new List<int>();
                    foreach (var x in items)
                    {
                        flat.AddRange(x);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_NotOffered_WhenTheCallIsNestedInAnExpression()
    {
        // The rewrite replaces a whole statement, so a nested call has nothing it can replace.
        return VerifyNoFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(List<List<int>> lists)
                {
                    var count = {|E128085:lists.SelectMany(x => x).ToList()|}.Count;
                }
            }
            """);
    }
}
