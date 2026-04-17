using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OrderByFirstE128CodeFixTests
{
    private static Task VerifyFixAsync(string source, string fixedCode)
    {
        return new CSharpCodeFixTest<OrderByFirstAnalyzer, OrderByFirstCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            NumberOfFixAllIterations = 1
        }.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesOrderByFirst_WithMinBy()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M()
                {
                    var list = new List<int> { 3, 1, 2 };
                    var x = {|E128009:list.OrderBy(i => i).First()|};
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
                    var list = new List<int> { 3, 1, 2 };
                    var x = list.MinBy(i => i);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesOrderByDescendingFirst_WithMaxBy()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M()
                {
                    var list = new List<int> { 3, 1, 2 };
                    var x = {|E128009:list.OrderByDescending(i => i).First()|};
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
                    var list = new List<int> { 3, 1, 2 };
                    var x = list.MaxBy(i => i);
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task CodeFix_ReplacesOrderByFirstOrDefault_WithMinBy()
    {
        return VerifyFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M()
                {
                    var list = new List<int> { 3, 1, 2 };
                    var x = {|E128009:list.OrderBy(i => i).FirstOrDefault()|};
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
                    var list = new List<int> { 3, 1, 2 };
                    var x = list.MinBy(i => i);
                }
            }
            """);
    }
}
