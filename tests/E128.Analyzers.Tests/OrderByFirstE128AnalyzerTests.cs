using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class OrderByFirstE128AnalyzerTests
{
    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<OrderByFirstAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderByFirst_Fires_E128009()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderByDescendingFirst_Fires_E128009()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task OrderByFirstOrDefault_Fires_E128009()
    {
        return VerifyAsync("""
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
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task MinBy_DoesNotFire()
    {
        return VerifyAsync("""
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
    public Task FirstWithoutOrderBy_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                void M()
                {
                    var list = new List<int> { 3, 1, 2 };
                    var x = list.First();
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task NonLinqOrderBy_DoesNotFire()
    {
        return VerifyAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                List<int> OrderBy(System.Func<int, int> k) => new List<int>();
                void M()
                {
                    var x = OrderBy(i => i).First();
                }
            }
            """);
    }
}
