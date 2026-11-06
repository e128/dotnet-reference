using System.Threading.Tasks;
using E128.Analyzers.Performance;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace E128.Analyzers.Tests;

public sealed class EfQueryInLoopAnalyzerTests
{
    // The test project carries no EF Core package. Each snippet declares the surface under test as a stub
    // in the Microsoft.EntityFrameworkCore namespace: a DbSet<T> that implements IQueryable<T> and the
    // ToListAsync extension. Both compile, so the semantic model resolves the receiver and the method.
    // The using directives sit here because a using clause must precede every namespace in the file.
    private const string EfStub = """
                                  using System;
                                  using System.Collections;
                                  using System.Collections.Generic;
                                  using System.Linq;
                                  using System.Threading.Tasks;
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

                                      public static class EntityFrameworkQueryableExtensions
                                      {
                                          public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
                                              => throw new NotImplementedException();
                                      }
                                  }

                                  """;

    private static Task VerifyAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<EfQueryInLoopAnalyzer, DefaultVerifier>
        {
            TestCode = EfStub + code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Contains_OnDbSetInForeach_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items, Item[] known)
                               {
                                   foreach (var item in known)
                                   {
                                       var exists = {|E128180:items.Contains(item)|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_OnDbSetInDeconstructionForeach_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items, (int Id, int Count)[] pairs)
                               {
                                   foreach (var (id, count) in pairs)
                                   {
                                       var rows = {|E128180:items.ToList()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_OnDbSetInForeach_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items, int[] ids)
                               {
                                   foreach (var id in ids)
                                   {
                                       var rows = {|E128180:items.ToList()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task WhereThenToList_InForLoop_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items)
                               {
                                   for (int i = 0; i < 10; i++)
                                   {
                                       var rows = {|E128180:items.Where(x => x.Id > i).ToList()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToListAsync_InWhileLoop_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               async Task M(DbSet<Item> items)
                               {
                                   int i = 0;
                                   while (i < 10)
                                   {
                                       var rows = await {|E128180:items.ToListAsync()|};
                                       i++;
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task Count_InDoWhileLoop_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items)
                               {
                                   int i = 0;
                                   do
                                   {
                                       var total = {|E128180:items.Count()|};
                                       i++;
                                   } while (i < 10);
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsEnumerable_AsNestedForEachSource_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items, int[] ids)
                               {
                                   foreach (var id in ids)
                                   {
                                       foreach (var item in {|E128180:items.AsEnumerable()|})
                                       {
                                       }
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsEnumerable_InLoopBodyWithoutForEach_DoesNotFire()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items, int[] ids)
                               {
                                   foreach (var id in ids)
                                   {
                                       var rows = items.AsEnumerable();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task AsEnumerable_AsForEachSourceWithoutLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items)
                               {
                                   foreach (var item in items.AsEnumerable())
                                   {
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task QuerySyntaxThenToList_InForeach_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class Store { public DbSet<Item> Rows { get; set; } }
                           class C
                           {
                               void M(Store db, int[] ids)
                               {
                                   foreach (var id in ids)
                                   {
                                       var rows = {|E128180:(from r in db.Rows where r.Id > id select r).ToList()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_InsideLambdaInsideLoop_Fires()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items, int[] ids)
                               {
                                   foreach (var id in ids)
                                   {
                                       Func<List<Item>> load = () => {|E128180:items.ToList()|};
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_AsForEachSource_DoesNotFire()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items)
                               {
                                   foreach (var item in items.ToList()) { }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_OnListInForeach_DoesNotFire()
    {
        return VerifyAsync("""
                           class C
                           {
                               void M(List<int> values)
                               {
                                   foreach (var value in values)
                                   {
                                       var copy = values.ToList();
                                   }
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_OutsideLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items)
                               {
                                   var rows = items.ToList();
                               }
                           }
                           """);
    }

    [Fact]
    [Trait("Category", "CI")]
    public Task ToList_InsideLambdaWithoutLoop_DoesNotFire()
    {
        return VerifyAsync("""
                           class Item { public int Id { get; set; } }
                           class C
                           {
                               void M(DbSet<Item> items)
                               {
                                   var query = items.Where(x => items.ToList().Count > x.Id);
                               }
                           }
                           """);
    }
}
