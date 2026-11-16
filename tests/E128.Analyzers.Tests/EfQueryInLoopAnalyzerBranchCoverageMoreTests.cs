using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EfQueryInLoopAnalyzerBranchCoverageMoreTests
{
    // The test project carries no EF Core package. Each snippet declares the query surface as a stub.
    private const string EfStub = """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Linq;
        using Microsoft.EntityFrameworkCore;

        namespace Microsoft.EntityFrameworkCore
        {
            public abstract class DbSet<T> : IQueryable<T>
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

        class Holder
        {
            public IQueryable<Item> Query { get; set; } = Enumerable.Empty<Item>().AsQueryable();
        }

        """;

    private static Task VerifyAsync(string code)
    {
        return AnalyzerTestRunner.VerifyAsync<EfQueryInLoopAnalyzer>(EfStub + code);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheCompilationHasNoDbSetType()
    {
        return AnalyzerTestRunner.VerifyAsync<EfQueryInLoopAnalyzer>(
            """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                void M(List<int> local, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var all = local.ToList();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsAnArray()
    {
        return VerifyAsync("""
            class C
            {
                void M(int[] values, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var found = values.Contains(id);
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheReceiverIsATypeParameterConstrainedToQueryable()
    {
        return VerifyAsync("""
            class C
            {
                void M<TQuery>(TQuery source, int[] ids)
                    where TQuery : IQueryable<Item>
                {
                    foreach (var id in ids)
                    {
                        var all = source.ToList();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Fires_WhenACastParenthesizedDbSetIsMaterialized()
    {
        return VerifyAsync("""
            class C
            {
                void M(DbSet<Item> items, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var all = {|E128180:((IQueryable<Item>)items).ToList()|};
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenTheQueryableComesFromAMemberOfAnotherObject()
    {
        return VerifyAsync("""
            class C
            {
                void M(Holder holder, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var all = holder.Query.ToList();
                    }
                }
            }
            """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Silent_WhenAStaticQueryableCallStartsAtANamespace()
    {
        return VerifyAsync("""
            class C
            {
                void M(List<Item> local, int[] ids)
                {
                    foreach (var id in ids)
                    {
                        var all = System.Linq.Queryable.AsQueryable(local).ToList();
                    }
                }
            }
            """);
    }
}
