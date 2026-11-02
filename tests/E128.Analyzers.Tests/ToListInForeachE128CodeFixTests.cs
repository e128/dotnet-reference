using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ToListInForeachE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode) =>
        CodeFixTestRunner.RunAsync<ToListInForeachAnalyzer, ToListInForeachCodeFixProvider>(source, fixedCode);

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_CodeFix_ReplacesWithToArray()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(IEnumerable<int> items)
                {
                    foreach (var x in {|E128018:items.ToList()|})
                    {
                    }
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(IEnumerable<int> items)
                {
                    foreach (var x in items.ToArray())
                    {
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_WithLinqChain_CodeFix_ReplacesWithToArray()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(IEnumerable<int> items)
                {
                    foreach (var x in {|E128018:items.Where(i => i > 0).ToList()|})
                    {
                    }
                }
            }
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M(IEnumerable<int> items)
                {
                    foreach (var x in items.Where(i => i > 0).ToArray())
                    {
                    }
                }
            }
            """);
    }
}
