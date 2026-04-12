using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class ToListInForeachE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ToListInForeachAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_DirectCall_Fires()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListInForeach_WithLinqChain_Fires()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToArrayInForeach_DoesNotFire()
    {
        return VerifyAsync("""
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
}
