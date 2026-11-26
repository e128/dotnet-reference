using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EfQueryInLoopBranchCoverageTests
{
    // The test project carries no EF Core package. Each snippet declares the query surface as a stub.
    private const string EfStub = TestStubs.EfQueryInLoopStub + """

        namespace Microsoft.EntityFrameworkCore.Query
        {
            public abstract class RawQuery<T> : IQueryable<T>
            {
                public Type ElementType => typeof(T);
                public System.Linq.Expressions.Expression Expression => throw new NotImplementedException();
                public IQueryProvider Provider => throw new NotImplementedException();
                public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
        }

        class Item
        {
            public int Id { get; set; }
        }

        """;

    private static Task VerifyAsync(string code, AnalyzerTestOptions? options = null)
    {
        return AnalyzerTestRunner.VerifyAsync<EfQueryInLoopAnalyzer>(EfStub + code, expected: null, options: options);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAParenthesizedAsEnumerableFeedsANestedForeach()
    {
        return VerifyAsync("""
            class C
            {
                void M(DbSet<Item> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        foreach (var item in ({|E128180:items.AsEnumerable()|}))
                        {
                            _ = item.Id + id;
                        }
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenAnOrderedQueryOverADbSetMaterializes()
    {
        return VerifyAsync("""
            class C
            {
                void M(DbSet<Item> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var ordered = {|E128180:items.OrderBy(i => i.Id).ToList()|};
                        var casted = {|E128180:((IQueryable<Item>)items).Where(i => i.Id == id).ToList()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenTheQueryTypeLivesInAnEntityFrameworkNamespace()
    {
        return VerifyAsync("""
            class C
            {
                void M(Microsoft.EntityFrameworkCore.Query.RawQuery<Item> rows, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var all = {|E128180:rows.ToList()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheQueryableHasNoEntityFrameworkRoot()
    {
        return VerifyAsync("""
            class C
            {
                void M(List<Item> local, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var plain = local.AsQueryable().ToList();
                        var query = (from i in local.AsQueryable() where i.Id > id select i).ToList();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCallIsNotAMemberAccessOrDoesNotBind()
    {
        return VerifyAsync(
            """
            class C
            {
                static int ToList() => 0;

                void M(DbSet<Item> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        _ = ToList();
                        _ = items.ToList(1, 2, 3);
                    }
                }
            }
            """,
            new AnalyzerTestOptions { CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None });
    }
}
